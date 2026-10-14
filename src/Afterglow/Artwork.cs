using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Afterglow;

public static class Artwork
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12) };
    public static async Task<BitmapSource> DownloadAsync(string url, CancellationToken cancel)
    {
        if (!Bridge.IsArtworkUrl(url)) throw new InvalidOperationException("Artwork must come from YouTube's image hosts.");
        var preferred=PreferredUrl(url);
        using var response = await Client.GetAsync(preferred, HttpCompletionOption.ResponseHeadersRead, cancel);
        if(!response.IsSuccessStatusCode && preferred!=url)return await DownloadOriginalAsync(url,cancel);
        return await DecodeAsync(response,cancel);
    }
    internal static string PreferredUrl(string url)
    {
        var host=new Uri(url).Host;
        return host.EndsWith("googleusercontent.com",StringComparison.OrdinalIgnoreCase)||host.EndsWith("ggpht.com",StringComparison.OrdinalIgnoreCase)
            ? Regex.Replace(url,@"=(?:w|s)\d+[^?]*","=w2400-h2400-l100-rj") : url;
    }
    private static async Task<BitmapSource> DownloadOriginalAsync(string url,CancellationToken cancel)
    {
        using var response=await Client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancel);
        return await DecodeAsync(response,cancel);
    }
    internal static async Task<BitmapSource> DecodeAsync(HttpResponseMessage response,CancellationToken cancel)
    {
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 12 * 1024 * 1024) throw new IOException("Artwork exceeds 12 MB.");
        using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var bytes = new MemoryStream();
        var buffer = new byte[16384]; int read;
        while ((read = await stream.ReadAsync(buffer, cancel)) > 0)
        { if (bytes.Length + read > 12 * 1024 * 1024) throw new IOException("Artwork exceeds 12 MB."); await bytes.WriteAsync(buffer.AsMemory(0, read), cancel); }
        return await RenderWorker.Wallpapers.Run<BitmapSource>(()=>
        {
            bytes.Position=0;
            var decoder=BitmapDecoder.Create(bytes,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
            var frame=decoder.Frames[0];var sourceWidth=frame.PixelWidth;var sourceHeight=frame.PixelHeight;
            bytes.Position=0;
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
            if(Math.Max(sourceWidth,sourceHeight)>4096){if(sourceWidth>=sourceHeight)bitmap.DecodePixelWidth=4096;else bitmap.DecodePixelHeight=4096;}
            bitmap.StreamSource=bytes;bitmap.EndInit();bitmap.Freeze();return bitmap;
        },cancel);
    }
    public static BitmapSource Demo()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(33, 22, 67), Color.FromRgb(202, 81, 65), 70), null, new Rect(0, 0, 800, 800));
            var glow = new RadialGradientBrush(Color.FromRgb(255, 203, 125), Color.FromArgb(0, 241, 151, 135));
            dc.DrawEllipse(glow, null, new Point(515, 330), 400, 380);
            for (int i = 0; i < 20; i++)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(24 + i * 3), 255, 209, 174)), 2.5);
                dc.DrawEllipse(null, pen, new Point(400, 460), 145 + i * 14, 110 + i * 12);
            }
            dc.DrawEllipse(new LinearGradientBrush(Color.FromRgb(255, 221, 155), Color.FromRgb(245, 122, 108), 90), null, new Point(405, 380), 115, 115);
            dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(0, 20, 11, 45), Color.FromArgb(220, 20, 11, 45), 90), null, new Rect(0, 490, 800, 310));
            DrawText(dc, "AFTERGLOW", 43, 72, 66, Brushes.White, 670, FontWeights.SemiBold);
            DrawText(dc, "A PLACE FOR YOUR MUSIC", 15, 76, 124, new SolidColorBrush(Color.FromArgb(200,255,255,255)), 670, FontWeights.Normal);
            DrawText(dc, "01 / THE AMBIENT COLLECTION", 16, 76, 706, Brushes.White, 670, FontWeights.Normal);
        }
        var b = new RenderTargetBitmap(800, 800, 96, 96, PixelFormats.Pbgra32); b.Render(visual); b.Freeze(); return b;
    }
    internal static void DrawText(DrawingContext dc, string value, double size, double x, double y, Brush brush, double width, FontWeight weight)
    {
        var text = new FormattedText(value, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush, 1);
        text.MaxTextWidth = Math.Max(1, width); text.MaxLineCount = 2; text.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(text, new Point(x, y));
    }
    public static Color[] Palette(BitmapSource art)
    {
        var small = new TransformedBitmap(art, new ScaleTransform(32.0 / art.PixelWidth, 32.0 / art.PixelHeight));
        var converted = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        byte[] bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0);
        var colors = new Color[3];
        for (int part = 0; part < 3; part++)
        {
            double r=0,g=0,b=0,n=0;
            for (int y = part * converted.PixelHeight / 3; y < (part+1) * converted.PixelHeight / 3; y++)
                for (int x = 0; x < converted.PixelWidth; x++)
                {
                    int p = (y * converted.PixelWidth + x)*4; double w = 0.25 + (Math.Max(bytes[p], Math.Max(bytes[p+1],bytes[p+2])) - Math.Min(bytes[p], Math.Min(bytes[p+1],bytes[p+2]))) / 255.0;
                    b+=bytes[p]*w;g+=bytes[p+1]*w;r+=bytes[p+2]*w;n+=w;
                }
            colors[part]=Color.FromRgb((byte)(r/n),(byte)(g/n),(byte)(b/n));
        }
        return colors;
    }
}

