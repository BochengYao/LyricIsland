using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LyricHover.Core;
using LyricHover.Core.Layout;

namespace LyricHover.App.Modules
{
    public partial class LyricsModuleView : UserControl, IIslandModuleView
    {
        private readonly LyricTextTransitionTracker lyricTextTransitionTracker = new LyricTextTransitionTracker();
        private string displayedPrimary;
        private string displayedAccent;
        private string displayedSecondary;
        private int lyricsTransitionVersion;

        public LyricsModuleView()
        {
            InitializeComponent();
            ApplyModuleSettings(IslandModuleInstance.DefaultLyricsWidth);
        }

        public void ApplyModuleSettings(double lyricsWidth)
        {
            Width = IslandModuleInstance.NormalizeLyricsWidth(lyricsWidth);
        }

        public void Update(IslandRenderState state)
        {
            state = state ?? new IslandRenderState();
            SetIslandText(state.PrimaryLyric, state.SecondaryLyric, state.PrimaryAccent, state.LineDuration);
        }

        private void SetIslandText(string primary, string secondary, string accent, TimeSpan lineDuration)
        {
            primary = primary ?? string.Empty;
            secondary = secondary ?? string.Empty;
            accent = accent ?? string.Empty;

            if (displayedPrimary == primary && displayedSecondary == secondary && displayedAccent == accent)
            {
                return;
            }

            var shouldAnimate = lyricTextTransitionTracker.Update(primary, secondary);
            displayedPrimary = primary;
            displayedSecondary = secondary;
            displayedAccent = accent;
            var transitionVersion = ++lyricsTransitionVersion;

            if (!shouldAnimate)
            {
                ApplyCurrentLyricsText(primary, secondary, accent);
                ResetLyricsAnimationState();
                StartMarquee(lineDuration);
                return;
            }

            ResetLyricsAnimationState();
            StopMarquee();
            PreparePrimaryLine(
                IncomingPrimaryLyricLinePanel,
                IncomingPrimaryLyricText,
                IncomingPrimaryAccentText,
                IncomingPrimaryLyricTextTransform,
                IncomingPrimaryLyricClip,
                primary,
                accent,
                true);
            PrepareTextBlock(
                IncomingSecondaryLyricText,
                IncomingSecondaryLyricTextTransform,
                IncomingSecondaryLyricClip,
                secondary,
                !string.IsNullOrWhiteSpace(secondary));
            IncomingLyricsPanel.Opacity = 0;
            IncomingLyricsTransform.Y = 10;
            CurrentLyricsPanel.Opacity = 1;
            CurrentLyricsTransform.Y = 0;

            var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(280);

            var outgoingOpacity = new DoubleAnimation(0, duration) { EasingFunction = easing };
            var outgoingMove = new DoubleAnimation(-9, duration) { EasingFunction = easing };
            var incomingOpacity = new DoubleAnimation(1, duration) { EasingFunction = easing };
            var incomingMove = new DoubleAnimation(0, duration) { EasingFunction = easing };

            incomingOpacity.Completed += (sender, args) =>
            {
                if (transitionVersion != lyricsTransitionVersion)
                {
                    return;
                }

                ApplyCurrentLyricsText(primary, secondary, accent);
                ResetLyricsAnimationState();
                StartMarquee(lineDuration);
            };

            CurrentLyricsPanel.BeginAnimation(OpacityProperty, outgoingOpacity);
            CurrentLyricsTransform.BeginAnimation(TranslateTransform.YProperty, outgoingMove);
            IncomingLyricsPanel.BeginAnimation(OpacityProperty, incomingOpacity);
            IncomingLyricsTransform.BeginAnimation(TranslateTransform.YProperty, incomingMove);
        }

        private void ApplyCurrentLyricsText(string primary, string secondary, string accent)
        {
            StopMarquee();
            PreparePrimaryLine(
                PrimaryLyricLinePanel,
                PrimaryLyricText,
                PrimaryAccentText,
                PrimaryLyricTransform,
                PrimaryLyricClip,
                primary,
                accent,
                true);
            PrepareTextBlock(
                SecondaryLyricText,
                SecondaryLyricTransform,
                SecondaryLyricClip,
                secondary,
                !string.IsNullOrWhiteSpace(secondary));
        }

