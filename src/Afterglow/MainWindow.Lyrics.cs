using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Afterglow;

public partial class MainWindow
{
    private readonly LyricsProvider lyricsProvider = new();
    private readonly Stopwatch lyricClock = Stopwatch.StartNew();
    private readonly LyricPlaybackClock lyricPlayback = new();
    private readonly DispatcherTimer lyricTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private CancellationTokenSource? lyricRequest;
    private LyricLine[] lyricLines = Array.Empty<LyricLine>();
    private string lyricKey = "", appliedLyricKey = "";
    private DateTime lyricRetry = DateTime.MinValue, ownershipCheck = DateTime.MinValue, layerRetry = DateTime.MinValue;
    private DesktopLyrics? desktopLyrics;
    private bool ownsLyricLayout;

    private void InitializeLyrics()
    {
        ownsLyricLayout=Settings.ShowLyrics&&Settings.OwnsWallpaper&&!string.IsNullOrEmpty(Settings.AppliedLyricsKey);
        appliedLyricKey=Settings.AppliedLyricsKey;
        LyricsCheck.IsChecked = Settings.ShowLyrics; LyricsOffsetSlider.Value = Settings.LyricsOffset;
        LyricsOffsetLabel.Text = $"{Settings.LyricsOffset:+0.00;-0.00;0.00} s · positive advances lyrics";
        lyricTimer.Tick += (_, _) => TickLyrics(); lyricTimer.Start();
    }
    private bool NeedsLyricsMigration()
    {
        if (!Settings.ShowLyrics || !Settings.OwnsWallpaper || Settings.AppliedLyricsKey != "") return false;
        try { using var desktop=new NativeWallpaper();return Desktop.IsOwned(desktop,Settings); } catch { return false; }
    }
    private async Task RefreshLyricsAsync()
    {
        if (!Settings.ShowLyrics || AutomationMode || Exiting) return;
        var key = LyricsProvider.Key(track);
        if (lyricKey == key && DateTime.UtcNow < lyricRetry) return;
        lyricRequest?.Cancel(); lyricRequest?.Dispose(); lyricRequest = new(); var cancellation = lyricRequest.Token;
        lyricKey = key; lyricRetry = DateTime.MaxValue; lyricLines = Array.Empty<LyricLine>();
        LyricPreview.Visibility = Visibility.Collapsed;LyricsStage.Visibility=Visibility.Collapsed;LyricsEmpty.Visibility=Visibility.Visible; RemoveDesktopLyrics(); LyricsStatus.Text = "Finding synced lyrics…";
        try
        {
            var result = await lyricsProvider.FetchAsync(track, cancellation);
            if (cancellation.IsCancellationRequested || !Settings.ShowLyrics || Exiting || key != LyricsProvider.Key(track)) return;
            lyricLines = result.Lines; LyricsStatus.Text = result.Status;
            if (lyricLines.Length == 0) lyricRetry = DateTime.UtcNow.AddMinutes(5);
            TickLyrics();
        }
        catch (OperationCanceledException) { }
    }
    private void TickLyrics()
    {
        if (AutomationMode) return;
        if (!Settings.ShowLyrics || Exiting || !Music.Connected || lyricLines.Length == 0 || lyricKey != LyricsProvider.Key(track))
        { LyricPreview.Visibility = Visibility.Collapsed;LyricsStage.Visibility=Visibility.Collapsed;LyricsEmpty.Visibility=Visibility.Visible; RemoveDesktopLyrics(); return; }
        var position = Math.Clamp(lyricPlayback.Position(lyricClock.Elapsed.TotalSeconds) + Settings.LyricsOffset, 0, Math.Max(0, track.Duration));
        var index = LyricsProvider.Index(lyricLines, position);
        LyricPreview.Visibility = Visibility.Visible; LyricPreview.Update(lyricLines, index, Settings.SmoothTransitions);LyricsStage.Visibility=Visibility.Visible;LyricsEmpty.Visibility=Visibility.Collapsed;LyricsStage.Update(lyricLines,index,Settings.SmoothTransitions);
        if (!ownsLyricLayout || appliedLyricKey != lyricKey || suspendWallpaper || AutomationMode) { RemoveDesktopLyrics(); return; }
        if (DateTime.UtcNow >= ownershipCheck)
        {
            ownershipCheck = DateTime.UtcNow.AddSeconds(2);
            try { using var desktop = new NativeWallpaper(); if (!Desktop.IsOwned(desktop, Settings)) { ownsLyricLayout = false; RemoveDesktopLyrics(); return; } }
            catch { RemoveDesktopLyrics(); return; }
            desktopLyrics?.KeepVisible();
        }
        if (desktopLyrics is not null && !desktopLyrics.Attached) { RemoveDesktopLyrics(); layerRetry = DateTime.UtcNow.AddSeconds(5); }
        if (desktopLyrics is not null && desktopLyrics.Failure != "") { LyricsStatus.Text="Desktop lyrics: "+desktopLyrics.Failure;RemoveDesktopLyrics();layerRetry=DateTime.UtcNow.AddSeconds(30); }
        if (desktopLyrics is null && DateTime.UtcNow >= layerRetry)
        {
            try { desktopLyrics = new DesktopLyrics(); }
            catch { LyricsStatus.Text = "Desktop lyrics unavailable. App preview still works."; layerRetry = DateTime.UtcNow.AddSeconds(30); }
        }
        desktopLyrics?.Update(lyricLines, index, Settings.SmoothTransitions);
    }
    private void RemoveDesktopLyrics() { desktopLyrics?.Dispose(); desktopLyrics = null; }
    private async void Lyrics_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        Settings.ShowLyrics = LyricsCheck.IsChecked == true;
        lyricRequest?.Cancel(); lyricKey = ""; lyricLines = Array.Empty<LyricLine>(); RemoveDesktopLyrics();
        LyricPreview.Visibility = Visibility.Collapsed;LyricsStage.Visibility=Visibility.Collapsed;LyricsEmpty.Visibility=Visibility.Visible; LyricsStatus.Text = Settings.ShowLyrics ? "Play a song to load lyrics" : "Lyrics off";
        SaveChoices(); ScheduleRender();
        if (Settings.ShowLyrics) await RefreshLyricsAsync();
        // Explicitly update an existing wallpaper when changing this setting,
        // even if automatic following is off. Never claim another app's wallpaper.
        if (!Settings.AutoApply && Settings.OwnsWallpaper && !AutomationMode)
        {
            using var desktop = new NativeWallpaper();
            if (Desktop.IsOwned(desktop, Settings)) await ApplyWallpaperAsync();
        }
    }
    private void LyricsOffset_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        Settings.LyricsOffset = LyricsOffsetSlider.Value;
        LyricsOffsetLabel.Text = $"{Settings.LyricsOffset:+0.00;-0.00;0.00} s · positive advances lyrics";
        SaveChoices(); TickLyrics();
    }
    private void StopLyrics()
    { lyricTimer.Stop(); lyricRequest?.Cancel(); lyricRequest?.Dispose(); lyricRequest = null; RemoveDesktopLyrics(); }
    internal void PreviewLyricsDemo()
    {
        // Original fixture text, not a downloaded song's copyrighted lyrics.
        LyricPreview.Visibility = Visibility.Visible;
        var fixture=new[] { new LyricLine(0, "Let the colors settle"), new LyricLine(4, "Your music lights up the room and carries us through the night"), new LyricLine(8, "And the night rolls on") };LyricPreview.Update(fixture,1,false);LyricsStage.Visibility=Visibility.Visible;LyricsEmpty.Visibility=Visibility.Collapsed;LyricsStage.Update(fixture,1,false);
        LyricsStatus.Text = "Lyrics preview · original sample text";
    }
}
