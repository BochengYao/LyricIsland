using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LyricHover.Core;

namespace LyricHover.App.Modules
{
    /// <summary>
    /// Renders one lyric line as a dim base plus a full-brightness overlay whose clip
    /// follows the provider's real word timing. Playback is projected between media
    /// snapshots so the highlight remains smooth without polling the media session at
    /// display-frame frequency.
    /// </summary>
    public sealed class WordTrackingTextBlock : Grid
    {
        private static readonly TimeSpan MaximumContinuousAnchorDrift = TimeSpan.FromMilliseconds(500);
        private const int MaximumCachedVisualWordMaps = 128;
        private static readonly object VisualWordMapCacheSync = new object();
        private static readonly Dictionary<VisualWordMapCacheKey, VisualWordMap> VisualWordMapCache =
            new Dictionary<VisualWordMapCacheKey, VisualWordMap>();
        private static readonly Queue<VisualWordMapCacheKey> VisualWordMapCacheOrder =
            new Queue<VisualWordMapCacheKey>();
        private readonly TextBlock dimText = new TextBlock();
        private readonly TextBlock highlightText = new TextBlock();
        private readonly RectangleGeometry highlightClip = new RectangleGeometry();
        private LyricLine trackingLine;
        private TimeSpan anchorPosition;
        private long anchorTimestamp;
        private bool playbackAdvancing;
        private bool renderingSubscribed;
        private double displayedProgress = -1;
        private double dimOpacity = 0.45;
        private IReadOnlyList<VisualWordSpan> visualWordSpans = Array.Empty<VisualWordSpan>();
        private double measuredTextWidth;

        public WordTrackingTextBlock()
        {
            dimText.IsHitTestVisible = false;
            highlightText.IsHitTestVisible = false;
            Children.Add(dimText);
            Children.Add(highlightText);

            Loaded += (sender, args) => UpdateRenderingSubscription();
            Unloaded += (sender, args) => SetRenderingSubscribed(false);
            IsVisibleChanged += (sender, args) => UpdateRenderingSubscription();
            SizeChanged += (sender, args) => ApplyClip(displayedProgress);
            Foreground = Brushes.White;
        }

        public string Text
        {
            get => highlightText.Text;
            set
            {
                var normalized = value ?? string.Empty;
                if (highlightText.Text == normalized) return;
                dimText.Text = normalized;
                highlightText.Text = normalized;

                // Text is exposed as a CLR wrapper over the two child TextBlocks rather
                // than as a dependency property on this Grid. Invalidate this wrapper
                // explicitly so callers that synchronously measure the containing lyric
                // line cannot observe the previous line's DesiredSize.
                dimText.InvalidateMeasure();
                highlightText.InvalidateMeasure();
                InvalidateMeasure();
            }
        }

        public Brush Foreground
        {
            get => highlightText.Foreground;
            set
            {
                var normalized = value ?? Brushes.White;
                highlightText.Foreground = normalized;
                dimText.Foreground = CreateDimBrush(normalized, dimOpacity);
            }
        }

        public double DimOpacity
        {
            get => dimOpacity;
            set
            {
                dimOpacity = Math.Max(0, Math.Min(1, value));
                dimText.Foreground = CreateDimBrush(Foreground, dimOpacity);
            }
        }

        public FontFamily FontFamily
        {
            get => highlightText.FontFamily;
            set => Apply(text => text.FontFamily = value);
        }

        public double FontSize
        {
            get => highlightText.FontSize;
            set => Apply(text => text.FontSize = value);
        }

        public FontWeight FontWeight
        {
            get => highlightText.FontWeight;
            set => Apply(text => text.FontWeight = value);
        }

        public FontStyle FontStyle
        {
            get => highlightText.FontStyle;
            set => Apply(text => text.FontStyle = value);
        }

        public FontStretch FontStretch
        {
            get => highlightText.FontStretch;
            set => Apply(text => text.FontStretch = value);
        }

        public double LineHeight
        {
            get => highlightText.LineHeight;
            set => Apply(text => text.LineHeight = value);
        }

