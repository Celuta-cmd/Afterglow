using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Afterglow;

internal sealed record LyricLine(double Time, string Text);
internal sealed record LyricResult(LyricLine[] Lines, string Status);
internal sealed class LyricsProvider
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 512 * 1024 };
    private readonly Dictionary<string, (DateTime Time, LyricResult Result)> cache = new();
    internal static string Key(Track t) => $"{t.Title}|{t.Artist}|{t.Album}|{Math.Round(t.Duration)}";
    internal static string Normalize(string s) => string.Concat(s.Normalize().Where(char.IsLetterOrDigit)).ToLowerInvariant();
    internal static bool Matches(Track t, string title, string artist, double duration) =>
        Normalize(t.Title) == Normalize(title) && Normalize(t.Artist) == Normalize(artist) &&
        double.IsFinite(duration) && Math.Abs(t.Duration - duration) <= 2;
    internal static LyricLine[] Parse(string? lrc)
    {
        if (string.IsNullOrEmpty(lrc) || lrc.Length > 200000) return Array.Empty<LyricLine>();
        var lines = new List<LyricLine>();
        double offset = 0;
        var offsetMatch = Regex.Match(lrc, @"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase);
        if (offsetMatch.Success && double.TryParse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var ms)) offset = Math.Clamp(ms / 1000, -30, 30);
        foreach (var row in lrc.Split('\n'))
        {
            var stamps = Regex.Matches(row, @"\[(\d{1,3}):(\d{2}(?:\.\d{1,3})?)\]");
            if (stamps.Count == 0) continue;
            var text = Regex.Replace(row, @"\[[^\]]*\]|<\d{1,3}:\d{2}(?:\.\d{1,3})?>", "").Trim();
            foreach (Match stamp in stamps)
            {
                var seconds = double.Parse(stamp.Groups[2].Value, CultureInfo.InvariantCulture);
                if (seconds >= 60) continue;
                var time = int.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + seconds + offset;
                if (time >= 0 && time < 604800) lines.Add(new(time, text.Length > 1000 ? text[..1000] : text));
                if (lines.Count >= 2000) return lines.OrderBy(l => l.Time).ToArray();
            }
        }
        // Combine simultaneous vocals instead of dropping one of the lines.
        return lines.OrderBy(l => l.Time).GroupBy(l => l.Time).Select(g => new LyricLine(g.Key, string.Join("\n", g.Select(l => l.Text).Distinct()))).ToArray();
    }
    internal static int Index(LyricLine[] lines, double position)
    {
        int low = 0, high = lines.Length - 1, result = -1;
        while (low <= high) { var mid = (low + high) / 2; if (lines[mid].Time <= position) { result = mid; low = mid + 1; } else high = mid - 1; }
        return result;
    }
    internal async Task<LyricResult> FetchAsync(Track track, CancellationToken cancellation)
    {
        var key = Key(track);
        if (cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.Time < TimeSpan.FromMinutes(hit.Result.Lines.Length > 0 ? 60 : 5)) return hit.Result;
        if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist) || track.Duration <= 0) return new(Array.Empty<LyricLine>(), "Waiting for song details");
        string Q(string value) => Uri.EscapeDataString(value);
        var url = $"https://lrclib.net/api/get?track_name={Q(track.Title)}&artist_name={Q(track.Artist)}&album_name={Q(track.Album)}&duration={track.Duration.ToString("0.###", CultureInfo.InvariantCulture)}";
        LyricResult result;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Afterglow/1.5.0");
            using var response = await Client.SendAsync(request, cancellation);
            if (response.StatusCode == HttpStatusCode.NotFound) result = new(Array.Empty<LyricLine>(), "No matching lyrics on LRCLIB");
            else
            {
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
                var data = json.RootElement;
                if (!Matches(track, data.GetProperty("trackName").GetString() ?? "", data.GetProperty("artistName").GetString() ?? "", data.GetProperty("duration").GetDouble())) result = new(Array.Empty<LyricLine>(), "Lyrics match rejected: different recording");
                else if (data.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True) result = new(Array.Empty<LyricLine>(), "Instrumental track");
                else
                {
                    var lines = Parse(data.TryGetProperty("syncedLyrics", out var synced) && synced.ValueKind == JsonValueKind.String ? synced.GetString() : null);
                    result = new(lines, lines.Length > 0 ? "Line-synced lyrics · LRCLIB" : "Synced lyrics unavailable for this song");
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch { result = new(Array.Empty<LyricLine>(), "Lyrics service unavailable. Try again later."); }
        cancellation.ThrowIfCancellationRequested();
        if (cache.Count >= 64) cache.Remove(cache.OrderBy(p => p.Value.Time).First().Key);
        cache[key] = (DateTime.UtcNow, result); return result;
    }
}
