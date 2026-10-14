using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace Afterglow;

// No-redirection composition windows avoid invisible layered WPF children.
internal sealed class DesktopLyrics : IDisposable
{
    private readonly RenderWorker worker = new("Afterglow desktop lyrics");
    private readonly List<Layer> layers = new();
    private IntPtr parent, after, wallpaperHost;
    private bool disposed;
    private int lastIndex = int.MinValue;
    private LyricLine[]? lastLines;
    private Task pending = Task.CompletedTask;
    private int revision;
    internal bool Attached => !disposed && worker.Read(() => layers.Count > 0 && layers.TrueForAll(l => IsWindowVisible(l.Handle) && GetParent(l.Handle) == parent));
    internal bool ClickThrough => !disposed && worker.Read(() => layers.Count > 0 && layers.TrueForAll(l => (GetWindowLongPtr(l.Handle, -20).ToInt64() & 0x08000020) == 0x08000020));
    internal bool AboveWallpaper => !disposed && worker.Read(() => layers.Count > 0 && layers.TrueForAll(VisibleAboveWallpaper));
    private bool VisibleAboveWallpaper(Layer layer)
    {
        if (wallpaperHost == IntPtr.Zero) return true;
        for (var child = GetWindow(parent, 5); child != IntPtr.Zero; child = GetWindow(child, 2)) { if (child == layer.Handle) return true; if (child == wallpaperHost) return false; }
        return false;
    }
    internal DesktopLyrics() { try { worker.Invoke(Create); } catch { Dispose(); throw; } }
    private void Create()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) throw new InvalidOperationException("Explorer desktop is unavailable.");
        SendMessageTimeout(progman, 0x052c, new IntPtr(0xd), new IntPtr(1), 2, 1000, out _);
        var icons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (icons != IntPtr.Zero && (GetWindowLongPtr(progman, -20).ToInt64() & 0x00200000) != 0)
        { parent = progman; after = icons; wallpaperHost = FindWindowEx(parent, IntPtr.Zero, "WorkerW", null); }
        else EnumWindows((window, _) => { if (FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) return true; parent = FindWindowEx(IntPtr.Zero, window, "WorkerW", null); return false; }, IntPtr.Zero);
        if (parent == IntPtr.Zero) throw new InvalidOperationException("Explorer lyrics host was not found.");
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var b = screen.Bounds; var landscape = b.Width >= b.Height;
            var width = (int)(b.Width * (landscape ? .38 : .76)); var height = (int)(b.Height * (landscape ? .72 : .40));
            var point = new NativePoint { X = b.Left + (int)(b.Width * (landscape ? .53 : .12)), Y = b.Top + (int)(b.Height * (landscape ? .14 : .51)) }; MapWindowPoints(IntPtr.Zero, parent, ref point, 1);
            var layer = new Layer(parent, width, height); layers.Add(layer);
            if (!SetWindowPos(layer.Handle, after, point.X, point.Y, width, height, 0x10 | 0x40)) throw new Win32Exception();
        }
        if (wallpaperHost != IntPtr.Zero) SetWindowPos(wallpaperHost, new IntPtr(1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
    }
    internal void KeepVisible()
    {
        if (disposed) return;
        worker.Invoke(() => { foreach (var layer in layers) SetWindowPos(layer.Handle, after, 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x40); });
    }
    internal void Update(LyricLine[] lines, int index, bool animate)
    {
        if (disposed || (ReferenceEquals(lines, lastLines) && index == lastIndex)) return;
        var reset = !ReferenceEquals(lines, lastLines);
        lastLines = lines; lastIndex = index;
        var request=++revision;
        pending = worker.RunAsync(async () =>
        {
            if (disposed || request!=revision) return;
            foreach (var layer in layers)
            {
                var scene = LyricLayout.Create(lines, index, layer.Width, layer.Height);
                layer.Animating = layer.HasFrame && !reset && animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
                layer.Gpu.Lyrics(scene, layer.Width, layer.Animating, reset);
                layer.HasFrame=true;
            }
            await Task.Delay(LyricsView.TransitionDuration + TimeSpan.FromMilliseconds(20));
            if(disposed||request!=revision)return;
            foreach(var layer in layers)if(layer.Animating){layer.Gpu.Finish();layer.Animating=false;}
        });
    }
    internal async Task WaitForFrameAsync() => await pending;
    internal string Failure => pending.Exception?.GetBaseException().Message ?? "";
    internal BitmapSource CaptureText() => worker.Read(() => layers[0].Gpu.Capture(layers[0].Width, layers[0].Height).frame);
    internal (double Y, double Opacity, int Count, int Uploads, int Commits) LyricState(int id) => worker.Read(() => layers[0].Gpu.LyricState(id));
    public void Dispose()
    {
        if (disposed) return;
        worker.Invoke(() => { disposed = true; foreach (var layer in layers) layer.Dispose(); layers.Clear(); }); worker.Dispose();
    }
    private sealed class Layer : Forms.NativeWindow, IDisposable
    {
        internal readonly int Width, Height;
        internal readonly GpuComposition Gpu;
        internal bool HasFrame;
        internal bool Animating;
        internal Layer(IntPtr parent, int width, int height)
        {
            Width = width; Height = height;
            CreateHandle(new Forms.CreateParams { Caption = "Afterglow desktop lyrics", Parent = parent, Width = width, Height = height, Style = 0x40000000, ExStyle = 0x08000000 | 0x80 | 0x20 | 0x00200000 });
            try { Gpu = new GpuComposition(new[] { Handle }, new[] { width }, new[] { height }); } catch { DestroyHandle(); throw; }
        }
        protected override void WndProc(ref Forms.Message message)
        { if (message.Msg == 0x84) { message.Result = new IntPtr(-1); return; } if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; } if (message.Msg == 0x14) { message.Result = new IntPtr(1); return; } base.WndProc(ref message); }
        public void Dispose() { Gpu.Dispose(); if (Handle != IntPtr.Zero) DestroyHandle(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string name, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string? title);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint msg, IntPtr wp, IntPtr lp, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref NativePoint point, uint count);
}
