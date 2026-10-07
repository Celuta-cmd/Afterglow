using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterglow;

public sealed class ConnectWindow : Window
{
    public ConnectWindow(Preferences p)
    {
        Title="Connect YouTube Music · Afterglow";Width=580;Height=650;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(BackgroundProperty,"PanelBrush");SetResourceReference(ForegroundProperty,"TextBrush");
        var content=new StackPanel { Margin=new Thickness(30) };Content=new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        content.Children.Add(new TextBlock { Text="Bring your music along.",FontSize=28,FontWeight=FontWeights.SemiBold });
        Add("Connect the YouTube Music website in Chrome or Edge. Your Google sign-in stays in your browser.",16);
        Add("1  Load the browser extension",22,true);
        Add("Open chrome://extensions or edge://extensions. Enable Developer mode, choose Load unpacked, and select the extension folder included with Afterglow.",8);
        var folder=new Button { Content="Open extension folder",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0) };
        folder.Click+=(_,_)=>{var path=Path.Combine(AppContext.BaseDirectory,"extension");if(!Directory.Exists(path))path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","extension"));try{MainWindow.Open(path);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Extension folder");}};content.Children.Add(folder);
        Add("2  Pair it with this PC",22,true);
        Add("Open the extension's options, paste this pairing key, then select Save & test connection.",8);
        var key=new TextBox { Text=p.Token,IsReadOnly=true,Margin=new Thickness(0,10,0,8),FontFamily=new FontFamily("Consolas"),FontSize=11 };content.Children.Add(key);
        var copy=new Button { Content="Copy pairing key",HorizontalAlignment=HorizontalAlignment.Left };copy.Click+=(_,_)=>{Clipboard.SetText(p.Token);copy.Content="Copied ✓";};content.Children.Add(copy);
        Add("3  Play something on YouTube Music",22,true);
        Add("Reload your music tab after installing the extension. Album artwork will appear here. Enable Follow the playing song to update your wallpaper automatically.",8);
        var music=new Button { Content="Open YouTube Music  ↗",Background=new SolidColorBrush(Color.FromRgb(255,76,100)),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,18,0,0) };music.Click+=(_,_)=>MainWindow.Open("https://music.youtube.com/");content.Children.Add(music);
        Add("The app must stay running, including in the system tray. The bridge listens only on this PC (127.0.0.1:47836).",20);
        void Add(string text,double top,bool heading=false) {var label=new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,top,0,0),FontSize=heading?15:13,FontWeight=heading?FontWeights.SemiBold:FontWeights.Normal};label.SetResourceReference(TextBlock.ForegroundProperty,heading?"TextBrush":"MutedBrush");content.Children.Add(label);}
    }
}
