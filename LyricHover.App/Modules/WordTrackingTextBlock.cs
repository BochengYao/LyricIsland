using System;
using System.Diagnostics;
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
            Text = text;
            trackingLine = IsMatchingWordTimedLine(line, Text) ? line : null;
            anchorPosition = effectivePosition;
            anchorTimestamp = Stopwatch.GetTimestamp();
            playbackAdvancing = isPlaying && trackingLine != null;

            var progress = trackingLine != null
                ? trackingLine.GetWordProgress(effectivePosition)
                : fallbackProgress;
            ApplyClip(progress);
            UpdateRenderingSubscription();
        }

        public void StopPlaybackProjection()
        {
            playbackAdvancing = false;
            UpdateRenderingSubscription();
        }

        private void Rendering(object sender, EventArgs args)
        {
            if (!playbackAdvancing || trackingLine == null)
            {
                UpdateRenderingSubscription();
                return;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - anchorTimestamp;
            var elapsed = TimeSpan.FromSeconds(elapsedTicks / (double)Stopwatch.Frequency);
            var progress = trackingLine.GetWordProgress(anchorPosition + elapsed);
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
            highlightClip.Rect = new Rect(0, 0, Math.Max(0, width * normalized), Math.Max(0, height));
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
