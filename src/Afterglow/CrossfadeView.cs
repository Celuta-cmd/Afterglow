using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Afterglow;

// The old frame stays fully opaque while the new frame fades over it. This
// avoids the dark dip caused by fading both images' opacity at the same time.
public sealed class CrossfadeView : Grid
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(2);
    private readonly Image previous = new();
    private readonly Image next = new();
    private BitmapSource? from;
    private BitmapSource? target;
    private int revision;
    private TaskCompletionSource? completion;
    public double Progress => next.Opacity;
    public BitmapSource? Source { get => target; set { if(value is not null)SetSource(value,false); } }
    public Stretch Stretch { get => next.Stretch; set { previous.Stretch=value;next.Stretch=value; } }

    public CrossfadeView()
    {
        IsHitTestVisible=false;ClipToBounds=true;
        RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
        Children.Add(previous);Children.Add(next);
    }
    public BitmapSource? CurrentFrame() => target is null ? null : from is null || next.Opacity>=1 ? target : Blend(from,target,next.Opacity);
    public void SetSource(BitmapSource image,bool animate=true)
    { _=FadeAsync(image,animate,CancellationToken.None); }

    public async Task FadeAsync(BitmapSource image,bool animate,CancellationToken cancellation)
    {
        if(ReferenceEquals(target,image))return;
        var frame=CurrentFrame();var version=++revision;
        completion?.TrySetResult();next.BeginAnimation(OpacityProperty,null);
        from=frame;target=image;previous.Source=frame;next.Source=image;
        if(!animate||frame is null||!SystemParameters.ClientAreaAnimation)
        {next.Opacity=1;previous.Source=null;from=null;return;}
        next.Opacity=0;
        var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);completion=finished;
        var fade=new DoubleAnimation(0,1,new Duration(Duration))
        {EasingFunction=new SmoothStepEase(),FillBehavior=FillBehavior.HoldEnd};
        fade.Completed+=(_,_)=>finished.TrySetResult();
        next.BeginAnimation(OpacityProperty,fade);
        try { await finished.Task.WaitAsync(cancellation); }
        finally
        {
            if(version==revision)
            {
                // Retain the actually displayed blend on interruption. The next
                // request starts here, even when the listener skips rapidly.
                var displayed=CurrentFrame();next.BeginAnimation(OpacityProperty,null);
                next.Opacity=1;next.Source=displayed;target=displayed;from=null;previous.Source=null;
                completion=null;
            }
        }
    }
    internal static BitmapSource Blend(BitmapSource oldImage,BitmapSource newImage,double amount)
    {
        if(amount<=0)return oldImage;if(amount>=1)return newImage;
        var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen())
        {
            var bounds=new Rect(0,0,newImage.PixelWidth,newImage.PixelHeight);
            dc.DrawImage(oldImage,bounds);dc.PushOpacity(amount);dc.DrawImage(newImage,bounds);dc.Pop();
        }
        var output=new RenderTargetBitmap(newImage.PixelWidth,newImage.PixelHeight,96,96,PixelFormats.Pbgra32);
        output.Render(visual);output.Freeze();return output;
    }
}

internal sealed class SmoothStepEase : EasingFunctionBase
{
    public SmoothStepEase(){EasingMode=EasingMode.EaseIn;}
    protected override double EaseInCore(double t)=>t*t*(3-2*t);
    protected override Freezable CreateInstanceCore()=>new SmoothStepEase();
}
