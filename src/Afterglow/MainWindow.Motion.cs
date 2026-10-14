using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Afterglow;

public partial class MainWindow
{
    private bool windowMotion;
    private static bool MotionAllowed => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    private void InitializeMotion()
    {
        Loaded += (_, _) => { if (!AutomationMode && MotionAllowed) StudioSurface.BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(220))); };
        StateChanged += (_, _) => { if(WindowState==WindowState.Maximized){var bounds=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;var dpi=VisualTreeHelper.GetDpi(this);MaxHeight=bounds.Height/dpi.DpiScaleY;MaxWidth=bounds.Width/dpi.DpiScaleX;} if(WindowState==WindowState.Normal){MaxHeight=double.PositiveInfinity;MaxWidth=double.PositiveInfinity;} if (WindowState != WindowState.Minimized && !windowMotion && MotionAllowed) StudioSurface.BeginAnimation(OpacityProperty, new DoubleAnimation(.7, 1, TimeSpan.FromMilliseconds(180))); };
    }
    private async Task AnimateMinimizeAsync()
    {
        if (windowMotion) return;
        if (!MotionAllowed) { WindowState=WindowState.Minimized; return; }
        windowMotion=true; placementTimer.Stop();
        try
        {
            var scale=new ScaleTransform(1,1);StudioSurface.RenderTransform=scale;
            var animation=new DoubleAnimation(1,.96,TimeSpan.FromMilliseconds(150)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}};
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,animation);scale.BeginAnimation(ScaleTransform.ScaleYProperty,animation);
            StudioSurface.BeginAnimation(OpacityProperty,new DoubleAnimation(1,.15,TimeSpan.FromMilliseconds(150)));
            await Task.Delay(150);if(!Exiting)WindowState=WindowState.Minimized;
        }
        finally { StudioSurface.BeginAnimation(OpacityProperty,null);StudioSurface.Opacity=1;StudioSurface.RenderTransform=Transform.Identity;windowMotion=false; }
    }
    private async Task AnimateWindowSizeAsync()
    {
        if (windowMotion) return;
        if (!MotionAllowed) { WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;return; }
        var maximizing=WindowState!=WindowState.Maximized;
        var original=maximizing?new Rect(Left,Top,ActualWidth,ActualHeight):RestoreBounds;
        if(original.IsEmpty)return;
        var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        var topLeft=PointFromScreen(new Point(screen.Left,screen.Top));var bottomRight=PointFromScreen(new Point(screen.Right,screen.Bottom));
        var area=new Rect(Left+topLeft.X,Top+topLeft.Y,bottomRight.X-topLeft.X,bottomRight.Y-topLeft.Y);
        var from=maximizing?original:area;var target=maximizing?area:original;
        windowMotion=true;placementTimer.Stop();
        try
        {
            if(!maximizing)WindowState=WindowState.Normal;
            Left=from.Left;Top=from.Top;Width=from.Width;Height=from.Height;
            void Animate(DependencyProperty property,double start,double end)=>BeginAnimation(property,new DoubleAnimation(start,end,TimeSpan.FromMilliseconds(230)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}});
            Animate(LeftProperty,from.Left,target.Left);Animate(TopProperty,from.Top,target.Top);Animate(WidthProperty,from.Width,target.Width);Animate(HeightProperty,from.Height,target.Height);
            await Task.Delay(240);
        }
        finally
        {
            foreach(var property in new[]{LeftProperty,TopProperty,WidthProperty,HeightProperty})BeginAnimation(property,null);
            Left=original.Left;Top=original.Top;Width=original.Width;Height=original.Height;
            if(maximizing&&!Exiting){MaxHeight=area.Height;MaxWidth=area.Width;WindowState=WindowState.Maximized;}
            windowMotion=false;RememberPlacement();
        }
    }
    internal async Task VerifyMotionAsync()
    {
        var original=new Size(ActualWidth,ActualHeight);
        await AnimateWindowSizeAsync();await Task.Delay(80);
        var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        var dpi=VisualTreeHelper.GetDpi(this);
        if(WindowState!=WindowState.Maximized||ActualHeight*dpi.DpiScaleY>screen.Height+2)throw new Exception("Maximized studio exceeds the taskbar work area.");
        await AnimateWindowSizeAsync();await Task.Delay(80);
        if(WindowState!=WindowState.Normal||Math.Abs(ActualWidth-original.Width)>2||Math.Abs(ActualHeight-original.Height)>2)throw new Exception("Restore did not return to the original window size.");
        await AnimateMinimizeAsync();if(WindowState!=WindowState.Minimized)throw new Exception("Minimize did not finish.");
        ShowStudio();await Task.Delay(250);if(WindowState!=WindowState.Normal||StudioSurface.Opacity!=1)throw new Exception("Restore from minimize left the studio hidden.");
        StopLyrics();Exiting=true;Close();
    }
}
