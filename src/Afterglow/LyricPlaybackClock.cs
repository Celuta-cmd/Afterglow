using System;

namespace Afterglow;

// Bridge packets can arrive just behind the interpolated display clock. Do not
// let that small correction replay the preceding phrase at a timestamp boundary.
internal sealed class LyricPlaybackClock
{
    private Track? track;
    private double anchor, received;
    private static string Identity(Track t) => $"{t.Source}|{t.Url}|{t.Title}|{t.Artist}|{t.Album}";
    internal void Update(Track incoming, double now)
    {
        var predicted = Position(now);
        var continuous = track is not null && track.Playing && incoming.Playing && Identity(track) == Identity(incoming);
        anchor = continuous && incoming.Position < predicted && predicted - incoming.Position < .75 ? predicted : incoming.Position;
        received = now; track = incoming;
    }
    internal double Position(double now)
    {
        if (track is null) return 0;
        var elapsed = track.Playing ? Math.Clamp(now - received, 0, 2.5) * track.PlaybackRate : 0;
        return Math.Clamp(anchor + elapsed, 0, Math.Max(0, track.Duration));
    }
}