        public TextAlignment TextAlignment
        {
            get => highlightText.TextAlignment;
            set => Apply(text => text.TextAlignment = value);
        }

        public TextTrimming TextTrimming
        {
            get => highlightText.TextTrimming;
            set => Apply(text => text.TextTrimming = value);
        }

        public TextWrapping TextWrapping
        {
            get => highlightText.TextWrapping;
            set => Apply(text => text.TextWrapping = value);
        }

        public void Present(
            string text,
            LyricLine line,
            TimeSpan effectivePosition,
            bool isPlaying,
            double fallbackProgress = -1)
        {
            var normalizedText = text ?? string.Empty;
            var textChanged = !string.Equals(Text, normalizedText, StringComparison.Ordinal);
            var nextTrackingLine = IsMatchingWordTimedLine(line, normalizedText) ? line : null;
            var trackingLineChanged = !ReferenceEquals(trackingLine, nextTrackingLine);
            var now = Stopwatch.GetTimestamp();
            var keepContinuousAnchor = !textChanged &&
                !trackingLineChanged &&
                playbackAdvancing &&
                isPlaying &&
                nextTrackingLine != null &&
                Math.Abs((effectivePosition - GetProjectedPosition(now)).TotalMilliseconds) <=
                    MaximumContinuousAnchorDrift.TotalMilliseconds;
            Text = normalizedText;
            trackingLine = nextTrackingLine;
            if (textChanged || trackingLineChanged)
            {
                RebuildVisualWordMap();
            }
            if (!keepContinuousAnchor)
            {
                anchorPosition = effectivePosition;
                anchorTimestamp = now;
            }
            playbackAdvancing = isPlaying && trackingLine != null;

            var progress = trackingLine != null
                ? GetVisualProgress(keepContinuousAnchor ? GetProjectedPosition(now) : effectivePosition)
                : fallbackProgress;
            ApplyClip(progress);
            UpdateRenderingSubscription();
        }

        public void StopPlaybackProjection()
        {
            playbackAdvancing = false;
            UpdateRenderingSubscription();
        }

        internal TimeSpan CapturePlaybackPosition()
        {
            return playbackAdvancing && trackingLine != null
                ? GetProjectedPosition(Stopwatch.GetTimestamp())
                : anchorPosition;
        }

        private void Rendering(object sender, EventArgs args)
        {
            if (!playbackAdvancing || trackingLine == null)
            {
                UpdateRenderingSubscription();
                return;
            }

            var progress = GetVisualProgress(GetProjectedPosition(Stopwatch.GetTimestamp()));
            ApplyClip(progress);
            if (progress >= 1)
            {
                playbackAdvancing = false;
                UpdateRenderingSubscription();
            }
        }

        private void ApplyClip(double progress)
        {
            displayedProgress = progress;
            if (progress < 0 || string.IsNullOrEmpty(Text))
            {
                dimText.Visibility = Visibility.Collapsed;
                highlightText.Clip = null;
                return;
            }

            dimText.Visibility = Visibility.Visible;
            highlightText.Clip = highlightClip;
            var normalized = Math.Max(0, Math.Min(1, progress));
            var width = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
            var height = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;
            var textWidth = measuredTextWidth > 0 ? Math.Min(width, measuredTextWidth) : width;
            var origin = TextAlignment == TextAlignment.Center
                ? Math.Max(0, (width - textWidth) / 2)
                : TextAlignment == TextAlignment.Right ? Math.Max(0, width - textWidth) : 0;
            highlightClip.Rect = new Rect(origin, 0, Math.Max(0, textWidth * normalized), Math.Max(0, height));
        }

        private void UpdateRenderingSubscription()
        {
            SetRenderingSubscribed(IsLoaded && IsVisible && playbackAdvancing && trackingLine != null);
        }

        private void SetRenderingSubscribed(bool subscribe)
        {
            if (renderingSubscribed == subscribe) return;
            renderingSubscribed = subscribe;
            if (subscribe)
            {
                CompositionTarget.Rendering += Rendering;
            }
            else
            {
                CompositionTarget.Rendering -= Rendering;
            }
        }

