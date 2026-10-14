using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Afterglow;

public sealed class ConnectWindow : Window
{
    private readonly Preferences preferences;
    private readonly Bridge bridge;
    private readonly DateTime opened=DateTime.UtcNow;
    private readonly ComboBox browsers=new(){MinWidth=240,DisplayMemberPath="Name"};
    private readonly StackPanel body=new();
    private readonly TextBlock progress=new(),status=new(){TextWrapping=TextWrapping.Wrap};
    private readonly Button next=new(),back=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    private readonly CheckBox follow=new(){Content="Follow the playing song automatically"};
    private int step;
    private bool paired;
    private DateTime feedbackUntil;
    public ConnectWindow(Preferences p,Bridge music)
    {
        preferences=p;bridge=music;Title="Set up Afterglow";Width=650;Height=620;MinWidth=580;MinHeight=570;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty,"WindowBrush");SetResourceReference(ForegroundProperty,"TextBrush");
        MotionPolicy.SetEnabled(this,SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast);
        var root=new Grid{Margin=new Thickness(30)};root.SetResourceReference(Panel.BackgroundProperty,"WindowBrush");Content=root;
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var heading=new StackPanel();root.Children.Add(heading);
        heading.Children.Add(new TextBlock{Text="Make room for your music.",FontSize=28,FontWeight=FontWeights.SemiBold});
        progress.Margin=new Thickness(0,8,0,22);progress.FontSize=12;progress.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");heading.Children.Add(progress);
        var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        var notice=new Border{CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Margin=new Thickness(0,18,0,18),Child=status};notice.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");Grid.SetRow(notice,2);root.Children.Add(notice);status.FontSize=13;
        var actions=new DockPanel{LastChildFill=false};Grid.SetRow(actions,3);root.Children.Add(actions);
        var later=Button("Set up later",()=>Close(),"LinkButton");DockPanel.SetDock(later,Dock.Left);actions.Children.Add(later);
        next.Style=(Style)FindResource("PrimaryButton");next.Click+=(_,_)=>Continue();DockPanel.SetDock(next,Dock.Right);actions.Children.Add(next);
        back.Content="Back";back.Style=(Style)FindResource("SecondaryButton");back.Margin=new Thickness(0,0,8,0);back.Click+=(_,_)=>{step--;Render();};DockPanel.SetDock(back,Dock.Right);actions.Children.Add(back);
        foreach(var browser in BrowserSetup.FindBrowsers())browsers.Items.Add(browser);browsers.SelectedIndex=browsers.Items.Count>0?0:-1;foreach(SetupBrowser browser in browsers.Items)if(browser.Name==p.SetupBrowserName)browsers.SelectedItem=browser;
        browsers.SelectionChanged+=(_,_)=>{if(step==0)UpdateStatus();};follow.IsChecked=p.AutoApply;
        timer.Tick+=(_,_)=>UpdateStatus();timer.Start();Closed+=(_,_)=>timer.Stop();PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}};
        Render();
    }
    private static Button Button(string text,Action click,string style="SecondaryButton")
    {var button=new Button{Content=text,HorizontalAlignment=HorizontalAlignment.Left};button.SetResourceReference(StyleProperty,style);button.Click+=(_,_)=>click();return button;}
    private void Text(string text,bool heading=false)
    {
        var label=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=heading?20:14,FontWeight=heading?FontWeights.SemiBold:FontWeights.Normal,Margin=new Thickness(0,0,0,heading?14:18)};
        label.SetResourceReference(TextBlock.ForegroundProperty,heading?"TextBrush":"MutedBrush");body.Children.Add(label);
    }
    private void Render()
    {
        body.Children.Clear();progress.Text=$"{step+1} of 3  ·  "+(step==0?"Choose a browser":step==1?"Add the extension":"Connect your music");back.Visibility=step==0?Visibility.Collapsed:Visibility.Visible;
        if(step==0)
        {
            Text("Where do you listen?",true);Text("Choose the browser you use for YouTube Music. We'll prepare an extension already paired with this PC and open its Extensions page.");body.Children.Add(browsers);
            Text("\nYour Google sign-in stays in the browser. Afterglow reads the playing song on YouTube Music and sends it to this PC.");
            Text("Already connected? Skip straight to the last step.");body.Children.Add(Button("I already have the extension",()=>{step=2;Render();},"LinkButton"));next.Content="Prepare & open browser";
        }
        else if(step==1)
        {
            Text("Just a few browser clicks.",true);
            Text("1. Turn on Developer mode on the Extensions page.\n2. Select Load unpacked.\n3. Paste the copied folder path into the folder picker and select that folder.");
            var path=new TextBox{Text=BrowserSetup.ExtensionFolder,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};System.Windows.Automation.AutomationProperties.SetName(path,"Extension folder path");body.Children.Add(path);
            var row=new StackPanel{Orientation=Orientation.Horizontal};var copy=Button("Copy folder path",()=>{try{Clipboard.SetText(BrowserSetup.ExtensionFolder);Feedback("Folder path copied. Paste it into the Load unpacked folder picker.");}catch(Exception e){Feedback(e.Message);}});row.Children.Add(copy);
            var reopen=Button("Reopen Extensions",()=>TryOpenExtensions());reopen.Margin=new Thickness(8,0,0,0);row.Children.Add(reopen);body.Children.Add(row);
            Text("\nA connection page opens automatically when the extension is installed. No pairing key to copy. If you loaded this folder before, use Reload on the extension instead.");next.Content="I've loaded it · open music";
        }
        else
        {
            Text("Play something you love.",true);Text("Open YouTube Music in your chosen browser and play a song. If it was already open when you installed the extension, refresh that tab once.");
            body.Children.Add(Button("Open YouTube Music",()=>OpenMusic()));
            follow.Margin=new Thickness(0,22,0,10);body.Children.Add(follow);
            Text("When enabled, new songs update your wallpaper automatically. Otherwise, choose Apply wallpaper in the Studio.");
            body.Children.Add(Button("Need the manual pairing key?",()=>ShowManualPairing(),"LinkButton"));next.Content="Finish setup";
        }
        UpdateStatus();
    }
    private void Continue()
    {
        try
        {
            if(step==0)
            {
                if(browsers.SelectedItem is not SetupBrowser browser)return;
                preferences.SetupBrowserName=browser.Name;preferences.Save();BrowserSetup.PrepareExtension(preferences);Clipboard.SetText(BrowserSetup.ExtensionFolder);BrowserSetup.Open(browser,browser.ExtensionsUrl);step=1;Render();status.Text="Extension folder path copied. Complete the three steps in your browser.";
            }
            else if(step==1){OpenMusic();step=2;Render();}
            else if(bridge.Connected){preferences.SetupCompleted=true;preferences.AutoApply=follow.IsChecked==true;preferences.Save();Close();}
        }
        catch(Exception e){Feedback(e.Message);}
    }
    private void TryOpenExtensions(){try{if(browsers.SelectedItem is SetupBrowser browser)BrowserSetup.Open(browser,browser.ExtensionsUrl);}catch(Exception e){Feedback(e.Message);}}
    private void OpenMusic(){try{if(browsers.SelectedItem is SetupBrowser browser)BrowserSetup.Open(browser,"https://music.youtube.com/");else MainWindow.Open("https://music.youtube.com/");}catch(Exception e){Feedback(e.Message);}}
    private void UpdateStatus()
    {
        paired|=bridge.LastPairingSeen>=opened||bridge.Connected;
        next.IsEnabled=step==0?browsers.SelectedItem is SetupBrowser:step==1||bridge.Connected;
        if(DateTime.UtcNow<feedbackUntil)return;
        if(bridge.Connected)status.Text="Connected. YouTube Music is sending its song to Afterglow.";
        else if(paired)status.Text="Extension paired. Play a song on YouTube Music to finish.";
        else if(step==0)status.Text=browsers.Items.Count==0?"Install Google Chrome or Microsoft Edge, then reopen this guide.":"No password or pairing-key paste needed. You approve the extension in your browser.";
        else if(step==2)status.Text="Waiting for the extension. Keep Afterglow open, then refresh YouTube Music and play a song.";
    }
    private void Feedback(string text){status.Text=text;feedbackUntil=DateTime.UtcNow.AddSeconds(6);}
    internal void VerifyGuide()
    {
        if(step!=0||!next.IsEnabled&&browsers.Items.Count>0)throw new Exception("Initial browser setup state is invalid.");
        step=2;Render();if(next.IsEnabled)throw new Exception("Finish must wait for a real music connection.");
        step=1;Render();if(!next.IsEnabled)throw new Exception("Manual loading step should remain navigable.");
        step=0;Render();
    }
    private void ShowManualPairing()
    {
        var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text="Manual pairing",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        panel.Children.Add(new TextBlock{Text="Use this for an extension loaded from another folder. Click its toolbar icon, paste the key, and choose Save & test connection.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
        var copy=Button("Copy pairing key",()=>{try{Clipboard.SetText(preferences.Token);Feedback("Pairing key copied.");}catch(Exception e){Feedback(e.Message);}});panel.Children.Add(copy);
        var dialog=new Window{Title="Manual pairing · Afterglow",Owner=this,Width=440,Height=270,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};dialog.SetResourceReference(BackgroundProperty,"PanelBrush");dialog.SetResourceReference(ForegroundProperty,"TextBrush");dialog.ShowDialog();
    }
}
