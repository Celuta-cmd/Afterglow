using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Afterglow;

public partial class MainWindow
{
    private string selectedPage="Studio";
    private readonly string[] pageNames={"Studio","Appearance","Lyrics","Playback","Connection"};
    private void InitializeNavigation()
    {
        Navigate(Settings.SelectedSection,false);SetSidebar(Settings.SidebarCollapsed,false);
        SizeChanged+=(_,_)=> { if(ActualWidth>0&&ActualWidth<1020&&!Settings.SidebarCollapsed)SetSidebar(true,false,false);else if(ActualWidth>=1020&&!Settings.SidebarCollapsed)SetSidebar(false,false,false); };
        SystemEvents.UserPreferenceChanged+=SystemAppearanceChanged;
        Closed+=(_,_)=>SystemEvents.UserPreferenceChanged-=SystemAppearanceChanged;
    }
    private void SystemAppearanceChanged(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.InvokeAsync(()=>{AppearanceTheme.Apply(Settings.Theme);MotionPolicy.SetEnabled(this,MotionAllowed);});
    private ScrollViewer Page(string name)=>(ScrollViewer)FindName(name+"Page");
    internal void Navigate(string name,bool animate=true)
    {
        if(!pageNames.Contains(name))name="Studio";
        foreach(var item in pageNames){Page(item).Visibility=item==name?Visibility.Visible:Visibility.Collapsed;var button=(Button)FindName("Nav"+item);button.SetResourceReference(BackgroundProperty,item==name?"SelectionBrush":"TransparentBrush");button.SetResourceReference(ForegroundProperty,item==name?"SelectedTextBrush":"TextBrush");button.FontWeight=item==name?FontWeights.Medium:FontWeights.Normal;System.Windows.Automation.AutomationProperties.SetItemStatus(button,item==name?"Selected":"");}
        selectedPage=name;ToolbarTitle.Text=name=="Studio"?"Wallpaper Studio":name;
        if(ready&&!AutomationMode){Settings.SelectedSection=name;SaveChoices();}
        var page=Page(name);page.BeginAnimation(OpacityProperty,null);page.RenderTransform=Transform.Identity;
        if(animate&&MotionAllowed&&!AutomationMode){var move=new TranslateTransform(0,8);page.RenderTransform=move;move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(8,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});page.BeginAnimation(OpacityProperty,new DoubleAnimation(.5,1,TimeSpan.FromMilliseconds(180)));}
    }
    private void Navigate_Click(object sender,RoutedEventArgs e)=>Navigate(((Button)sender).Tag.ToString()!);
    private void Sidebar_Click(object sender,RoutedEventArgs e)=>SetSidebar(!Settings.SidebarCollapsed,true);
    private void SetSidebar(bool collapsed,bool animate,bool remember=true)
    {
        if(remember){Settings.SidebarCollapsed=collapsed;if(ready&&!AutomationMode)SaveChoices();}
        foreach(var element in new FrameworkElement[]{BrandLabel,SectionLabel,MusicLabel,SidebarStatus,VersionLabel,LabelStudio,LabelAppearance,LabelLyrics,LabelPlayback,LabelConnection})element.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;
        var width=collapsed?64.0:204.0;SidebarShell.BeginAnimation(WidthProperty,null);
        if(animate&&MotionAllowed&&!AutomationMode){var from=SidebarShell.ActualWidth;SidebarShell.Width=width;SidebarShell.BeginAnimation(WidthProperty,new DoubleAnimation(from,width,TimeSpan.FromMilliseconds(200)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}else SidebarShell.Width=width;
    }
    private void ClearSearch_Click(object sender,RoutedEventArgs e){SearchBox.Clear();SearchBox.Focus();}
    private void Shell_KeyDown(object sender,KeyEventArgs e)
    {
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var number=(int)e.Key-(int)Key.D1;if(number>=0&&number<5){Navigate(pageNames[number]);e.Handled=true;}
            else if(e.Key==Key.B){SetSidebar(!Settings.SidebarCollapsed,true);e.Handled=true;}
            else if(e.Key==Key.F){SearchBox.Focus();e.Handled=true;}
            else if(e.Key==Key.OemComma){Navigate("Appearance");e.Handled=true;}
            else if(e.Key==Key.M&&Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){WidgetCheck.IsChecked=WidgetCheck.IsChecked!=true;e.Handled=true;}
        }
        else if(e.Key==Key.Escape&&SearchBox.IsKeyboardFocusWithin){SearchBox.Clear();Focus();e.Handled=true;}
    }
    internal void VerifyNavigation()
    {
        foreach(var name in pageNames){Navigate(name,false);if(Page(name).Visibility!=Visibility.Visible||pageNames.Count(p=>Page(p).Visibility==Visibility.Visible)!=1)throw new Exception("Navigation failed: "+name);}
        SetSidebar(true,false);if(SidebarShell.Width!=64)throw new Exception("Sidebar did not collapse.");SetSidebar(false,false);if(SidebarShell.Width!=204)throw new Exception("Sidebar did not expand.");Navigate("Studio",false);
    }
}
public static class MotionPolicy
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(MotionPolicy),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetEnabled(DependencyObject element)=>(bool)element.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject element,bool enabled)=>element.SetValue(EnabledProperty,enabled);
}