        private void Apply(Action<TextBlock> update)
        {
            update(dimText);
            update(highlightText);
            RebuildVisualWordMap();
        }

        private TimeSpan GetProjectedPosition(long timestamp)
        {
            var elapsedTicks = timestamp - anchorTimestamp;
            var elapsed = TimeSpan.FromSeconds(Math.Max(0, elapsedTicks) / (double)Stopwatch.Frequency);
            return anchorPosition + elapsed;
        }

        private double GetVisualProgress(TimeSpan position)
        {
            if (trackingLine == null || visualWordSpans.Count != trackingLine.Words.Count || measuredTextWidth <= 0)
                return trackingLine?.GetWordProgress(position) ?? -1;
            var elapsed = position - trackingLine.Timestamp;
            if (elapsed <= TimeSpan.Zero) return 0;
            for (var index = 0; index < trackingLine.Words.Count; index++)
            {
                var word = trackingLine.Words[index];
                var span = visualWordSpans[index];
                if (elapsed >= word.Offset + word.Duration) continue;
                if (elapsed <= word.Offset) return span.Start;
                var fraction = word.Duration <= TimeSpan.Zero
                    ? 1
                    : Math.Min(1, (elapsed - word.Offset).TotalMilliseconds / word.Duration.TotalMilliseconds);
                return Math.Max(0, Math.Min(1, span.Start + ((span.End - span.Start) * fraction)));
            }
            return 1;
        }

        private void RebuildVisualWordMap()
        {
            visualWordSpans = Array.Empty<VisualWordSpan>();
            measuredTextWidth = 0;
            if (string.IsNullOrEmpty(Text)) return;

            var dpi = GetPixelsPerDip();
            if (trackingLine != null && trackingLine.HasWordTiming)
            {
                var cacheKey = new VisualWordMapCacheKey(
                    trackingLine,
                    Text,
                    FontFamily?.Source ?? string.Empty,
                    FontStyle,
                    FontWeight,
                    FontStretch,
                    dpi,
                    CultureInfo.CurrentUICulture.Name);
                if (TryGetCachedVisualWordMap(cacheKey, out var cachedMap))
                {
                    visualWordSpans = cachedMap.Spans;
                    measuredTextWidth = CreateFormattedText(Text, dpi).WidthIncludingTrailingWhitespace;
                    return;
                }

                var builtMap = BuildVisualWordMap();
                visualWordSpans = builtMap.Spans;
                measuredTextWidth = builtMap.TextWidth;
                CacheVisualWordMap(cacheKey, builtMap);
                return;
            }

            measuredTextWidth = CreateFormattedText(Text, dpi).WidthIncludingTrailingWhitespace;
        }

        private VisualWordMap BuildVisualWordMap()
        {
            var formattedText = CreateFormattedText(Text, GetPixelsPerDip());
            var textWidth = formattedText.WidthIncludingTrailingWhitespace;
            if (trackingLine == null || !trackingLine.HasWordTiming || textWidth <= 0)
                return new VisualWordMap(textWidth, Array.Empty<VisualWordSpan>());

            var spans = new List<VisualWordSpan>(trackingLine.Words.Count);
            var cursor = 0;
            foreach (var word in trackingLine.Words)
            {
                var index = Text.IndexOf(word.Text, cursor, StringComparison.Ordinal);
                if (index < cursor || string.IsNullOrEmpty(word.Text))
                {
                    return new VisualWordMap(textWidth, Array.Empty<VisualWordSpan>());
                }
                cursor = index + word.Text.Length;
                var bounds = formattedText.BuildHighlightGeometry(new Point(0, 0), index, word.Text.Length).Bounds;
                spans.Add(new VisualWordSpan(bounds.Left / textWidth, bounds.Right / textWidth));
            }
            return new VisualWordMap(textWidth, spans.ToArray());
        }

        private FormattedText CreateFormattedText(string value, double dpi)
        {
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            return new FormattedText(
                value ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Foreground ?? Brushes.White,
                dpi);
        }

