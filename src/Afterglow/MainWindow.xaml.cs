using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Afterglow;

public partial class MainWindow : Window
{
    public Preferences Settings { get; }
    public Bridge Music { get; }
    private BitmapSource art=Artwork.Demo();
    private Track track=new();
    private string loadedArtwork="";
    private string pendingArtwork="";
    private DateTime artworkRetryAfter=DateTime.MinValue;
    private bool suspendWallpaper;
    private string renderKey="";
    private CancellationTokenSource? artworkRequest;
    private readonly SemaphoreSlim wallpaperGate=new(1,1);
    private CancellationTokenSource? wallpaperFade;
    private DesktopTransition? desktopTransition;
    private int applyRevision;
    private int desktopEpoch;
    private int previewRevision;
    private CancellationTokenSource? previewRequest;
    internal Task PreviewReady { get; private set; }=Task.CompletedTask;
    private readonly DispatcherTimer debounce=new() { Interval=TimeSpan.FromMilliseconds(240) };
    private readonly DispatcherTimer connectionTimer=new() { Interval=TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer placementTimer=new(){Interval=TimeSpan.FromMilliseconds(300)};
    private ControllerWindow? widget;
    private bool ready;
    public bool Exiting { get; set; }
    public bool AutomationMode { get; set; }
    public MainWindow(Preferences settings,Bridge bridge)
    {
        Settings=settings;Music=bridge;AppearanceTheme.Apply(settings.Theme);InitializeComponent();
        BlurSlider.Value=settings.Blur;DimSlider.Value=settings.Darkness;SizeSlider.Value=settings.CoverSize;
        InfoCheck.IsChecked=settings.ShowInfo;AutoCheck.IsChecked=settings.AutoApply;WidgetCheck.IsChecked=settings.Widget;RestoreCheck.IsChecked=settings.RestoreOnExit;FadeCheck.IsChecked=settings.SmoothTransitions;
        StartupCheck.IsChecked=Desktop.StartWithWindows;
        ResolutionBox.SelectedIndex=settings.Resolution=="4K"?2:settings.Resolution=="1080p"?1:0;
        ThemeBox.SelectedIndex=settings.Theme=="Dark"?2:settings.Theme=="Light"?1:0;
        Width=Math.Min(settings.StudioWidth,SystemParameters.WorkArea.Width);Height=Math.Min(settings.StudioHeight,SystemParameters.WorkArea.Height);
        if(settings.StudioLeft is double left&&settings.StudioTop is double top&&IsOnScreen(left,top,Width,Height))
        {WindowStartupLocation=WindowStartupLocation.Manual;Left=left;Top=top;}
        placementTimer.Tick+=(_,_)=>{placementTimer.Stop();RememberPlacement();};
        LocationChanged+=(_,_)=>QueuePlacement();SizeChanged+=(_,_)=>QueuePlacement();StateChanged+=(_,_)=>QueuePlacement();
        debounce.Tick+=(_,_)=>{debounce.Stop();SaveChoices();RenderPreview();if(Settings.AutoApply&&!AutomationMode&&Music.Connected&&!suspendWallpaper)_=ApplyWallpaperAsync();};
        connectionTimer.Tick+=(_,_)=>UpdateConnection();connectionTimer.Start();
        InitializeLyrics();InitializeNavigation();Music.TrackChanged+=OnTrack;ready=true;RenderPreview();UpdateConnection();
        Loaded+=(_,_)=>{if(!AutomationMode){if(Settings.StudioMaximized)WindowState=WindowState.Maximized;if(Settings.Widget)ShowWidget();}};
        Closing+=OnClosing;InitializeMotion();InitializeMaterials();MotionPolicy.SetEnabled(this,MotionAllowed);
    }
    private void OnTrack(Track incoming) => Dispatcher.InvokeAsync(()=>ReceiveTrackAsync(incoming));
    private async Task ReceiveTrackAsync(Track incoming)
    {
        if(Exiting)return;
        lyricPlayback.Update(incoming,lyricClock.Elapsed.TotalSeconds);track=incoming;UpdatePlayer();UpdateConnection();_ = RefreshLyricsAsync();
        if(incoming.Artwork!=loadedArtwork)
        {
            if(pendingArtwork==incoming.Artwork||DateTime.UtcNow<artworkRetryAfter)return;
            artworkRequest?.Cancel();artworkRequest?.Dispose();artworkRequest=new();var cancellation=artworkRequest.Token;
            if(incoming.Artwork=="") { Status.Text="Waiting for YouTube Music artwork. The last wallpaper is kept.";return; }
            pendingArtwork=incoming.Artwork;
            try
            {
                var image=await Artwork.DownloadAsync(incoming.Artwork,cancellation);
                if(cancellation.IsCancellationRequested||track.Artwork!=incoming.Artwork)return;
                art=image;loadedArtwork=incoming.Artwork;
                ArtworkQuality.Text=$"Source artwork · {image.PixelWidth} × {image.PixelHeight}";
            }
            catch(OperationCanceledException){return;}
            catch(Exception e){Status.Text="Could not load artwork: "+e.Message;artworkRetryAfter=DateTime.UtcNow.AddSeconds(10);return;}
            finally {if(pendingArtwork==incoming.Artwork)pendingArtwork="";}
        }
        var key=track.Title+"|"+track.Artist+"|"+track.Album+"|"+Math.Round(track.Duration)+"|"+track.Artwork;
        if(key!=renderKey)
        {
            renderKey=key;RenderPreview();
            if((Settings.AutoApply||NeedsLyricsMigration())&&!AutomationMode&&!suspendWallpaper)await ApplyWallpaperAsync();
        }
    }
    private void RenderPreview()
    {
        PreviewReady=RenderPreviewAsync();
    }
    private async Task RenderPreviewAsync()
    {
        var version=++previewRevision;previewRequest?.Cancel();previewRequest?.Dispose();previewRequest=new();
        var cancellation=previewRequest.Token;var image=art;var song=track;var choices=Settings.Snapshot();
        PreviewLabel.Text=(loadedArtwork.Length==0?"Style preview · ":"")+Settings.Preset+" preview";
        UpdateSelection();
        SizeSlider.IsEnabled=Settings.ShowLyrics||Settings.Preset is not ("Immersive" or "Backdrop");
        InfoCheck.IsEnabled=Settings.ShowLyrics||Settings.Preset!="Backdrop";
        BlurValue.Text=$"{Settings.Blur:0}%";DimValue.Text=$"{Settings.Darkness:0}%";SizeValue.Text=$"{Settings.CoverSize:0}%";
        UpdatePlayer();
        try
        {
            var frames=await RenderWorker.Wallpapers.Run(()=>
            {
                cancellation.ThrowIfCancellationRequested();
                BitmapSource Thumbnail(string preset)=>WallpaperRenderer.Render(image,song,new Preferences{Preset=preset,Blur=choices.Blur,Darkness=choices.Darkness,CoverSize=choices.CoverSize,ShowInfo=false},384,216);
                return new[]{WallpaperRenderer.Render(image,song,choices,1280,720),Thumbnail("Ambient"),Thumbnail("Gallery"),Thumbnail("Immersive"),Thumbnail("Backdrop")};
            },cancellation);
            if(cancellation.IsCancellationRequested||version!=previewRevision||Exiting)return;
            Preview.SetSource(frames[0],Settings.SmoothTransitions&&!AutomationMode);
            AmbientThumb.Source=frames[1];GalleryThumb.Source=frames[2];ImmersiveThumb.Source=frames[3];BackdropThumb.Source=frames[4];Atmosphere.Source=frames[4];
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(version==previewRevision)Status.Text="Preview could not be rendered: "+ex.Message;}
    }
    private void UpdateSelection()
    {
        foreach(var button in new[]{AmbientButton,GalleryButton,ImmersiveButton,BackdropButton})
        {button.SetResourceReference(Button.BackgroundProperty,Settings.Preset==button.Tag.ToString()?"SelectionBrush":"SurfaceBrush");}
    }
    private void SaveChoices(){if(AutomationMode)return;Settings.Save();SettingsSaved.Text="Saved on this PC";}
    private void ScheduleRender(){if(!ready)return;SettingsSaved.Text="Saving changes…";debounce.Stop();debounce.Start();}
    private void QueuePlacement(){if(!ready||AutomationMode||Exiting||windowMotion)return;placementTimer.Stop();placementTimer.Start();}
    private void RememberPlacement()
    {
        if(AutomationMode||windowMotion)return;
        var bounds=WindowState==WindowState.Normal?new Rect(Left,Top,ActualWidth,ActualHeight):RestoreBounds;
        if(!bounds.IsEmpty&&double.IsFinite(bounds.Left)&&bounds.Width>=MinWidth)
        {Settings.StudioLeft=bounds.Left;Settings.StudioTop=bounds.Top;Settings.StudioWidth=bounds.Width;Settings.StudioHeight=bounds.Height;}
        if(WindowState!=WindowState.Minimized)Settings.StudioMaximized=WindowState==WindowState.Maximized;
        SaveChoices();
    }
    internal static bool IsOnScreen(double left,double top,double width,double height)
    {
        if(!double.IsFinite(left)||!double.IsFinite(top))return false;
        foreach(var screen in System.Windows.Forms.Screen.AllScreens)
        {var area=screen.WorkingArea;if(left+width>area.Left+80&&left<area.Right-80&&top+height>area.Top+60&&top<area.Bottom-60)return true;}
        return false;
    }
    private void Theme_Changed(object sender,SelectionChangedEventArgs e)
    {if(!ready)return;Settings.Theme=ThemeBox.SelectedIndex==2?"Dark":ThemeBox.SelectedIndex==1?"Light":"System";AppearanceTheme.Apply(Settings.Theme);UpdateSelection();SaveChoices();}
    private void WindowClose_Click(object sender,RoutedEventArgs e)=>Close();
    private async void WindowMinimize_Click(object sender,RoutedEventArgs e)=>await AnimateMinimizeAsync();
    private async void WindowMaximize_Click(object sender,RoutedEventArgs e)=>await AnimateWindowSizeAsync();
    private void Appearance_Click(object sender,RoutedEventArgs e){Navigate("Appearance");ThemeBox.Focus();}
    private void MiniPlayer_Click(object sender,RoutedEventArgs e)=>WidgetCheck.IsChecked=WidgetCheck.IsChecked!=true;
    private void Search_KeyDown(object sender,KeyEventArgs e)
    {if(e.Key!=Key.Enter||string.IsNullOrWhiteSpace(SearchBox.Text))return;Open("https://music.youtube.com/search?q="+Uri.EscapeDataString(SearchBox.Text.Trim()));e.Handled=true;}
    private void Appearance_Changed(object sender,RoutedPropertyChangedEventArgs<double> e)
    {if(!ready)return;Settings.Blur=BlurSlider.Value;Settings.Darkness=DimSlider.Value;Settings.CoverSize=SizeSlider.Value;BlurValue.Text=$"{Settings.Blur:0}%";DimValue.Text=$"{Settings.Darkness:0}%";SizeValue.Text=$"{Settings.CoverSize:0}%";ScheduleRender();}
    private void Options_Changed(object sender,RoutedEventArgs e)
    {
        if(!ready)return;Settings.ShowInfo=InfoCheck.IsChecked==true;Settings.AutoApply=AutoCheck.IsChecked==true;Settings.Widget=WidgetCheck.IsChecked==true;Settings.RestoreOnExit=RestoreCheck.IsChecked==true;
        if(!Settings.AutoApply)CancelDesktopTransition();
        if(Settings.Widget)ShowWidget();else {widget?.Close();widget=null;}ScheduleRender();
    }
    private void Fade_Changed(object sender,RoutedEventArgs e)
    {if(!ready)return;Settings.SmoothTransitions=FadeCheck.IsChecked==true;ScheduleRender();}
    private void Startup_Changed(object sender,RoutedEventArgs e)
    {if(!ready)return;try {Desktop.StartWithWindows=StartupCheck.IsChecked==true;Status.Text="Windows startup preference saved.";}catch(Exception ex){Status.Text=ex.Message;}}
    private void Resolution_Changed(object sender,SelectionChangedEventArgs e)
    {if(!ready)return;Settings.Resolution=(ResolutionBox.SelectedItem as ComboBoxItem)?.Content?.ToString()??"Display";ScheduleRender();}
    private void Preset_Click(object sender,RoutedEventArgs e){Settings.Preset=((Button)sender).Tag.ToString()!;ScheduleRender();}
    private void Studio_Click(object sender,RoutedEventArgs e)=>Navigate("Studio");
    private void Music_Click(object sender,RoutedEventArgs e)=>Open("https://music.youtube.com/");
    internal static void Open(string path){Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
    private void Connect_Click(object sender,RoutedEventArgs e)=>ShowSetupGuide();
    internal void ShowSetupGuide(){Settings.SetupGuideSeen=true;SaveChoices();new ConnectWindow(Settings,Music){Owner=this}.ShowDialog();AutoCheck.IsChecked=Settings.AutoApply;}
    private void Player_Click(object sender,RoutedEventArgs e)=>SendCommand(((Button)sender).Tag.ToString()!);
    public void SendCommand(string action,double value=0)
    {Status.Text=Music.Send(action,value)?"Playback command sent to YouTube Music.":"Connect a playing YouTube Music tab to use the controls.";}
    private void Seek_Released(object sender,MouseButtonEventArgs e)
    {if(track.Duration>0)SendCommand("seek",ProgressSlider.Value*track.Duration);}
    private void Seek_KeyUp(object sender,KeyEventArgs e)
    {if(e.Key is Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End){if(track.Duration>0)SendCommand("seek",ProgressSlider.Value*track.Duration);e.Handled=true;}}
    private void UpdateConnection()
    {
        if(desktopTransition is not null&&wallpaperFade is null&&!suspendWallpaper)
        {
            // Release the idle overlay if the user changes Windows wallpaper,
            // a display disappears, or Explorer replaces the desktop host.
            try{using var desktop=new NativeWallpaper();if(!desktopTransition.Attached||!Desktop.IsOwned(desktop,Settings)){desktopTransition.Dispose();desktopTransition=null;}}
            catch{desktopTransition?.Dispose();desktopTransition=null;}
        }
        bool connected=Music.Connected;SidebarStatus.Text=connected?(track.Playing?"Connected · playing":"Connected · paused"):"Waiting for connection";
        ConnectionButton.Content=connected?"Connected":"Connect music";ConnectionNotice.Visibility=connected?Visibility.Collapsed:Visibility.Visible;HeroSection.Visibility=connected||loadedArtwork!=""?Visibility.Visible:Visibility.Collapsed;
        PreviousButton.IsEnabled=PlayButton.IsEnabled=NextButton.IsEnabled=connected;ProgressSlider.IsEnabled=connected&&track.Duration>0;
        widget?.SetConnected(connected);
    }
    private void UpdatePlayer()
    {
        HeroStateLabel.Text=Music.Connected?"NOW PLAYING":"STYLE PREVIEW";TrackTitle.Text=HeroTitle.Text=track.Title;TrackArtist.Text=HeroArtist.Text=track.Artist;HeroAlbum.Text=string.IsNullOrWhiteSpace(track.Album)?"YouTube Music":track.Album;
        MiniArt.SetSource(art,Settings.SmoothTransitions&&!AutomationMode);HeroArt.SetSource(art,Settings.SmoothTransitions&&!AutomationMode);PlayGlyph.Kind=track.Playing?"pause":"play";LyricsCover.SetSource(art,Settings.SmoothTransitions&&!AutomationMode);
        static string Time(double seconds)=>double.IsFinite(seconds)?$"{(int)Math.Max(0,seconds)/60}:{(int)Math.Max(0,seconds)%60:00}":"0:00";
        ElapsedTime.Text=Time(track.Position);TotalTime.Text=Time(track.Duration);
        if(!ProgressSlider.IsMouseOver&&!ProgressSlider.IsKeyboardFocusWithin)ProgressSlider.Value=track.Duration>0?Math.Clamp(track.Position/track.Duration,0,1):0;
        widget?.Update(track,art);
    }
    private void ShowWidget()
    {
        if(AutomationMode)return;
        if(widget is null){widget=new ControllerWindow(this);widget.Closed+=(_,_)=>{widget=null;if(!Exiting){Settings.Widget=false;WidgetCheck.IsChecked=false;Settings.Save();}};widget.Show();}
        widget.Update(track,art);widget.SetConnected(Music.Connected);
    }
    private (int width,int height) Dimensions()
    {
        if(Settings.Resolution=="4K")return(3840,2160);if(Settings.Resolution=="1080p")return(1920,1080);
        var bounds=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;return(Math.Clamp(bounds.Width,640,7680),Math.Clamp(bounds.Height,480,4320));
    }
    private async Task ApplyWallpaperAsync()
    {
        var revision=++applyRevision;wallpaperFade?.Cancel();
        await wallpaperGate.WaitAsync();
        string? staged=null;
        Task? encoding=null;
        CancellationTokenSource? fade=null;
        try
        {
            if(suspendWallpaper||revision!=applyRevision)return;
            fade=new CancellationTokenSource();wallpaperFade=fade;var cancellation=fade.Token;
            var (width,height)=Dimensions();var image=art;var song=track;var choices=Settings.Snapshot();
            var bitmap=await RenderWorker.Wallpapers.Run(()=>WallpaperRenderer.Render(image,song,choices,width,height),cancellation);
            cancellation.ThrowIfCancellationRequested();
            var path=Preferences.WallpaperPath;Directory.CreateDirectory(Preferences.WallpaperDirectory);
            bool owned;using(var desktop=new NativeWallpaper())owned=Settings.OwnsWallpaper&&Desktop.IsOwned(desktop,Settings);
            if(!owned||!Settings.SmoothTransitions||!MotionAllowed){desktopTransition?.Dispose();desktopTransition=null;}
            if(Settings.SmoothTransitions&&MotionAllowed&&owned&&desktopTransition is null)
            {
                var oldPath=string.IsNullOrEmpty(Settings.AppliedWallpaper)?Desktop.CurrentWallpaper():Settings.AppliedWallpaper;
                if(File.Exists(oldPath))
                {
                    try
                    {
                        var old=await RenderWorker.Wallpapers.Run(()=>LoadImage(oldPath),cancellation);
                        var epoch=desktopEpoch;var created=await DesktopTransition.TryCreateAsync(old);
                        if(epoch!=desktopEpoch){created?.Dispose();cancellation.ThrowIfCancellationRequested();return;}
                        desktopTransition=created;desktopLyrics?.KeepVisible();
                        cancellation.ThrowIfCancellationRequested();
                    }
                    catch(OperationCanceledException){throw;}
                    catch {desktopTransition?.Dispose();desktopTransition=null;}
                }
            }
            staged=Path.Combine(Preferences.WallpaperDirectory,"pending-"+Guid.NewGuid().ToString("N")+".png");
            var stagedPath=staged;
            encoding=Task.Run(()=>WallpaperRenderer.SaveDesktop(bitmap,stagedPath));
            cancellation.ThrowIfCancellationRequested();
            var faded=false;
            if(desktopTransition is not null)
            {
                Status.Text="Blending into the next wallpaper…";
                try {await desktopTransition.FadeAsync(bitmap,cancellation);faded=true;}
                catch(OperationCanceledException){throw;}
                catch {desktopTransition.Dispose();desktopTransition=null;}
            }
            await encoding;
            cancellation.ThrowIfCancellationRequested();
            File.Move(staged,path,true);staged=null;Desktop.Apply(path,Settings);
            ownsLyricLayout=choices.ShowLyrics;appliedLyricKey=LyricsProvider.Key(song);Settings.AppliedLyricsKey=choices.ShowLyrics?appliedLyricKey:"";Settings.Save();ownershipCheck=DateTime.MinValue;layerRetry=DateTime.MinValue;TickLyrics();
            Status.Text=$"Wallpaper applied · {width} × {height} · {Settings.Preset}";
            if(faded)
            {
                // Keep the final frame over Explorer's asynchronous native repaint.
                // A newer request can reuse this surface immediately without flashing back.
                await desktopTransition!.HoldAsync(cancellation);
            }
            // Retain the completed surface between tracks. There is no
            // create/destroy handoff to Explorer on every song change.
            if(Settings.SmoothTransitions&&MotionAllowed&&owned&&!faded)
                Status.Text+=" · Desktop fade unavailable; preview fade remains enabled.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){desktopTransition?.Dispose();desktopTransition=null;Status.Text="Wallpaper could not be applied: "+ex.Message;}
        finally
        {
            if((suspendWallpaper||!Settings.SmoothTransitions||Exiting)&&desktopTransition is not null)
            {desktopTransition.Dispose();desktopTransition=null;}
            if(staged is not null)
            {if(encoding is not null)try{await encoding;}catch{}try{File.Delete(staged);}catch{}}
            if(ReferenceEquals(wallpaperFade,fade))wallpaperFade=null;
            fade?.Dispose();wallpaperGate.Release();
        }
    }
    private void CancelDesktopTransition()
    {RemoveDesktopLyrics();desktopEpoch++;applyRevision++;wallpaperFade?.Cancel();desktopTransition?.Dispose();desktopTransition=null;}
    private async void TestFade_Click(object sender,RoutedEventArgs e)
    {
        if(AutomationMode||suspendWallpaper)return;
        if(!MotionAllowed){Status.Text="Desktop animations are disabled in Windows.";return;}
        suspendWallpaper=true;CancelDesktopTransition();await wallpaperGate.WaitAsync();
        using var cancellation=new CancellationTokenSource();wallpaperFade=cancellation;DesktopTransition? demonstration=null;var wasVisible=IsVisible;
        try
        {
            var current=await RenderWorker.Wallpapers.Run(()=>LoadImage(Preferences.WallpaperPath),cancellation.Token);
            demonstration=await DesktopTransition.TryCreateAsync(current);
            if(demonstration is null)throw new InvalidOperationException("Desktop fade could not start. "+DesktopTransition.LastFailure);
            var choices=Settings.Snapshot();
            var sample=await RenderWorker.Wallpapers.Run(()=>WallpaperRenderer.Render(Artwork.Demo(),new Track{Title="A softer transition",Artist="Afterglow desktop fade preview"},choices,current.PixelWidth,current.PixelHeight),cancellation.Token);
            Hide();await demonstration.FadeAsync(sample,cancellation.Token);await Task.Delay(350,cancellation.Token);
            await demonstration.FadeAsync(current,cancellation.Token);await Task.Delay(200,cancellation.Token);
            Status.Text="Desktop fade preview finished. Your wallpaper and settings were kept.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){Status.Text="Desktop fade preview: "+ex.Message;}
        finally
        {
            demonstration?.Dispose();wallpaperFade=null;wallpaperGate.Release();suspendWallpaper=false;
            if(wasVisible&&!Exiting)ShowStudio();
            if(Settings.AutoApply&&Music.Connected&&!Exiting)await ApplyWallpaperAsync();
        }
    }
    private async void Apply_Click(object sender,RoutedEventArgs e)=>await ApplyWallpaperAsync();
    private async void Restore_Click(object sender,RoutedEventArgs e)
    {
        ownsLyricLayout=false;AutoCheck.IsChecked=false;CancelDesktopTransition();await wallpaperGate.WaitAsync();
        try{Status.Text="Automatic updates off. "+Desktop.Restore(Settings);}
        catch(Exception ex){Status.Text="Could not restore wallpaper: "+ex.Message;}
        finally {wallpaperGate.Release();}
    }
    private static BitmapSource LoadImage(string path)
    {using var stream=File.OpenRead(path);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}
    private async void Export_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG wallpaper|*.png",FileName="Afterglow-"+Settings.Preset+".png"};
        if(dialog.ShowDialog(this)!=true)return;
        try{var(w,h)=Dimensions();var image=art;var song=track;var choices=Settings.Snapshot();var bitmap=await RenderWorker.Wallpapers.Run(()=>WallpaperRenderer.Render(image,song,choices,w,h));await Task.Run(()=>WallpaperRenderer.Save(bitmap,dialog.FileName));Status.Text="Wallpaper exported to "+dialog.FileName;}
        catch(Exception ex){Status.Text="Export failed: "+ex.Message;}
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {RememberPlacement();if(!Exiting){e.Cancel=true;Hide();return;}connectionTimer.Stop();debounce.Stop();placementTimer.Stop();previewRequest?.Cancel();artworkRequest?.Cancel();widget?.Close();}
    public void ShowStudio(){Show();if(WindowState==WindowState.Minimized)WindowState=Settings.StudioMaximized?WindowState.Maximized:WindowState.Normal;Activate();}
    public async Task PrepareExitAsync()
    {
        RememberPlacement();Exiting=true;StopLyrics();suspendWallpaper=true;Music.TrackChanged-=OnTrack;artworkRequest?.Cancel();previewRequest?.Cancel();debounce.Stop();CancelDesktopTransition();placementTimer.Stop();
        await wallpaperGate.WaitAsync();wallpaperGate.Release();
    }
}
