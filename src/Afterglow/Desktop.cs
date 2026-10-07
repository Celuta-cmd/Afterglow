using System;
using System.IO;
using Microsoft.Win32;

namespace Afterglow;

public static class Desktop
{
    public static string CurrentWallpaper()
    {
        using var desktop=new NativeWallpaper();return desktop.Get();
    }
    public static void Apply(string path,Preferences p)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Wallpaper has not been rendered.",path);
        using var desktop=new NativeWallpaper();
        if(!p.OwnsWallpaper || p.WallpaperSnapshot is null || !IsOwned(desktop,p))
        {
            p.WallpaperSnapshot=desktop.Capture(true);
            p.OwnsWallpaper=true;p.Save();
        }
        desktop.Apply(path);
        p.AppliedWallpaper=path;p.Save();
    }
    public static string Restore(Preferences p)
    {
        if(!p.OwnsWallpaper || p.WallpaperSnapshot is null)return "No Afterglow wallpaper is active.";
        using var desktop=new NativeWallpaper();
        // Avoid replacing a wallpaper that the user changed outside Afterglow.
        if(!IsOwned(desktop,p)) {p.OwnsWallpaper=false;p.Save();return "Your wallpaper changed outside Afterglow. It was left in place.";}
        desktop.Restore(p.WallpaperSnapshot);p.OwnsWallpaper=false;p.Save();return "Previous monitor wallpapers and slideshow settings restored.";
    }
    internal static bool IsOwned(NativeWallpaper desktop,Preferences p)
    {
        if(!string.IsNullOrEmpty(p.AppliedWallpaper))return desktop.Owns(p.AppliedWallpaper);
        // Preserve the original backup when upgrading from the JPEG wallpaper release.
        return desktop.Owns(Preferences.WallpaperPath)||desktop.Owns(Path.Combine(Preferences.WallpaperDirectory,"wallpaper.jpg"));
    }
    public static bool StartWithWindows
    {
        get { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return key?.GetValue("Afterglow") is not null; }
        set { using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(value)key.SetValue("Afterglow",$"\"{Environment.ProcessPath}\" --tray");else key.DeleteValue("Afterglow",false); }
    }
}
