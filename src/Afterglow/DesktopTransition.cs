using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace Afterglow;

// One owned child window per display hosts a DirectComposition visual tree.
// All displays share the same uploaded textures and one composition commit.
internal sealed class DesktopTransition : IDisposable
{
    internal static string LastFailure {get;private set;}="";
    private readonly RenderWorker worker=new("Afterglow GPU wallpaper");
    private readonly List<CompositionLayer> layers=new();
    private GpuComposition? gpu;
    private IntPtr parent,wallpaperHost;
    private bool disposed,animating;
    private Task operation=Task.CompletedTask;
    private Func<BitmapSource>? shown;
    private BitmapSource? incoming;
    internal int LayerCount=>disposed?0:worker.Read(()=>layers.Count);
    internal double Progress=>disposed?1:worker.Read(()=>gpu?.Progress??1);
    internal bool Attached=>!disposed&&layers.Count>0&&IsWindowVisible(parent)&&layers.TrueForAll(l=>IsWindowVisible(l.Handle)&&GetParent(l.Handle)==parent);
    internal bool AboveWallpaper
    {
        get
        {
            if(disposed)return false;
            if(wallpaperHost==IntPtr.Zero||GetParent(wallpaperHost)!=parent)return Attached;
            for(var child=GetWindow(parent,5);child!=IntPtr.Zero;child=GetWindow(child,2))
            {if(child==wallpaperHost)return false;if(layers.Exists(l=>l.Handle==child))return true;}
            return false;
        }
    }
    internal BitmapSource? CurrentFrame()=>disposed?null:worker.Read(()=>animating?CrossfadeView.Blend(shown!(),incoming!,gpu!.Progress):shown?.Invoke());
    internal BitmapSource CapturePaintedFrame()=>worker.Read(()=>gpu!.Capture(layers[0].Width,layers[0].Height).frame);
    internal Task<(BitmapSource frame,double amount)> CaptureAsync()=>worker.Run(()=>gpu!.Capture(layers[0].Width,layers[0].Height));
    internal Task<bool> BeginPresentationProbeAsync()=>worker.Run(()=>gpu!.BeginProbe());
    internal string ProbeDescription=>worker.Read(()=>gpu!.ProbeDescription);
    internal Task<GpuComposition.ProbeResult> ProbePresentationAsync()=>worker.Run(()=>gpu!.Probe());
    internal static Task<DesktopTransition?> TryCreateAsync(BitmapSource image)=>Task.Run(()=>TryCreate(image));
    public static DesktopTransition? TryCreate(BitmapSource image)
    {
        if(!SystemParameters.ClientAreaAnimation||SystemParameters.HighContrast)return null;
        var surface=new DesktopTransition();
        try{surface.worker.Invoke(()=>surface.Create(image));LastFailure="";return surface;}
        catch(Exception ex){LastFailure=ex.ToString();surface.Dispose();return null;}
    }
    private void Create(BitmapSource image)
    {
        var progman=FindWindow("Progman",null);
        if(progman==IntPtr.Zero)throw new InvalidOperationException("Explorer desktop is unavailable.");
        SendMessageTimeout(progman,0x052c,new IntPtr(0xd),new IntPtr(1),2,1000,out _);
        var icons=FindWindowEx(progman,IntPtr.Zero,"SHELLDLL_DefView",null);
        var raised=(GetWindowLongPtr(progman,-20).ToInt64()&0x00200000)!=0;
        var after=IntPtr.Zero;
        if(raised&&icons!=IntPtr.Zero)
        {
            parent=progman;after=icons;wallpaperHost=FindWindowEx(parent,IntPtr.Zero,"WorkerW",null);
            if(wallpaperHost!=IntPtr.Zero)SetWindowPos(wallpaperHost,new IntPtr(1),0,0,0,0,0x1|0x2|0x10);
        }
        else
        {
            EnumWindows((window,_)=>
            {
                if(FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null)==IntPtr.Zero)return true;
                parent=FindWindowEx(IntPtr.Zero,window,"WorkerW",null);return false;
            },IntPtr.Zero);
        }
        if(parent==IntPtr.Zero)throw new InvalidOperationException("Explorer wallpaper host was not found.");
        foreach(var screen in Forms.Screen.AllScreens)
        {
            var bounds=screen.Bounds;var point=new NativePoint{X=bounds.Left,Y=bounds.Top};MapWindowPoints(IntPtr.Zero,parent,ref point,1);
            var layer=new CompositionLayer(parent,bounds.Width,bounds.Height);layers.Add(layer);
            if(!SetWindowPos(layer.Handle,after,point.X,point.Y,bounds.Width,bounds.Height,0x10))throw new Win32Exception();
        }
        gpu=new GpuComposition(layers.Select(l=>l.Handle).ToArray(),layers.Select(l=>l.Width).ToArray(),layers.Select(l=>l.Height).ToArray());
        gpu.Initial(image);shown=()=>image;
        foreach(var layer in layers)
        {
            if(!SetWindowPos(layer.Handle,after,0,0,0,0,0x1|0x2|0x10|0x40))throw new Win32Exception();
            InvalidateRect(layer.Handle,IntPtr.Zero,false);UpdateWindow(layer.Handle);
        }
        if(!Attached||!AboveWallpaper)throw new InvalidOperationException("The GPU wallpaper layer is hidden by Explorer.");
    }
    public Task FadeAsync(BitmapSource image,CancellationToken cancellation)=>operation=worker.RunAsync(()=>FadeCoreAsync(image,cancellation));
    private async Task FadeCoreAsync(BitmapSource image,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(!Attached)throw new InvalidOperationException("Explorer changed its desktop layer.");
        KeepAboveWallpaper();gpu!.Begin(image);incoming=image;animating=true;
        try
        {
            // No CPU frame loop: the compositor samples opacity at display cadence.
            await Task.Delay(CrossfadeView.Duration,cancellation);
            cancellation.ThrowIfCancellationRequested();
            if(disposed)throw new OperationCanceledException(cancellation);
            if(!Attached)throw new InvalidOperationException("Explorer restarted.");
            KeepAboveWallpaper();gpu.Finish();shown=()=>image;incoming=null;animating=false;
        }
        catch(OperationCanceledException)
        {
            if(disposed)throw;
            var amount=gpu.Freeze();var previous=shown!;var next=image;
            // Only diagnostics need a CPU version of the held frame. Production
            // retargeting uses the GPU snapshot, with no full image readback.
            var held=new Lazy<BitmapSource>(()=>CrossfadeView.Blend(previous(),next,amount));
            shown=()=>held.Value;incoming=null;animating=false;throw;
        }
    }
    private void KeepAboveWallpaper()
    {
        if(wallpaperHost!=IntPtr.Zero&&GetParent(wallpaperHost)==parent&&!AboveWallpaper)
            SetWindowPos(wallpaperHost,new IntPtr(1),0,0,0,0,0x1|0x2|0x10);
    }
    internal Task HoldAsync(CancellationToken cancellation)=>operation=worker.RunAsync(async()=>
    {
        for(int i=0;i<10;i++)
        {
            cancellation.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException(cancellation);
            KeepAboveWallpaper();await Task.Delay(100,cancellation);
        }
    });
    public void Dispose()
    {
        if(disposed)return;
        worker.Invoke(()=>{disposed=true;gpu?.Dispose();gpu=null;foreach(var layer in layers)layer.Dispose();layers.Clear();shown=null;incoming=null;});
        _=StopWorkerAsync();
    }
    private async Task StopWorkerAsync(){try{await operation.ConfigureAwait(false);}catch{}finally{worker.Dispose();}}
    private sealed class CompositionLayer:Forms.NativeWindow,IDisposable
    {
        internal readonly int Width,Height;
        internal CompositionLayer(IntPtr parent,int width,int height)
        {
            Width=width;Height=height;
            CreateHandle(new Forms.CreateParams{Caption="Afterglow GPU wallpaper",Parent=parent,Width=width,Height=height,
                Style=0x40000000,ExStyle=0x08000000|0x80|0x20|0x00200000});
        }
        protected override void WndProc(ref Forms.Message message)
        {
            if(message.Msg==0x84){message.Result=new IntPtr(-1);return;}
            if(message.Msg==0x21){message.Result=new IntPtr(3);return;}
            if(message.Msg==0x14){message.Result=new IntPtr(1);return;}
            base.WndProc(ref message);
        }
        public void Dispose(){if(Handle!=IntPtr.Zero)DestroyHandle();}
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;}
    private delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindow(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string? title);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
    [DllImport("user32.dll")]private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")]private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]private static extern int MapWindowPoints(IntPtr from,IntPtr to,ref NativePoint point,uint count);
    [DllImport("user32.dll")]private static extern bool InvalidateRect(IntPtr window,IntPtr rect,bool erase);
    [DllImport("user32.dll")]private static extern bool UpdateWindow(IntPtr window);
}
