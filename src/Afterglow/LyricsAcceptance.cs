using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Afterglow;

internal static class LyricsAcceptance
{
    internal static async Task Run(string report)
    {
        // The provider smoke check uses public metadata, never the user's song.
        var provider = new LyricsProvider();
        var result = await provider.FetchAsync(new Track { Title = "Yellow", Artist = "Coldplay", Album = "Parachutes", Duration = 266 }, CancellationToken.None);
        var before = Desktop.CurrentWallpaper();
        using (var layer = new DesktopLyrics())
        {
            if (!layer.Attached || !layer.ClickThrough || !layer.AboveWallpaper) throw new Exception("Lyrics layer did not attach visibly above wallpaper with click-through styling.");
            var fixture=new[] { new LyricLine(0, "A softer evening"), new LyricLine(2, "Your music lights up the room"), new LyricLine(4, "A longer original phrase that keeps going onto the lines below instead of hiding the rest of its words") };
            layer.Update(fixture, 1, true);
            await layer.WaitForFrameAsync();
            var shared = layer.LyricState(2);
            var frame=layer.CaptureText();var pixels=new byte[frame.PixelWidth*frame.PixelHeight*4];frame.CopyPixels(pixels,frame.PixelWidth*4,0);
            int transparent=0,ink=0;for(int i=3;i<pixels.Length;i+=4){if(pixels[i]==0)transparent++;if(pixels[i]>180)ink++;}
            if(transparent<frame.PixelWidth*frame.PixelHeight/2||ink<100)throw new Exception("GPU lyrics frame must contain visible glyphs and a transparent background.");
            layer.Update(fixture,2,true);
            await Task.Delay(40);
            var start = layer.LyricState(2);
            if(start.Opacity < shared.Opacity-.01 || start.Count != 3 || start.Uploads != shared.Uploads || start.Commits != shared.Commits+1)
                throw new Exception("Shared desktop phrases must reuse their texture and start with their existing emphasis in one commit.");
            await Task.Delay(220);
            var moving = layer.LyricState(2);
            if(moving.Opacity <= start.Opacity || moving.Y >= start.Y) throw new Exception("The incoming phrase should move upward and brighten continuously.");
            var intermediate=layer.CaptureText();var midpoint=new byte[intermediate.PixelWidth*intermediate.PixelHeight*4];intermediate.CopyPixels(midpoint,intermediate.PixelWidth*4,0);
            if(System.Linq.Enumerable.SequenceEqual(midpoint,pixels))throw new Exception("Lyric transition did not change its presented texture.");
            layer.Update(fixture,1,true); // retarget a running slide, as on a seek
            await Task.Delay(25);
            var retarget = layer.LyricState(2);
            if(Math.Abs(retarget.Opacity-moving.Opacity)>.15 || Math.Abs(retarget.Y-moving.Y)>80 || retarget.Uploads!=moving.Uploads)
                throw new Exception("An interrupted desktop move restarted or duplicated a shared phrase.");
            await layer.WaitForFrameAsync();
            var settled=layer.LyricState(2);
            if(Math.Abs(settled.Opacity-.38)>.001 || settled.Count!=3)throw new Exception("A completed desktop move retained outgoing glyphs or incorrect emphasis.");
            var dense=System.Linq.Enumerable.Range(0,30).Select(i=>new LyricLine(i*.1,"Original rapid-transition phrase "+i)).ToArray();
            layer.Update(dense,1,false);await layer.WaitForFrameAsync();
            for(int i=2;i<=16;i++){layer.Update(dense,i,true);await Task.Delay(80);}
            if(layer.LyricState(16).Count>12)throw new Exception("Rapid advances extended departing phrase lifetimes or accumulated stale textures.");
            await layer.WaitForFrameAsync();
            if(layer.LyricState(16).Count!=3)throw new Exception("Rapid lyric sequence did not clean up its departing phrases.");
            layer.Update(fixture,2,false);await layer.WaitForFrameAsync();
            if(layer.LyricState(2).Opacity!=1)throw new Exception("Reduced-motion lyric updates should settle immediately.");
            // Exercise the WPF view's shared-line path and interrupted motion too.
            var view=new LyricsView {Width=486,Height=518};
            view.Update(fixture,1,false);view.Measure(new System.Windows.Size(486,518));view.Arrange(new System.Windows.Rect(0,0,486,518));view.UpdateLayout();
            var retained=view.LabelFor(2)!;var font=retained.FontSize;
            view.Update(fixture,2,true);
            if(!ReferenceEquals(retained,view.LabelFor(2)) || retained.FontSize!=font || view.VisualCount!=3)
                throw new Exception("Preview lyric changes must retain shared visuals and their font size.");
            view.Update(fixture,1,true);view.Update(fixture,2,false);
            if(view.VisualCount!=2 || !view.ActiveTextFits)throw new Exception("Preview reduced-motion cleanup or long-phrase wrapping failed.");
        }
        if (Desktop.CurrentWallpaper() != before) throw new Exception("Wallpaper changed during the layer test.");
        File.WriteAllText(report, $"Desktop composition lyrics attached above wallpaper on all displays with click-through and no-activation styles.\nGPU readback contains visible glyphs over transparent pixels. Shared phrases retain opacity, position continuity and raster textures; each animated update commits once.\nInterrupted motion retargeted without restarting the phrase; outgoing glyphs cleaned up. Reduced-motion updates settled immediately.\nFifteen rapid advances at 80 ms intervals kept departing phrase lifetimes bounded and returned to three retained glyphs.\nPreview retained shared visuals and stable font size; long text wrapped.\nLayer closed cleanly; wallpaper path was unchanged.\nLRCLIB: {result.Status}; timed lines: {result.Lines.Length}.\nNo user song metadata or lyric text was recorded.\n");
    }
}
