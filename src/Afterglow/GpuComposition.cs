using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Afterglow;

// The native helper is embedded in the portable executable. It uses only
// Windows graphics APIs and the statically linked C++ runtime.
internal sealed class GpuComposition : IDisposable
{
    private IntPtr engine;
    static GpuComposition()
    {
        NativeLibrary.SetDllImportResolver(typeof(GpuComposition).Assembly,(name,assembly,path)=>
        {
            if(name!="Afterglow.Composition")return IntPtr.Zero;
            using var source=assembly.GetManifestResourceStream("Afterglow.Composition.dll")??throw new FileNotFoundException("The graphics helper is missing.");
            using var memory=new MemoryStream();source.CopyTo(memory);var bytes=memory.ToArray();
            var folder=Path.Combine(Preferences.DirectoryPath,"native",Convert.ToHexString(SHA256.HashData(bytes)));
            Directory.CreateDirectory(folder);var file=Path.Combine(folder,"Afterglow.Composition.dll");
            if(!File.Exists(file)){var pending=file+"."+Guid.NewGuid().ToString("N");File.WriteAllBytes(pending,bytes);try{File.Move(pending,file,false);}catch(IOException)when(File.Exists(file)){File.Delete(pending);}}
            return NativeLibrary.Load(file);
        });
    }
    internal GpuComposition(IntPtr[] windows,int[] widths,int[] heights)=>Check(Create(windows,widths,heights,windows.Length,out engine));
    internal double Progress=>GetProgress(engine);
    internal void Initial(BitmapSource image)=>Check(InitialImage(engine,Pixels(image),image.PixelWidth,image.PixelHeight));
    internal void Begin(BitmapSource image)=>Check(BeginFade(engine,Pixels(image),image.PixelWidth,image.PixelHeight,CrossfadeView.Duration.TotalSeconds));
    internal void Finish()=>Check(FinishFade(engine));
    internal double Freeze(){Check(FreezeFade(engine,out var amount));return amount;}
    internal (BitmapSource frame,double amount) Capture(int width,int height)
    {
        var pixels=new byte[checked(width*height*4)];Check(CaptureFrame(engine,pixels,width,height,out var amount));
        var result=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,width*4);result.Freeze();return(result,amount);
    }
    internal bool BeginProbe(){Check(PrepareProbe(engine,out var available));return available!=0;}
    internal string ProbeDescription {get{var text=new StringBuilder(2048);GetProbeDescription(engine,text,text.Capacity);return text.ToString();}}
    internal ProbeResult Probe(){Check(RunProbe(engine,out var result));return result;}
    private static byte[] Pixels(BitmapSource image)
    {
        var bytes=new byte[checked(image.PixelWidth*image.PixelHeight*4)];
        BitmapSource converted=image.Format==PixelFormats.Pbgra32?image:new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);
        converted.CopyPixels(bytes,image.PixelWidth*4,0);return bytes;
    }
    private static void Check(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
    public void Dispose(){if(engine!=IntPtr.Zero){Destroy(engine);engine=IntPtr.Zero;}}
    [StructLayout(LayoutKind.Sequential)] internal struct ProbeResult
    {public int Frames,Errors,Reversals;public double MeanMs,P95Ms,MaxMs,RefreshHz;}
    [DllImport("Afterglow.Composition",EntryPoint="AG_Create",CallingConvention=CallingConvention.Cdecl)]private static extern int Create(IntPtr[] windows,int[] widths,int[] heights,int count,out IntPtr engine);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Initial",CallingConvention=CallingConvention.Cdecl)]private static extern int InitialImage(IntPtr engine,byte[] pixels,int width,int height);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Begin",CallingConvention=CallingConvention.Cdecl)]private static extern int BeginFade(IntPtr engine,byte[] pixels,int width,int height,double duration);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Finish",CallingConvention=CallingConvention.Cdecl)]private static extern int FinishFade(IntPtr engine);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Freeze",CallingConvention=CallingConvention.Cdecl)]private static extern int FreezeFade(IntPtr engine,out double amount);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Progress",CallingConvention=CallingConvention.Cdecl)]private static extern double GetProgress(IntPtr engine);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Capture",CallingConvention=CallingConvention.Cdecl)]private static extern int CaptureFrame(IntPtr engine,[Out] byte[] bytes,int width,int height,out double amount);
    [DllImport("Afterglow.Composition",EntryPoint="AG_ProbeBegin",CallingConvention=CallingConvention.Cdecl)]private static extern int PrepareProbe(IntPtr engine,out int available);
    [DllImport("Afterglow.Composition",EntryPoint="AG_ProbeRun",CallingConvention=CallingConvention.Cdecl)]private static extern int RunProbe(IntPtr engine,out ProbeResult result);
    [DllImport("Afterglow.Composition",EntryPoint="AG_Destroy",CallingConvention=CallingConvention.Cdecl)]private static extern void Destroy(IntPtr engine);
    [DllImport("Afterglow.Composition",EntryPoint="AG_ProbeDescription",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern int GetProbeDescription(IntPtr engine,StringBuilder text,int capacity);
}
