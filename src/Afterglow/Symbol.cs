using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterglow;

// Original 24-point vector glyphs. No Apple fonts or SF Symbols are bundled.
public sealed class Symbol : Control
{
    public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(Symbol),new FrameworkPropertyMetadata("wallpaper",FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind {get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
    private static readonly Dictionary<string,Geometry> Shapes=new();
    private static readonly Dictionary<string,string> Data=new()
    {
        ["wallpaper"]="M3,5 L21,5 21,19 3,19 Z M3,15 L8,10 13,15 16,12 21,17 M17,9 A1,1 0 1 1 17,11 A1,1 0 1 1 17,9",
        ["appearance"]="M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 M12,3 L12,21",
        ["lyrics"]="M4,4 L20,4 20,17 12,17 7,21 7,17 4,17 Z M8,9 L8,12 M12,9 L12,12 M16,9 L16,12",
        ["controller"]="M9,17 L9,4 19,2 19,15 M9,7 L19,5 M9,17 A3,2 0 1 1 3,17 A3,2 0 1 1 9,17 M19,15 A3,2 0 1 1 13,15 A3,2 0 1 1 19,15",
        ["connection"]="M9,8 L7,8 A4,4 0 0 0 7,16 L10,16 M15,8 L17,8 A4,4 0 0 1 17,16 L14,16 M8,12 L16,12",
        ["search"]="M10,3 A7,7 0 1 1 10,17 A7,7 0 1 1 10,3 M15,15 L21,21",
        ["sidebar"]="M3,4 L21,4 21,20 3,20 Z M9,4 L9,20 M5,8 L7,8 M5,12 L7,12",
        ["external"]="M14,3 L21,3 21,10 M21,3 L10,14 M11,5 L4,5 4,20 19,20 19,13",
        ["previous"]="M5,5 L5,19 M19,5 L8,12 19,19 Z",
        ["next"]="M19,5 L19,19 M5,5 L16,12 5,19 Z",
        ["play"]="M7,4 L20,12 7,20 Z",
        ["pause"]="M6,4 L10,4 10,20 6,20 Z M14,4 L18,4 18,20 14,20 Z",
        ["close"]="M6,6 L18,18 M18,6 L6,18",
        ["restore"]="M4,10 A8,8 0 1 1 6,18 M4,4 L4,10 10,10",
        ["check"]="M4,12 L9,17 20,6",
        ["settings"]="M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21"
    };
    public Symbol(){Width=18;Height=18;Focusable=false;IsHitTestVisible=false;}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var name=Data.ContainsKey(Kind)?Kind:"wallpaper";
        if(!Shapes.TryGetValue(name,out var shape)){shape=Geometry.Parse(Data[name]);shape.Freeze();Shapes[name]=shape;}
        var pen=new Pen(Foreground,1.7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));
        dc.DrawGeometry(name is "play" or "pause"?Foreground:null,name is "play" or "pause"?null:pen,shape);dc.Pop();
    }
}
public static class RoundClip
{
    public static readonly DependencyProperty RadiusProperty=DependencyProperty.RegisterAttached("Radius",typeof(double),typeof(RoundClip),new PropertyMetadata(0.0,Changed));
    public static void SetRadius(DependencyObject target,double radius)=>target.SetValue(RadiusProperty,radius);
    public static double GetRadius(DependencyObject target)=>(double)target.GetValue(RadiusProperty);
    private static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {if(target is FrameworkElement element){element.SizeChanged+=(_,_)=>Apply(element);Apply(element);}}
    private static void Apply(FrameworkElement element){var r=GetRadius(element);element.Clip=new RectangleGeometry(new Rect(0,0,element.ActualWidth,element.ActualHeight),r,r);}
}
