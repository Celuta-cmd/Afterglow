using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Afterglow;

public sealed record PlayerCommand(long Id, string Action, double Value);
public sealed class Bridge : IAsyncDisposable
{
    private WebApplication? server;
    private readonly string token;
    private readonly object gate = new();
    private readonly List<PlayerCommand> commands = new();
    private long sequence;
    public Track Current { get; private set; } = new();
    public DateTime LastSeen { get; private set; } = DateTime.MinValue;
    public bool Connected => DateTime.UtcNow - LastSeen < TimeSpan.FromSeconds(8);
    public event Action<Track>? TrackChanged;
    public Bridge(string token) { this.token = token; }
    public async Task StartAsync(int port = 47836)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}").ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 16384);
        server = builder.Build();
        server.Use(async (context, next) =>
        {
            var supplied = context.Request.Headers["X-Afterglow-Token"].ToString();
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(token)))
            { context.Response.StatusCode = 401; return; }
            await next(context);
        });
        server.MapGet("/health", () => Results.Json(new { name = "Afterglow", version = 1 }));
        server.MapPost("/state", async (HttpContext context) =>
        {
            Track? input;
            try { input = await context.Request.ReadFromJsonAsync<Track>(Preferences.Json); }
            catch { return Results.BadRequest(); }
            if (input is null || !IsValid(input)) return Results.BadRequest();
            PlayerCommand[] pending;
            lock (gate)
            {
                // Pin one tab. A playing tab may replace a paused tab; a stale tab releases ownership.
                if (Connected && Current.Source != input.Source && (Current.Playing || !input.Playing))
                    return Results.Json(new { commands = Array.Empty<PlayerCommand>(), selected = false });
                if (Current.Source != input.Source) commands.Clear();
                Current = input; LastSeen = DateTime.UtcNow;
                pending = commands.ToArray(); commands.Clear();
            }
            TrackChanged?.Invoke(input);
            return Results.Json(new { commands = pending, selected = true });
        });
        await server.StartAsync();
    }
    public static bool IsValid(Track t) => t.Source is { Length: > 0 and < 100 } && t.Title is { Length: <= 500 } && t.Artist is { Length: <= 500 } && t.Album is { Length: <= 500 } && t.Artwork is { Length: <= 2048 } &&
        Uri.TryCreate(t.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "music.youtube.com" &&
        (t.Artwork == "" || IsArtworkUrl(t.Artwork)) && double.IsFinite(t.Position) && double.IsFinite(t.Duration) && t.Position >= 0 && t.Duration >= 0 && t.Duration < 604800;
    public static bool IsArtworkUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" &&
        (u.Host == "lh3.googleusercontent.com" || u.Host == "yt3.googleusercontent.com" || u.Host == "i.ytimg.com" || u.Host.EndsWith(".ytimg.com", StringComparison.Ordinal) || u.Host.EndsWith(".ggpht.com", StringComparison.Ordinal));
    public bool Send(string action, double value = 0)
    {
        if (action is not ("toggle" or "previous" or "next" or "seek")) return false;
        lock (gate)
        {
            if (!Connected || commands.Count >= 8) return false;
            commands.Add(new(++sequence, action, value)); return true;
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (server is not null) { await server.StopAsync(); await server.DisposeAsync(); }
    }
}