public static class WallpaperRenderer
{
    [ThreadStatic] private static BitmapSource? cachedArt;
    [ThreadStatic] private static BitmapSource? cachedBackdrop;
    [ThreadStatic] private static double cachedSoftness;

    public static BitmapSource Render(BitmapSource art, Track track, Preferences p, int width, int height)
    {
        if(width<1||height<1)throw new ArgumentOutOfRangeException(nameof(width));
        var scale=height/1080.0;var w=width/scale;const double h=1080;
        var compact=w<1400;var preset=p.Preset=="Ambient"&&compact?"Gallery":p.Preset;
        var visual=new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        TextOptions.SetTextRenderingMode(visual,TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(visual,TextFormattingMode.Ideal);
        using(var dc=visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale,scale));
            dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h)));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(23,23,29)),null,new Rect(0,0,w,h));
            if(preset=="Immersive"&&p.Blur<1)
            {
                var fit=Math.Max(w/art.PixelWidth,h/art.PixelHeight);
                dc.DrawImage(art,new Rect((w-art.PixelWidth*fit)/2,(h-art.PixelHeight*fit)/2,art.PixelWidth*fit,art.PixelHeight*fit));
            }
            else
            {
                if(!ReferenceEquals(cachedArt,art)||cachedSoftness!=p.Blur)
                {cachedBackdrop=Backdrop.Create(art,p.Blur);cachedArt=art;cachedSoftness=p.Blur;}
                dc.DrawImage(cachedBackdrop,new Rect(0,0,w,h));
            }
            var vignette=new RadialGradientBrush();
            vignette.Center=new Point(.50,.47);vignette.GradientOrigin=vignette.Center;vignette.RadiusX=.74;vignette.RadiusY=.82;
            vignette.GradientStops.Add(new GradientStop(Colors.Transparent,.28));
            vignette.GradientStops.Add(new GradientStop(Color.FromArgb(105,12,11,18),1));
            dc.DrawRectangle(vignette,null,new Rect(0,0,w,h));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(p.Darkness*1.70,0,220),0,0,0)),null,new Rect(0,0,w,h));
            if(preset=="Gallery")dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(22,8,9,15)),null,new Rect(0,0,w,h));
            if(preset=="Ambient"&&p.ShowInfo)
            {
                var shade=new LinearGradientBrush(Color.FromArgb(74,10,12,22),Colors.Transparent,0);
                dc.DrawRectangle(shade,null,new Rect(0,0,w*.67,h));
            }

            var coverSize=Math.Min(h*p.CoverSize/100,w*(w<h?.92:.80));
            if(preset=="Gallery"&&p.ShowInfo)coverSize=Math.Min(coverSize,h*.60);
            var aspect=(double)art.PixelWidth/art.PixelHeight;
            var coverW=aspect>=1?coverSize:coverSize*aspect;var coverH=aspect>=1?coverSize/aspect:coverSize;
            var coverX=preset=="Gallery"?(w-coverW)/2:w*.69-coverW/2;
            var coverY=(h-coverH)/2-(preset=="Gallery"&&p.ShowInfo?105:0);
            if(p.ShowLyrics)
            {
                coverSize=Math.Min(h*(w>=h?Math.Clamp(.46*p.CoverSize/54,.25,.56):.28),w*(w>=h?.34:.65));
                coverW=aspect>=1?coverSize:coverSize*aspect;coverH=aspect>=1?coverSize/aspect:coverSize;
                coverX=w*(w>=h?.27:.5)-coverW/2;coverY=h*(w>=h?.43:.23)-coverH/2;
            }
            if(p.ShowLyrics||preset is not ("Immersive" or "Backdrop"))
            {
                var rect=new Rect(coverX,coverY,coverW,coverH);var radius=14.0;
                // Shadow is drawn separately; effects on a parent border rasterized and softened the cover.
                for(int layer=20;layer>=1;layer--)
                {
                    var spread=layer*2.2;var shadow=new Rect(rect.X-spread,rect.Y-spread+15,rect.Width+spread*2,rect.Height+spread*2);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(3,0,0,0)),null,shadow,radius+spread,radius+spread);
                }
                dc.PushClip(new RectangleGeometry(rect,radius,radius));dc.DrawImage(art,rect);dc.Pop();
                dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(30,255,255,255)),.7),rect,radius,radius);
            }
            if(p.ShowInfo&&(p.ShowLyrics||preset!="Backdrop"))
            {
                var x=preset=="Ambient"?w*.105:preset=="Gallery"?coverX:Math.Min(120,w*.1);
                var y=preset=="Ambient"?h*.41:preset=="Gallery"?coverY+coverH+44:h*.70;
                var textWidth=preset=="Ambient"?Math.Max(160,coverX-x-76):preset=="Gallery"?coverW:w-x*2;
                var centered=preset=="Gallery";
                var titleSize=preset=="Gallery"?38:58;
                if(p.ShowLyrics){x=coverX;y=coverY+coverH+30;textWidth=coverW;centered=false;titleSize=32;}
                Label(dc,"YouTube Music",16,x,y,textWidth,scale,FontWeights.Medium,Color.FromArgb(205,255,255,255),centered,1);
                var titleHeight=Label(dc,track.Title,titleSize,x,y+35,textWidth,scale,FontWeights.SemiBold,Colors.White,centered,2);
                var artistY=y+35+titleHeight+17;
                var artistHeight=Label(dc,track.Artist,preset=="Gallery"?24:29,x,artistY,textWidth,scale,FontWeights.Normal,Color.FromArgb(230,255,255,255),centered,2);
                if(!string.IsNullOrWhiteSpace(track.Album))
                    Label(dc,track.Album,18,x,artistY+artistHeight+13,textWidth,scale,FontWeights.Normal,Color.FromArgb(166,255,255,255),centered,1);
            }
            dc.Pop();dc.Pop();
        }
        // Vector text and source artwork are rasterized directly into the final pixel grid.
        var output=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);output.Render(visual);output.Freeze();return output;
    }
    private static double Label(DrawingContext dc,string value,double size,double x,double y,double width,double pixelsPerDip,FontWeight weight,Color color,bool centered,int lines)
    {
        var typeface=new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"),FontStyles.Normal,weight,FontStretches.Normal);
        var text=new FormattedText(value??"",System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
            typeface,size,new SolidColorBrush(color),pixelsPerDip)
        {MaxTextWidth=Math.Max(1,width),MaxLineCount=lines,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=centered?TextAlignment.Center:TextAlignment.Left};
        // A tiny independent shadow improves contrast without blurring the glyphs.
        var shadow=new FormattedText(value??"",System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,typeface,size,new SolidColorBrush(Color.FromArgb(48,0,0,0)),pixelsPerDip)
        {MaxTextWidth=text.MaxTextWidth,MaxLineCount=lines,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=text.TextAlignment};
        dc.DrawText(shadow,new Point(x,y+1));dc.DrawText(text,new Point(x,y));return text.Height;
    }
    public static void Save(BitmapSource image,string path)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
    public static void SaveDesktop(BitmapSource image,string path)
    {
        Save(image,path);
    }
}
