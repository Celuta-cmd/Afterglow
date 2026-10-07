using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Afterglow;
public static class WallpaperAcceptance
{
    // Explicitly opt-in: temporarily applies a test image, then restores the desktop.
    public static void Run(string reportPath)
    {
        var report=new StringBuilder();using var desktop=new NativeWallpaper();var snapshot=desktop.Capture(true,Path.Combine(Preferences.WallpaperDirectory,"test-originals"));
        var path=Path.Combine(Path.GetDirectoryName(reportPath)!,"acceptance.png");
        WallpaperRenderer.SaveDesktop(Artwork.Demo(),path);
        try
        {
            desktop.Apply(path);
            bool applied=false;
            for(int attempt=0;attempt<25&&!applied;attempt++)
            {
                using var readback=new NativeWallpaper();applied=readback.Owns(path);
                if(!applied)System.Threading.Thread.Sleep(200);
            }
            if(!applied)throw new Exception("Windows did not apply the wallpaper to every monitor.");
            report.AppendLine("PASS: Native wallpaper applied and read back on every monitor.");
            if(!snapshot.Monitors.All(m=>m.Path==""||File.Exists(m.Backup)))throw new Exception("Original backup is missing.");
            report.AppendLine("PASS: Original monitor images backed up.");
        }
        finally
        {
            desktop.Restore(snapshot);
            bool matched=false;
            for(int attempt=0;attempt<25&&!matched;attempt++)
            {
                using var readback=new NativeWallpaper();var restored=readback.Capture(false);
                var slideshow=restored.Slideshow.SequenceEqual(snapshot.Slideshow);
                var images=snapshot.Slideshow.Length>0 || restored.Monitors.All(m=>snapshot.Monitors.Any(o=>o.Monitor==m.Monitor && (o.Path==m.Path||o.Backup==m.Path)));
                matched=slideshow&&images&&restored.Position==snapshot.Position&&restored.Enabled==snapshot.Enabled&&restored.SlideshowOptions==snapshot.SlideshowOptions&&restored.SlideshowTick==snapshot.SlideshowTick;
                if(!matched)System.Threading.Thread.Sleep(200);
            }
            if(!matched)
                throw new Exception("Restoration did not match the saved desktop configuration.");
            report.AppendLine("PASS: Monitor wallpapers / slideshow, position, shuffle and timing restored.");
            File.WriteAllText(reportPath,report.ToString());
        }
    }
}
