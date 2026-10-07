using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Afterglow;

internal static class Backdrop
{
    // Blur a small color field, never the foreground cover or lettering. Bright,
    // saturated pixels carry more weight so album colors survive a broad blur.
    public static BitmapSource Create(BitmapSource art,double softness)
    {
        const int side=192;
        var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24,24,30)),null,new Rect(0,0,side,side));
            dc.DrawImage(art,new Rect(0,0,side,side));
        }
        var sample=new RenderTargetBitmap(side,side,96,96,PixelFormats.Pbgra32);sample.Render(visual);
        var pixels=new byte[side*side*4];sample.CopyPixels(pixels,side*4,0);
        var input=new float[pixels.Length];var temporary=new float[pixels.Length];
        for(int i=0;i<pixels.Length;i+=4)
        {
            var max=Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]));var min=Math.Min(pixels[i],Math.Min(pixels[i+1],pixels[i+2]));
            var saturation=max==0?0:(max-min)/(float)max;var brightness=max/255f;
            var weight=.30f+3.5f*saturation*saturation*brightness*brightness;
            input[i]=pixels[i]*weight;input[i+1]=pixels[i+1]*weight;input[i+2]=pixels[i+2]*weight;input[i+3]=weight;
        }
        var sigma=2+Math.Clamp(softness,0,100)*.25;var radius=(int)Math.Ceiling(sigma*3);
        var kernel=new float[radius*2+1];double total=0;
        for(int k=-radius;k<=radius;k++){kernel[k+radius]=(float)Math.Exp(-(k*k)/(2*sigma*sigma));total+=kernel[k+radius];}
        for(int k=0;k<kernel.Length;k++)kernel[k]/=(float)total;
        Pass(input,temporary,true);Pass(temporary,input,false);
        for(int i=0;i<pixels.Length;i+=4)
        {
            var b=input[i]/input[i+3];var g=input[i+1]/input[i+3];var r=input[i+2]/input[i+3];
            var gray=.2126f*r+.7152f*g+.0722f*b;
            pixels[i]=(byte)Math.Clamp(gray+(b-gray)*1.10f,0,255);
            pixels[i+1]=(byte)Math.Clamp(gray+(g-gray)*1.10f,0,255);
            pixels[i+2]=(byte)Math.Clamp(gray+(r-gray)*1.10f,0,255);pixels[i+3]=255;
        }
        var bitmap=BitmapSource.Create(side,side,96,96,PixelFormats.Pbgra32,null,pixels,side*4);bitmap.Freeze();return bitmap;

        void Pass(float[] source,float[] destination,bool horizontal)
        {
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                float b=0,g=0,r=0,w=0;
                for(int k=-radius;k<=radius;k++)
                {
                    var index=(horizontal?y*side+Math.Clamp(x+k,0,side-1):Math.Clamp(y+k,0,side-1)*side+x)*4;var weight=kernel[k+radius];
                    b+=source[index]*weight;g+=source[index+1]*weight;r+=source[index+2]*weight;w+=source[index+3]*weight;
                }
                var p=(y*side+x)*4;destination[p]=b;destination[p+1]=g;destination[p+2]=r;destination[p+3]=w;
            }
        }
    }
}
