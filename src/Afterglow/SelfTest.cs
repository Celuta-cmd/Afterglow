using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Afterglow;

public static class SelfTest
{
    public static async Task Run(string reportPath)
    {
        var report=new StringBuilder();int tests=0;
        void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);report.AppendLine("PASS: "+name);tests++;}
        var p=new Preferences();var demo=Artwork.Demo();
        var track=new Track{Title="A very long album title to check the safe area and wrapping of text in the wallpaper",Artist="Afterglow test artist",Source="tab-1",Url="https://music.youtube.com/watch?v=test",Playing=true,Duration=240,Position=30};
        foreach(var preset in new[]{"Ambient","Immersive","Gallery","Backdrop"})
        {
            p.Preset=preset;var image=WallpaperRenderer.Render(demo,track,p,1920,1080);Check(image.PixelWidth==1920&&image.PixelHeight==1080,preset+" wallpaper dimensions");
            var pixels=new byte[1920*4];image.CopyPixels(new System.Windows.Int32Rect(0,540,1920,1),pixels,1920*4,0);
            Check(pixels[3]==255&&pixels[1919*4+3]==255,preset+" is opaque");
            WallpaperRenderer.Save(image,Path.Combine(Path.GetDirectoryName(reportPath)!,preset+".png"));
        }
        foreach(var dimensions in new[]{(1080,1920),(3440,1440)})
        {
            p.Preset="Ambient";p.CoverSize=80;
            var image=WallpaperRenderer.Render(demo,track,p,dimensions.Item1,dimensions.Item2);
            Check(image.PixelWidth==dimensions.Item1&&image.PixelHeight==dimensions.Item2,"Aspect ratio preserved: "+dimensions);
            WallpaperRenderer.Save(image,Path.Combine(Path.GetDirectoryName(reportPath)!,"aspect-"+dimensions.Item1+".png"));
        }
        p.CoverSize=54;
        // A one-pixel source pattern must survive a 1:1 cover at 4K. A 900p
        // intermediate destroys these details even when the final file is 4K.
        var stripes=new byte[960*960*4];
        for(int y=0;y<960;y++)for(int x=0;x<960;x++)
        {var index=(y*960+x)*4;stripes[index]=stripes[index+1]=stripes[index+2]=(byte)(x%2==0?0:255);stripes[index+3]=255;}
        var sharpArt=System.Windows.Media.Imaging.BitmapSource.Create(960,960,96,96,System.Windows.Media.PixelFormats.Pbgra32,null,stripes,960*4);sharpArt.Freeze();
        var sharpSettings=new Preferences{Preset="Gallery",ShowInfo=false,CoverSize=100*960.0/2160};
        var sharp=WallpaperRenderer.Render(sharpArt,track,sharpSettings,3840,2160);var detail=new byte[64*4];
        sharp.CopyPixels(new System.Windows.Int32Rect(1800,1080,64,1),detail,64*4,0);
        int dark=0,light=0;for(int i=0;i<detail.Length;i+=4){if(detail[i]<20)dark++;if(detail[i]>235)light++;}
        Check(dark>=28&&light>=28,"4K foreground preserves one-pixel artwork detail without an intermediate upscale");
        var tiny=System.Windows.Media.Imaging.BitmapSource.Create(8,8,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[8*8*4],8*4);
        var tinyEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();tinyEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(tiny));
        using(var buffer=new MemoryStream())
        {
            tinyEncoder.Save(buffer);using var imageResponse=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(buffer.ToArray())};
            var decoded=await Artwork.DecodeAsync(imageResponse,System.Threading.CancellationToken.None);
            Check(decoded.PixelWidth==8&&decoded.PixelHeight==8,"Artwork decode preserves real source dimensions instead of upscaling thumbnails");
        }
        Check(Artwork.PreferredUrl("https://yt3.googleusercontent.com/cover=w60-h60-s-l90-rj")=="https://yt3.googleusercontent.com/cover=w2400-h2400-l100-rj","Artwork requests high-quality derivatives without forced enlargement/cropping");
        var red=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Pbgra32,null,new byte[]{0,0,255,255},4);red.Freeze();
        var blue=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Pbgra32,null,new byte[]{255,0,0,255},4);blue.Freeze();
        Check(ReferenceEquals(CrossfadeView.Blend(red,blue,0),red)&&ReferenceEquals(CrossfadeView.Blend(red,blue,1),blue),"Crossfade endpoints preserve exact original/final bitmap detail");
        var midpoint=new byte[4];CrossfadeView.Blend(red,blue,.5).CopyPixels(midpoint,4,0);
        Check(midpoint[3]==255&&midpoint[0]>=126&&midpoint[0]<=129&&midpoint[2]>=126&&midpoint[2]<=129,"Crossfade midpoint stays opaque and mixes both colors without a black dip");
        Check(new Preferences().SmoothTransitions,"Smooth song transitions enabled by default for existing and new settings");
        var preferenceFolder=Path.Combine(Path.GetDirectoryName(reportPath)!,"configuration-fixture");
        var saved=new Preferences{Preset="Backdrop",Blur=73,Darkness=11,CoverSize=61,ShowInfo=false,AutoApply=true,SmoothTransitions=false,Widget=true,RestoreOnExit=false,Resolution="4K",Theme="Dark",StudioLeft=80,StudioTop=90,StudioWidth=1500,StudioHeight=900,StudioMaximized=true,ControllerLeft=400,ControllerTop=500,ControllerPinned=true};
        saved.Save(preferenceFolder);var reloaded=Preferences.Load(preferenceFolder);
        Check(JsonSerializer.Serialize(saved,Preferences.Json)==JsonSerializer.Serialize(reloaded,Preferences.Json),"All appearance, automation, theme, window and mini-player preferences survive a new load");
        saved.Save(preferenceFolder);File.WriteAllText(Path.Combine(preferenceFolder,"settings.json"),"broken JSON");
        var recovered=Preferences.Load(preferenceFolder);
        Check(recovered.Token==saved.Token&&recovered.Preset==saved.Preset&&recovered.Theme==saved.Theme&&recovered.ControllerPinned,"Damaged settings recover from an atomic-save backup without losing pairing");
        Check(Bridge.IsArtworkUrl("https://lh3.googleusercontent.com/art=w1200"),"YouTube CDN allowed");
        Check(Bridge.IsArtworkUrl("https://yt3.googleusercontent.com/art=w1200"),"Current YouTube Music artwork host allowed");
        Check(!Bridge.IsArtworkUrl("https://lh3.googleusercontent.com.evil.test/art"),"Host suffix spoof rejected");
        Check(!Bridge.IsArtworkUrl("http://127.0.0.1/art"),"Local artwork requests rejected");
        Check(!Bridge.IsValid(track with{Url="https://youtube.com/watch"}),"Non-Music sources rejected");
        await using var bridge=new Bridge(p.Token);await bridge.StartAsync(47837);
        using var client=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:47837")};
        Check((await client.GetAsync("/health")).StatusCode==HttpStatusCode.Unauthorized,"Unauthenticated requests rejected");
        client.DefaultRequestHeaders.Add("X-Afterglow-Token",p.Token);
        Check((await client.GetAsync("/health")).IsSuccessStatusCode,"Pairing key authenticates");
        Check((await client.PostAsJsonAsync("/state",track,Preferences.Json)).IsSuccessStatusCode,"Playing state accepted");
        Check(bridge.Connected&&bridge.Current.Title==track.Title,"Now playing state updated");
        Check(bridge.Send("next"),"Playback command enqueued");
        var other=await client.PostAsJsonAsync("/state",track with{Source="tab-2"},Preferences.Json);
        Check(!(await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("selected").GetBoolean(),"Competing playing tab cannot steal controls");
        var response=await client.PostAsJsonAsync("/state",track,Preferences.Json);var json=await response.Content.ReadFromJsonAsync<JsonElement>();
        Check(json.GetProperty("commands")[0].GetProperty("action").GetString()=="next","Command delivered to selected YouTube Music tab");
        response=await client.PostAsJsonAsync("/state",track,Preferences.Json);json=await response.Content.ReadFromJsonAsync<JsonElement>();
        Check(json.GetProperty("commands").GetArrayLength()==0,"Commands are delivered once");
        Check((await client.PostAsJsonAsync("/state",track with{Artwork="https://example.com/a.png"},Preferences.Json)).StatusCode==HttpStatusCode.BadRequest,"Non-YouTube artwork blocked");
        Check((await client.PostAsync("/state",new StringContent("{\"title\":null,\"source\":\"tab-1\"}",Encoding.UTF8,"application/json"))).StatusCode==HttpStatusCode.BadRequest,"Null state fields rejected");
        Check(!bridge.Send("arbitrary-script"),"Unknown playback commands rejected");
        Check(Desktop.CurrentWallpaper() is not null,"Windows wallpaper API readable without admin");
        using(var desktop=new NativeWallpaper())
        {
            var snapshot=desktop.Capture(false);
            Check(snapshot.Monitors.Count>0,"Per-monitor Windows wallpaper paths readable");
            Check(snapshot.Monitors.TrueForAll(m=>m.Path==""||File.Exists(m.Path)),"Current monitor wallpapers exist");
            Check(!desktop.Owns(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png")),"Unrelated wallpaper ownership rejected");
            Check(JsonSerializer.Deserialize<WallpaperSnapshot>(JsonSerializer.Serialize(snapshot,Preferences.Json),Preferences.Json)!.Monitors.Count==snapshot.Monitors.Count,"Monitor/slideshow snapshot persists across restarts");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath)!,"desktop-state.json"),JsonSerializer.Serialize(snapshot,Preferences.Json));
        }
        var livePath=Path.Combine(Path.GetDirectoryName(reportPath)!,"live-track.json");
        if(File.Exists(livePath))
        {
            var live=JsonSerializer.Deserialize<Track>(File.ReadAllText(livePath),Preferences.Json)!;
            var realArt=await Artwork.DownloadAsync(live.Artwork,System.Threading.CancellationToken.None);
            Check(realArt.PixelWidth>=256,"Live YouTube Music artwork downloaded; native source "+realArt.PixelWidth+" × "+realArt.PixelHeight);
            p.Preset="Ambient";WallpaperRenderer.Save(WallpaperRenderer.Render(realArt,live,p,1920,1080),Path.Combine(Path.GetDirectoryName(reportPath)!,"live-wallpaper.png"));
            WallpaperRenderer.Save(WallpaperRenderer.Render(realArt,live,p,3840,2160),Path.Combine(Path.GetDirectoryName(reportPath)!,"live-wallpaper-4K.png"));
            foreach(var preset in new[]{"Backdrop","Gallery"})
            {p.Preset=preset;WallpaperRenderer.Save(WallpaperRenderer.Render(realArt,live,p,1920,1080),Path.Combine(Path.GetDirectoryName(reportPath)!,"live-"+preset+".png"));}
            p.Preset="Ambient";WallpaperRenderer.Save(WallpaperRenderer.Render(realArt,live,p,1080,1920),Path.Combine(Path.GetDirectoryName(reportPath)!,"live-portrait.png"));
        }
        report.AppendLine($"\n{tests} checks passed. Wallpaper was not changed by this test.");File.WriteAllText(reportPath,report.ToString());
    }
}
