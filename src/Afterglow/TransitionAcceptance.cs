using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace Afterglow;

internal static class TransitionAcceptance
{
    // Explicitly requested test: briefly animates fixtures on the desktop and
    // restores the captured desktop. It never writes the user's preferences.
    public static async Task RunAsync(string reportPath)
    {
        var folder=Path.GetDirectoryName(reportPath)!;Directory.CreateDirectory(folder);
        var report=new StringBuilder();int checks=0;
        void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);report.AppendLine("PASS: "+name);checks++;}
        using var desktop=new NativeWallpaper();var snapshot=desktop.Capture(true,Path.Combine(Preferences.WallpaperDirectory,"transition-test-originals"));
        DesktopTransition? surface=null;
        var settings=new Preferences{Preset="Ambient",Blur=67,Darkness=4};
        var art=Artwork.Demo();
        var track=new Track{Title="A softer change",Artist="Afterglow",Album="Smooth transitions"};
        var first=WallpaperRenderer.Render(art,track,settings,1920,1080);
        BitmapSource second;
        var livePath=Path.Combine(folder,"live-track.json");
        if(File.Exists(livePath))
        {
            var live=System.Text.Json.JsonSerializer.Deserialize<Track>(File.ReadAllText(livePath),Preferences.Json)!;
            var liveArt=await Artwork.DownloadAsync(live.Artwork,CancellationToken.None);
            second=WallpaperRenderer.Render(liveArt,live,settings,1920,1080);
        }
        else {settings.Preset="Gallery";second=WallpaperRenderer.Render(art,track with{Title="The next song"},settings,1920,1080);}
        var initialPath=Path.Combine(folder,"transition-start.png");var finalPath=Path.Combine(folder,"transition-end.png");
        WallpaperRenderer.Save(first,initialPath);WallpaperRenderer.Save(second,finalPath);
        try
        {
            desktop.Apply(initialPath);
            surface=await DesktopTransition.TryCreateAsync(first);
            Check(surface is not null&&surface.Attached&&surface.LayerCount==System.Windows.Forms.Screen.AllScreens.Length,"Animation surfaces attach behind Explorer icons on every active monitor"+(surface is null?": "+DesktopTransition.LastFailure:""));
            using var interrupted=new CancellationTokenSource();
            var animation=surface!.FadeAsync(second,interrupted.Token);
            await Task.Delay(700);
            Check(surface.Progress>.05&&surface.Progress<.95,"Desktop layer progresses through a genuine intermediate opacity");
            Check(surface.AboveWallpaper,"GPU visuals remain above Explorer's opaque wallpaper host");
            var painted=surface.CapturePaintedFrame();
            WallpaperRenderer.Save(painted,Path.Combine(folder,"desktop-painted-midpoint.png"));
            var actual=new byte[4];var a=new byte[4];var b=new byte[4];
            var point=new System.Windows.Int32Rect(300,500,1,1);
            painted.CopyPixels(point,actual,4,0);first.CopyPixels(point,a,4,0);second.CopyPixels(point,b,4,0);
            var mix=surface.Progress;
            Check(Enumerable.Range(0,3).All(i=>Math.Abs(actual[i]-(a[i]*(1-mix)+b[i]*mix))<18)&&actual[3]==255,
                "GPU texture readback contains the expected mixed colors");
            WallpaperRenderer.Save(surface.CurrentFrame()!,Path.Combine(folder,"transition-midpoint.png"));
            interrupted.Cancel();try{await animation;}catch(OperationCanceledException){}
            var held=surface.CurrentFrame();
            Check(held is not null&&!ReferenceEquals(held,first)&&!ReferenceEquals(held,second)&&surface.Attached,"Interrupted transition holds the visible blend for rapid retargeting");
            var third=WallpaperRenderer.Render(art,track with{Title="No abrupt jumps"},settings,1920,1080);
            var continued=surface.FadeAsync(third,CancellationToken.None);
            var restart=await surface.CaptureAsync();var restartPixel=new byte[4];var heldPixel=new byte[4];
            restart.frame.CopyPixels(point,restartPixel,4,0);held!.CopyPixels(point,heldPixel,4,0);
            Check(restart.amount<.05&&Enumerable.Range(0,3).All(i=>Math.Abs(restartPixel[i]-heldPixel[i])<4),"Next transition starts from the held composite");
            await continued;
            Check(ReferenceEquals(surface.CurrentFrame(),third),"Completed transition reaches the exact final-resolution bitmap");
            var thirdPath=Path.Combine(folder,"transition-retargeted.png");WallpaperRenderer.Save(third,thirdPath);
            desktop.Apply(thirdPath);await surface.HoldAsync(CancellationToken.None);
            Check(desktop.Owns(thirdPath),"Final PNG committed to every Windows monitor before the layer closes");
            // Exercise the production workers with native 4K sources while a
            // UI dispatcher heartbeat measures responsiveness, not just FPS.
            BitmapSource Solid(byte r,byte g,byte b)
            {
                var bytes=new byte[3840*2160*4];
                for(int i=0;i<bytes.Length;i+=4){bytes[i]=b;bytes[i+1]=g;bytes[i+2]=r;bytes[i+3]=255;}
                var result=BitmapSource.Create(3840,2160,96,96,PixelFormats.Pbgra32,null,bytes,3840*4);result.Freeze();return result;
            }
            var warm=await RenderWorker.Wallpapers.Run(()=>Solid(210,35,70));
            var cool=await RenderWorker.Wallpapers.Run(()=>Solid(40,110,225));
            await surface.FadeAsync(warm,CancellationToken.None);
            await Task.Delay(250);
            var canProbe=await surface.BeginPresentationProbeAsync();
            report.AppendLine(surface.ProbeDescription);
            var heartbeat=Stopwatch.StartNew();double previousTick=heartbeat.Elapsed.TotalMilliseconds,maxGap=0;int ticks=0;
            var timer=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>{var now=heartbeat.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-previousTick);previousTick=now;ticks++;};
            timer.Start();
            var dissolve=surface.FadeAsync(cool,CancellationToken.None);
            Task<GpuComposition.ProbeResult>? probe=canProbe?surface.ProbePresentationAsync():null;
            var rendering=RenderWorker.Wallpapers.Run(()=>
            {
                for(int i=0;i<4;i++)
                {
                    var options=new Preferences{Blur=60+i,Preset="Gallery"};
                    var bitmap=WallpaperRenderer.Render(art,track,options,3840,2160);
                    WallpaperRenderer.Save(bitmap,Path.Combine(folder,"responsive-render.png"));
                }
                return true;
            });
            bool clean=true,monotonic=true;int samples=0;double lastBlue=70;
            while(probe is null&&!dissolve.IsCompleted)
            {
                var sample=await surface.CaptureAsync();var pixel=new byte[4];sample.frame.CopyPixels(new System.Windows.Int32Rect(100,100,1,1),pixel,4,0);
                var expected=new[]{70+(225-70)*sample.amount,35+(110-35)*sample.amount,210+(40-210)*sample.amount};
                clean&=pixel[3]==255&&Enumerable.Range(0,3).All(i=>Math.Abs(pixel[i]-expected[i])<=3);
                monotonic&=pixel[0]>=lastBlue-1;lastBlue=pixel[0];samples++;
                await Task.Delay(55);
            }
            await dissolve;await rendering;timer.Stop();
            if(probe is not null)
            {
                var presentation=await probe;
                report.AppendLine($"Displayed desktop: {presentation.Frames} frames; {presentation.RefreshHz:0.0} Hz; mean {presentation.MeanMs:0.0} ms, p95 {presentation.P95Ms:0.0} ms, max {presentation.MaxMs:0.0} ms; {presentation.Errors} color errors; {presentation.Reversals} reversals.");
                Check(presentation.Frames>=Math.Max(40,CrossfadeView.Duration.TotalSeconds*presentation.RefreshHz*.75)&&presentation.Errors==0&&presentation.Reversals==0,"Actual displayed wallpaper progresses smoothly while the app render worker is occupied");
                Check(presentation.P95Ms<=1000/Math.Max(30,presentation.RefreshHz)*2.5,"95th-percentile displayed frame interval remains below 2.5 display refresh intervals");
            }
            else
            {
                Check(clean&&monotonic&&samples>=10,"GPU texture samples remain opaque and monotonic during concurrent 4K renders");
                report.AppendLine("Desktop presentation probe unavailable: no exposed fixture wallpaper or capture access; GPU readback was checked instead.");
            }
            Check(ticks>=20&&maxGap<200,"UI dispatcher remains responsive during native three-monitor fade and four 4K PNG renders");
            report.AppendLine($"Measured: {samples} GPU readback samples; {ticks} UI heartbeats; maximum UI heartbeat gap {maxGap:0.0} ms.");
            bool rapidContinuous=true;
            for(int skip=0;skip<6;skip++)
            {
                using var skipping=new CancellationTokenSource();
                var changing=surface.FadeAsync(skip%2==0?warm:cool,skipping.Token);
                await Task.Delay(80);skipping.Cancel();try{await changing;}catch(OperationCanceledException){}
                var heldGpu=await surface.CaptureAsync();var previousColor=new byte[4];heldGpu.frame.CopyPixels(new System.Windows.Int32Rect(100,100,1,1),previousColor,4,0);
                using var restarting=new CancellationTokenSource();
                var resumed=surface.FadeAsync(skip%2==0?cool:warm,restarting.Token);
                var firstGpu=await surface.CaptureAsync();var firstColor=new byte[4];firstGpu.frame.CopyPixels(new System.Windows.Int32Rect(100,100,1,1),firstColor,4,0);
                rapidContinuous&=Enumerable.Range(0,3).All(i=>Math.Abs(previousColor[i]-firstColor[i])<=3)&&firstColor[3]==255;
                restarting.Cancel();try{await resumed;}catch(OperationCanceledException){}
            }
            Check(rapidContinuous,"Six rapid skips preserve the held GPU colors without jumping to a stale song");
            Check(surface.Attached&&surface.AboveWallpaper,"Final surface remains attached between songs with no Explorer handoff");
            surface.Dispose();Check(!surface.Attached&&surface.LayerCount==0,"Temporary animation surfaces fully released after commit");surface=null;
        }
        finally
        {
            surface?.Dispose();desktop.Restore(snapshot);
            var restored=desktop.Capture(false);
            Check(restored.Position==snapshot.Position&&restored.Slideshow.SequenceEqual(snapshot.Slideshow)&&restored.SlideshowTick==snapshot.SlideshowTick&&
                (snapshot.Slideshow.Length>0||restored.Monitors.All(m=>snapshot.Monitors.Any(o=>o.Monitor==m.Monitor&&(o.Path==m.Path||o.Backup==m.Path)))),
                "Original monitor wallpaper/slideshow restored after animation test");
            report.AppendLine($"\n{checks} transition checks passed.");File.WriteAllText(reportPath,report.ToString());
        }
    }
}
