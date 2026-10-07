using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Afterglow;

public sealed class WallpaperSnapshot
{
    public List<MonitorWallpaper> Monitors { get; set; } = new();
    public int Position { get; set; }
    public uint BackgroundColor { get; set; }
    public bool Enabled { get; set; }
    public string[] Slideshow { get; set; } = Array.Empty<string>();
    public uint SlideshowOptions { get; set; }
    public uint SlideshowTick { get; set; }
}
public sealed record MonitorWallpaper(string Monitor, string Path, string Backup);

// COM declarations follow the Windows SDK vtable order; HRESULTs remain explicit.
[ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    [PreserveSig] int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitor, [MarshalAs(UnmanagedType.LPWStr)] string path);
    [PreserveSig] int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitor, out IntPtr path);
    [PreserveSig] int GetMonitorDevicePathAt(uint index, out IntPtr monitor);
    [PreserveSig] int GetMonitorDevicePathCount(out uint count);
    [PreserveSig] int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor, IntPtr rect);
    [PreserveSig] int SetBackgroundColor(uint color);
    [PreserveSig] int GetBackgroundColor(out uint color);
    [PreserveSig] int SetPosition(int position);
    [PreserveSig] int GetPosition(out int position);
    [PreserveSig] int SetSlideshow(IShellItemArray items);
    [PreserveSig] int GetSlideshow(out IShellItemArray? items);
    [PreserveSig] int SetSlideshowOptions(uint options, uint tick);
    [PreserveSig] int GetSlideshowOptions(out uint options, out uint tick);
    [PreserveSig] int AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitor, int direction);
    [PreserveSig] int GetStatus(out uint state);
    [PreserveSig] int Enable([MarshalAs(UnmanagedType.Bool)] bool enabled);
}
[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler(IntPtr context, in Guid handler, in Guid iid, out IntPtr result);
    void GetPropertyStore(int flags, in Guid iid, out IntPtr result);
    void GetPropertyDescriptionList(IntPtr key, in Guid iid, out IntPtr result);
    void GetAttributes(int flags, uint mask, out uint attributes);
    void GetCount(out uint count);
    void GetItemAt(uint index, out IShellItem item);
}
[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr context, in Guid handler, in Guid iid, out IntPtr result);
    void GetParent(out IShellItem parent);
    void GetDisplayName(uint format, out IntPtr name);
}

internal sealed class NativeWallpaper : IDisposable
{
    private readonly IDesktopWallpaper api = (IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll")]
    private static extern int SHCreateShellItemArrayFromIDLists(uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IntPtr[] items, out IShellItemArray result);
    private static void Check(int hr) { if(hr<0) Marshal.ThrowExceptionForHR(hr); }
    private static string TakeString(IntPtr value) { try { return Marshal.PtrToStringUni(value)??""; } finally { Marshal.FreeCoTaskMem(value); } }
    public string Get(string? monitor=null) { Check(api.GetWallpaper(monitor,out var value)); return TakeString(value); }
    public WallpaperSnapshot Capture(bool backup, string? backupFolder=null)
    {
        var snapshot=new WallpaperSnapshot();
        Check(api.GetMonitorDevicePathCount(out var count));
        for(uint i=0;i<count;i++)
        {
            Check(api.GetMonitorDevicePathAt(i,out var value)); var monitor=TakeString(value); var path=Get(monitor); var saved="";
            if(backup && path!="")
            {
                if(!File.Exists(path)) throw new IOException("The current wallpaper could not be backed up: "+path);
                var folder=backupFolder??System.IO.Path.Combine(Preferences.WallpaperDirectory,"originals");
                Directory.CreateDirectory(folder);
                saved=System.IO.Path.Combine(folder,"monitor-"+i+System.IO.Path.GetExtension(path));
                File.Copy(path,saved,true);
            }
            snapshot.Monitors.Add(new(monitor,path,saved));
        }
        Check(api.GetPosition(out var position)); snapshot.Position=position;
        Check(api.GetBackgroundColor(out var color)); snapshot.BackgroundColor=color;
        Check(api.GetStatus(out var status)); snapshot.Enabled=(status&1)!=0;
        if((status&2)!=0)
        {
            Check(api.GetSlideshow(out var items));
            if(items is null) throw new IOException("Windows did not supply the slideshow configuration.");
            try
            {
                items.GetCount(out var n); var paths=new List<string>();
                for(uint i=0;i<n;i++)
                {
                    items.GetItemAt(i,out var item);
                    try { item.GetDisplayName(0x80028000,out var name); paths.Add(TakeString(name)); }
                    finally { Marshal.ReleaseComObject(item); }
                }
                snapshot.Slideshow=paths.ToArray();
            }
            finally { Marshal.ReleaseComObject(items); }
            Check(api.GetSlideshowOptions(out var options,out var tick)); snapshot.SlideshowOptions=options; snapshot.SlideshowTick=tick;
        }
        return snapshot;
    }
    public void Apply(string path)
    {
        var hr=api.SetWallpaper(null,path); if(hr<0) throw new IOException("Windows could not set the image ("+hr.ToString("X8")+").",Marshal.GetExceptionForHR(hr));
        Check(api.SetPosition(4));
    }
    public void Restore(WallpaperSnapshot snapshot)
    {
        Check(api.SetPosition(snapshot.Position)); Check(api.SetBackgroundColor(snapshot.BackgroundColor));
        foreach(var m in snapshot.Monitors)
        {
            var cached=System.IO.Path.GetFileName(m.Path).StartsWith("Transcoded",StringComparison.OrdinalIgnoreCase);
            var path=!cached && File.Exists(m.Path)?m.Path:m.Backup;
            if(m.Path!="" && !File.Exists(path)) throw new IOException("The original wallpaper and its backup are unavailable.");
            Check(api.SetWallpaper(m.Monitor,path));
        }
        if(snapshot.Slideshow.Length>0)
        {
            var ids=new List<IntPtr>(); IShellItemArray? items=null;
            try
            {
                foreach(var path in snapshot.Slideshow) { Check(SHParseDisplayName(path,IntPtr.Zero,out var id,0,out _)); ids.Add(id); }
                Check(SHCreateShellItemArrayFromIDLists((uint)ids.Count,ids.ToArray(),out items));
                Check(api.SetSlideshowOptions(snapshot.SlideshowOptions,snapshot.SlideshowTick)); Check(api.SetSlideshow(items));
            }
            finally { if(items is not null) Marshal.ReleaseComObject(items); foreach(var id in ids) Marshal.FreeCoTaskMem(id); }
        }
        Check(api.Enable(snapshot.Enabled));
    }
    public bool Owns(string path)
    {
        var current=Capture(false);
        return current.Slideshow.Length==0 && current.Monitors.Count>0 && current.Monitors.TrueForAll(m=>string.Equals(m.Path,path,StringComparison.OrdinalIgnoreCase));
    }
    public void Dispose()=>Marshal.ReleaseComObject(api);
}
