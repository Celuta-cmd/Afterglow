using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Afterglow;

// Preview and desktop share one layout. Each phrase keeps its own font size
// and identity as it moves from upcoming to active to previous.
internal sealed record LyricPlacement(int Id, string Text, double Y, double Height, double FontSize, double Opacity);
internal static class LyricLayout
{
    internal static TextBlock Label(string text, double width, double size) => new()
    {
        Text = text, Width = width, FontSize = size, Foreground = Brushes.White,
        FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
        TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None
    };
    internal static LyricPlacement[] Create(LyricLine[] lines, int index, double width, double height)
    {
        if (width <= 0 || height <= 0) return Array.Empty<LyricPlacement>();
        var items = new List<LyricPlacement>();
        for (int id = index - 1; id <= index + 1; id++)
        {
            if (id < -1 || id >= lines.Length || (id == -1 && index != -1)) continue;
            var text = id == -1 || string.IsNullOrWhiteSpace(lines[id].Text) ? "• • •" : lines[id].Text;
            var size = Math.Clamp(height * .105, 16, 144);
            var label = Label(text, width, size); label.Measure(new Size(width, double.PositiveInfinity));
            while (label.DesiredSize.Height > height * .78 && size > 6)
            { size = Math.Max(6, size * .9); label.FontSize = size; label.Measure(new Size(width, double.PositiveInfinity)); }
            items.Add(new(id, text, 0, label.DesiredSize.Height, size, id == index ? 1 : id < index ? .28 : .38));
        }
        var active = items.Find(p => p.Id == index);
        if (active is null) return Array.Empty<LyricPlacement>();
        var center = (height - active.Height) / 2; var gap = Math.Clamp(height * .105, 16, 144) * .55;
        return items.Select(p => p with { Y = p.Id == index ? center : p.Id < index ? center - gap - p.Height : center + active.Height + gap }).ToArray();
    }
}

public sealed class LyricsView : Canvas
{
    internal static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(650);
    private readonly Dictionary<int, TextBlock> labels = new();
    private LyricLine[]? lines;
    private int index = int.MinValue;
    internal bool ActiveTextFits => labels.TryGetValue(index, out var label) && label.DesiredSize.Height <= ActualHeight && label.TextTrimming == TextTrimming.None;
    internal double ActiveHeight => labels.TryGetValue(index, out var label) ? label.DesiredSize.Height : 0;
    internal int VisualCount => labels.Count;
    internal TextBlock? LabelFor(int id) => labels.GetValueOrDefault(id);
    public LyricsView()
    {
        IsHitTestVisible = false; ClipToBounds = true;
        SizeChanged += (_, _) => { if (lines is not null) Present(false, false); };
        OpacityMask = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Colors.White, .10), new(Colors.White, .90), new(Colors.Transparent, 1) }, 90);
    }
    internal void Update(LyricLine[] incoming, int active, bool animate)
    {
        if (ReferenceEquals(lines, incoming) && index == active) return;
        var reset = !ReferenceEquals(lines, incoming);
        lines = incoming; index = active;
        Present(animate && !reset && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast, reset);
    }
    private void Present(bool animate, bool reset)
    {
        if (lines is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var scene = LyricLayout.Create(lines, index, ActualWidth, ActualHeight);
        if (reset) { Children.Clear(); labels.Clear(); }
        // Sample displayed values before replacing animations. A retained phrase
        // gets one visual, rather than two overlapping snapshots.
        var shift = scene.Where(p => labels.ContainsKey(p.Id)).Select(p => p.Y - ((TranslateTransform)labels[p.Id].RenderTransform).Y).FirstOrDefault();
        foreach (var p in scene)
        {
            var created = !labels.TryGetValue(p.Id, out var label);
            if (created)
            {
                label = LyricLayout.Label(p.Text, ActualWidth, p.FontSize);
                label.RenderTransform = new TranslateTransform(0, animate ? p.Y - shift : p.Y);
                label.Opacity = animate ? 0 : p.Opacity; labels.Add(p.Id, label); Children.Add(label);
            }
            label!.Width = ActualWidth; label.FontSize = p.FontSize; label.Measure(new Size(ActualWidth, double.PositiveInfinity));
            Move(label, p.Y, p.Opacity, animate);
        }
        foreach (var entry in labels.ToArray())
        {
            if (scene.Any(p => p.Id == entry.Key)) continue;
            var label = entry.Value;
            if (!animate || label.Opacity < .001) { Children.Remove(label); labels.Remove(entry.Key); continue; }
            if ((double)label.GetAnimationBaseValue(OpacityProperty) == 0) continue;
            Move(label, ((TranslateTransform)label.RenderTransform).Y + shift, 0, true, () =>
            {
                if (label.Opacity < .001 && labels.GetValueOrDefault(entry.Key) == label)
                { Children.Remove(label); labels.Remove(entry.Key); }
            });
        }
    }
    private static DoubleAnimation? Move(TextBlock label, double y, double opacity, bool animate, Action? completed = null)
    {
        var transform = (TranslateTransform)label.RenderTransform;
        var fromY = transform.Y; var fromOpacity = label.Opacity;
        transform.BeginAnimation(TranslateTransform.YProperty, null); label.BeginAnimation(OpacityProperty, null);
        transform.Y = y; label.Opacity = opacity;
        if (!animate) return null;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, y, TransitionDuration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        var fade = new DoubleAnimation(fromOpacity, opacity, TransitionDuration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        if (completed is not null) fade.Completed += (_, _) => completed();
        label.BeginAnimation(OpacityProperty, fade); return fade;
    }
}