        private double GetPixelsPerDip()
        {
            try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
            catch { return 1.0; }
        }

        private static bool TryGetCachedVisualWordMap(VisualWordMapCacheKey key, out VisualWordMap map)
        {
            lock (VisualWordMapCacheSync)
            {
                return VisualWordMapCache.TryGetValue(key, out map);
            }
        }

        private static void CacheVisualWordMap(VisualWordMapCacheKey key, VisualWordMap map)
        {
            lock (VisualWordMapCacheSync)
            {
                if (VisualWordMapCache.ContainsKey(key)) return;
                while (VisualWordMapCache.Count >= MaximumCachedVisualWordMaps && VisualWordMapCacheOrder.Count > 0)
                {
                    VisualWordMapCache.Remove(VisualWordMapCacheOrder.Dequeue());
                }
                VisualWordMapCache[key] = map;
                VisualWordMapCacheOrder.Enqueue(key);
            }
        }

        private sealed class VisualWordMap
        {
            public VisualWordMap(double textWidth, IReadOnlyList<VisualWordSpan> spans)
            {
                TextWidth = textWidth;
                Spans = spans ?? Array.Empty<VisualWordSpan>();
            }

            public double TextWidth { get; }
            public IReadOnlyList<VisualWordSpan> Spans { get; }
        }

        private readonly struct VisualWordMapCacheKey : IEquatable<VisualWordMapCacheKey>
        {
            private readonly LyricLine line;
            private readonly string text;
            private readonly string fontFamily;
            private readonly FontStyle fontStyle;
            private readonly FontWeight fontWeight;
            private readonly FontStretch fontStretch;
            private readonly double pixelsPerDip;
            private readonly string cultureName;

            public VisualWordMapCacheKey(
                LyricLine line,
                string text,
                string fontFamily,
                FontStyle fontStyle,
                FontWeight fontWeight,
                FontStretch fontStretch,
                double pixelsPerDip,
                string cultureName)
            {
                this.line = line;
                this.text = text ?? string.Empty;
                this.fontFamily = fontFamily ?? string.Empty;
                this.fontStyle = fontStyle;
                this.fontWeight = fontWeight;
                this.fontStretch = fontStretch;
                this.pixelsPerDip = pixelsPerDip;
                this.cultureName = cultureName ?? string.Empty;
            }

            public bool Equals(VisualWordMapCacheKey other)
            {
                return ReferenceEquals(line, other.line) &&
                    string.Equals(text, other.text, StringComparison.Ordinal) &&
                    string.Equals(fontFamily, other.fontFamily, StringComparison.Ordinal) &&
                    fontStyle == other.fontStyle &&
                    fontWeight == other.fontWeight &&
                    fontStretch == other.fontStretch &&
                    pixelsPerDip.Equals(other.pixelsPerDip) &&
                    string.Equals(cultureName, other.cultureName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is VisualWordMapCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = RuntimeHelpers.GetHashCode(line);
                    hash = (hash * 397) ^ text.GetHashCode();
                    hash = (hash * 397) ^ fontFamily.GetHashCode();
                    hash = (hash * 397) ^ fontStyle.GetHashCode();
                    hash = (hash * 397) ^ fontWeight.GetHashCode();
                    hash = (hash * 397) ^ fontStretch.GetHashCode();
                    hash = (hash * 397) ^ pixelsPerDip.GetHashCode();
                    hash = (hash * 397) ^ cultureName.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class VisualWordSpan
        {
            public VisualWordSpan(double start, double end) { Start = start; End = end; }
            public double Start { get; }
            public double End { get; }
        }

        private static bool IsMatchingWordTimedLine(LyricLine line, string text)
        {
            return line != null && line.HasWordTiming &&
                string.Equals(line.Text, text ?? string.Empty, StringComparison.Ordinal);
        }

        private static Brush CreateDimBrush(Brush source, double opacity)
        {
            var brush = (source ?? Brushes.White).CloneCurrentValue();
            brush.Opacity *= opacity;
            return brush;
        }
    }
}