        private void ResetLyricsAnimationState()
        {
            CurrentLyricsPanel.BeginAnimation(OpacityProperty, null);
            CurrentLyricsTransform.BeginAnimation(TranslateTransform.YProperty, null);
            IncomingLyricsPanel.BeginAnimation(OpacityProperty, null);
            IncomingLyricsTransform.BeginAnimation(TranslateTransform.YProperty, null);

            CurrentLyricsPanel.Opacity = 1;
            CurrentLyricsTransform.Y = 0;
            IncomingLyricsPanel.Opacity = 0;
            IncomingLyricsTransform.Y = 10;
        }

        private void StartMarquee(TimeSpan lineDuration)
        {
            StartMarqueeIfNeeded(PrimaryLyricLinePanel, PrimaryLyricTransform, PrimaryLyricClip, lineDuration);
            StartMarqueeIfNeeded(SecondaryLyricText, SecondaryLyricTransform, SecondaryLyricClip, lineDuration);
        }

        private void StartMarqueeIfNeeded(FrameworkElement element, TranslateTransform transform, FrameworkElement clip, TimeSpan lineDuration)
        {
            if (element == null || element.Visibility != Visibility.Visible)
            {
                return;
            }

            var placement = MeasureAndPositionElement(element, transform, clip);
            if (!placement.RequiresMarquee)
            {
                return;
            }

            var duration = TimeSpan.FromMilliseconds(Math.Max(1800, lineDuration.TotalMilliseconds - 450));

            var animation = new DoubleAnimation
            {
                From = 0,
                To = -placement.Overflow,
                BeginTime = TimeSpan.FromMilliseconds(260),
                Duration = duration,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void PrepareTextBlock(
            TextBlock textBlock,
            TranslateTransform transform,
            FrameworkElement clip,
            string text,
            bool visible)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            textBlock.Text = text ?? string.Empty;
            textBlock.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            textBlock.Width = double.NaN;
            Canvas.SetLeft(textBlock, 0);
            if (visible)
            {
                MeasureAndPositionElement(textBlock, transform, clip);
            }
        }

        private void PreparePrimaryLine(
            StackPanel panel,
            TextBlock textBlock,
            TextBlock accentTextBlock,
            TranslateTransform transform,
            FrameworkElement clip,
            string text,
            string accent,
            bool visible)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            textBlock.Text = text ?? string.Empty;
            accentTextBlock.Text = accent ?? string.Empty;
            accentTextBlock.Visibility = string.IsNullOrWhiteSpace(accent)
                ? Visibility.Collapsed
                : Visibility.Visible;
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            panel.Width = double.NaN;
            Canvas.SetLeft(panel, 0);
            if (visible)
            {
                MeasureAndPositionElement(panel, transform, clip);
            }
        }

        private LyricTextPlacement MeasureAndPositionElement(
            FrameworkElement element,
            TranslateTransform transform,
            FrameworkElement clip)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            element.Width = double.NaN;
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = element.DesiredSize.Width;
            element.Width = textWidth;

            var availableWidth = clip.ActualWidth;
            if (availableWidth <= 0)
            {
                availableWidth = ActualWidth > 0 ? ActualWidth : Width;
            }

            var placement = LyricTextPlacement.Calculate(availableWidth, textWidth);
            Canvas.SetLeft(element, placement.Left);
            return placement;
        }

        private void StopMarquee()
        {
            PrimaryLyricTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SecondaryLyricTransform.BeginAnimation(TranslateTransform.XProperty, null);
            PrimaryLyricTransform.X = 0;
            SecondaryLyricTransform.X = 0;
            PrimaryLyricLinePanel.Width = double.NaN;
            SecondaryLyricText.Width = double.NaN;
        }
    }
}
