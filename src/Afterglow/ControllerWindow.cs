using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Afterglow;

public sealed class ControllerWindow : Window
{
    private readonly TextBlock title=new() {FontSize=15,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};
    private readonly TextBlock artist=new() {FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(181,173,192)),TextTrimming=TextTrimming.CharacterEllipsis};
    private readonly CrossfadeView image=new() {Width=54,Height=54,Stretch=Stretch.UniformToFill};
    private readonly MainWindow studio;
    private readonly Button play;
    private readonly Button previous;
    private readonly Button next;
    public ControllerWindow(MainWindow studio)
    {
        this.studio=studio;
        Title="Afterglow controller";
        Width=375;Height=153;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=studio.Settings.ControllerPinned;
        SetResourceReference(ForegroundProperty,"TextBrush");artist.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        Left=Math.Max(0,SystemParameters.WorkArea.Right-Width-28);Top=Math.Max(0,SystemParameters.WorkArea.Bottom-Height-28);
        if(studio.Settings.ControllerLeft is double left&&studio.Settings.ControllerTop is double top&&MainWindow.IsOnScreen(left,top,Width,Height)){Left=left;Top=top;}
        var border=new Border {BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(18,13,18,13)};border.SetResourceReference(Border.BackgroundProperty,"PanelBrush");border.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Content=border;
        var grid=new Grid();border.Child=grid;grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var head=new Grid();grid.Children.Add(head);var heading=new TextBlock{Text="AFTERGLOW  /  MINI-PLAYER",FontSize=9};heading.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");head.Children.Add(heading);
        var close=new Button{Content="×",HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(6,0,6,0),Background=Brushes.Transparent,ToolTip="Hide controller"};close.Click+=(_,_)=>Close();head.Children.Add(close);
        var body=new Grid{Margin=new Thickness(0,8,0,0)};Grid.SetRow(body,1);grid.Children.Add(body);body.ColumnDefinitions.Add(new(){Width=new GridLength(68)});body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});body.Children.Add(image);
        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(labels,1);body.Children.Add(labels);labels.Children.Add(title);labels.Children.Add(artist);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,0)};Grid.SetRow(buttons,2);grid.Children.Add(buttons);
        previous=Make("⏮","previous");play=Make("▶","toggle");next=Make("⏭","next");buttons.Children.Add(previous);buttons.Children.Add(play);buttons.Children.Add(next);
        var pin=new Button{Content=Topmost?"Unpin":"Pin",Margin=new Thickness(16,0,0,0),Padding=new Thickness(9,3,9,3),FontSize=10};pin.Click+=(_,_)=>{Topmost=!Topmost;pin.Content=Topmost?"Unpin":"Pin";studio.Settings.ControllerPinned=Topmost;studio.Settings.Save();};buttons.Children.Add(pin);
        var open=new Button{Content="Studio",Margin=new Thickness(6,0,0,0),Padding=new Thickness(9,3,9,3),FontSize=10};open.Click+=(_,_)=>studio.ShowStudio();buttons.Children.Add(open);
        border.MouseLeftButtonDown+=(_,e)=>
        {
            var origin=e.OriginalSource as DependencyObject;while(origin is not null&&origin is not Button)origin=VisualTreeHelper.GetParent(origin);
            if(origin is null&&e.LeftButton==MouseButtonState.Pressed){try{DragMove();studio.Settings.ControllerLeft=Left;studio.Settings.ControllerTop=Top;studio.Settings.Save();}catch(InvalidOperationException){}}
        };
        Button Make(string label,string action){var b=new Button{Content=label,Padding=new Thickness(13,3,13,3),Background=Brushes.Transparent,ToolTip=action};b.Click+=(_,_)=>studio.SendCommand(action);return b;}
    }
    public void Update(Track t,BitmapSource art){title.Text=t.Title;artist.Text=t.Artist;image.SetSource(art,studio.Settings.SmoothTransitions);play.Content=t.Playing?"Ⅱ":"▶";}
    public void SetConnected(bool enabled)=>previous.IsEnabled=play.IsEnabled=next.IsEnabled=enabled;
}
