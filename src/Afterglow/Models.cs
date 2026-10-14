using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Afterglow;

public sealed record Track
{
    public string Title { get; init; } = "Your music, in a new light.";
    public string Artist { get; init; } = "Connect YouTube Music to get started";
    public string Album { get; init; } = "";
    public string Artwork { get; init; } = "";
    public string Source { get; init; } = "";
    public string Url { get; init; } = "";
    public bool Playing { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public double PlaybackRate { get; init; } = 1;
}

public sealed class Preferences
{
    public string Token { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    public string Preset { get; set; } = "Ambient";
    public double Blur { get; set; } = 62;
    public double Darkness { get; set; } = 26;
    public double CoverSize { get; set; } = 54;
    public bool ShowInfo { get; set; } = true;
    public bool ShowLyrics { get; set; }
    public double LyricsOffset { get; set; }
    public bool AutoApply { get; set; }
    public bool SmoothTransitions { get; set; } = true;
    public bool Widget { get; set; }
    public bool RestoreOnExit { get; set; } = true;
    public string Theme { get; set; } = "System";
    public string SelectedSection { get; set; } = "Studio";
    public bool SidebarCollapsed { get; set; }
    public bool SetupGuideSeen { get; set; }
    public bool SetupCompleted { get; set; }
    public string SetupBrowserName { get; set; } = "";
    public double StudioWidth { get; set; } = 1260;
    public double StudioHeight { get; set; } = 860;
    public double? StudioLeft { get; set; }
    public double? StudioTop { get; set; }
    public bool StudioMaximized { get; set; }
    public double? ControllerLeft { get; set; }
    public double? ControllerTop { get; set; }
    public bool ControllerPinned { get; set; }
    public string Resolution { get; set; } = "Display";
    public string OriginalWallpaper { get; set; } = "";
    public string OriginalStyle { get; set; } = "10";
    public string OriginalTile { get; set; } = "0";
    public bool OwnsWallpaper { get; set; }
    public WallpaperSnapshot? WallpaperSnapshot { get; set; }
    public string AppliedWallpaper { get; set; } = "";
    public string AppliedLyricsKey { get; set; } = "";

    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Afterglow");
    // Windows' wallpaper service must read these files outside an app's virtualized AppData.
    public static readonly string WallpaperDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Afterglow");
    public static readonly string WallpaperPath = Path.Combine(WallpaperDirectory,"wallpaper.png");
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly object SaveGate=new();
    internal Preferences Snapshot()=>(Preferences)MemberwiseClone();
    public static Preferences Load(string? directory=null)
    {
        var folder=directory??DirectoryPath;
        foreach(var name in new[]{"settings.json","settings.json.bak"})
        {
            try
            {
                var savedJson=File.ReadAllText(Path.Combine(folder,name));
                var p = JsonSerializer.Deserialize<Preferences>(savedJson, Json);
                if(p is null||p.Token is null||p.Token.Length!=48)continue;
                // Existing users can reopen setup from Connection without an upgrade interrupting them.
                using(var document=JsonDocument.Parse(savedJson))
                    if(!document.RootElement.TryGetProperty("setupGuideSeen",out _))p.SetupGuideSeen=true;
                if(p.SelectedSection is not ("Studio" or "Appearance" or "Lyrics" or "Playback" or "Connection"))p.SelectedSection="Studio";
                p.LyricsOffset=double.IsFinite(p.LyricsOffset)?Math.Clamp(p.LyricsOffset,-5,5):0;
                p.Blur=double.IsFinite(p.Blur)?Math.Clamp(p.Blur,0,100):62;
                p.Darkness=double.IsFinite(p.Darkness)?Math.Clamp(p.Darkness,0,100):26;
                p.CoverSize=double.IsFinite(p.CoverSize)?Math.Clamp(p.CoverSize,25,80):54;
                if(p.Preset is not ("Ambient" or "Immersive" or "Gallery" or "Backdrop"))p.Preset="Ambient";
                if(p.Theme is not ("Light" or "Dark" or "System"))p.Theme="System";
                if(p.Resolution is not ("Display" or "1080p" or "4K"))p.Resolution="Display";
                p.StudioWidth=double.IsFinite(p.StudioWidth)?Math.Clamp(p.StudioWidth,920,3840):1260;
                p.StudioHeight=double.IsFinite(p.StudioHeight)?Math.Clamp(p.StudioHeight,660,2160):860;
                return p;
            }
            catch {}
        }
        return new();
    }
    public void Save(string? directory=null)
    {
        lock(SaveGate)
        {
            var folder=directory??DirectoryPath;Directory.CreateDirectory(folder);
            var path=Path.Combine(folder,"settings.json");
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,Json));
            if(File.Exists(path))
            {
                try{var prior=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path),Json);if(prior?.Token?.Length==48)File.Copy(path,path+".bak",true);}catch{}
            }
            File.Move(path+".tmp",path,true);
        }
    }
}
