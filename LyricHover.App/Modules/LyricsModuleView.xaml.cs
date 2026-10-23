using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Windows.Threading;
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
        private bool animationsEnabled = true;
        private bool lyricsTransitionInProgress;
        private LyricLine displayedWordTrackingLine;
        private TimeSpan displayedWordTrackingPosition;
        private bool displayedWordTrackingPlaying;
        private double displayedWordTrackingProgress = -1;
        private TimeSpan displayedLineDuration;
        private int viewportRefreshVersion;
        private bool refreshingViewport;
        private FontFamily chineseFontFamily = new FontFamily("Microsoft YaHei UI");
        private FontFamily englishFontFamily = new FontFamily("Segoe UI");
        private double lyricFontSize = OverlayPlacementSettings.DefaultIslandLyricsFontSize;

        public void ApplyAppearance(OverlayPlacementSettings settings)
        {
            settings = settings ?? new OverlayPlacementSettings();
            chineseFontFamily = IslandAppearanceRendering.ResolveFont(settings.IslandChineseFontFamily, "Microsoft YaHei UI");
            englishFontFamily = IslandAppearanceRendering.ResolveFont(settings.IslandEnglishFontFamily, "Segoe UI");
            lyricFontSize = settings.IslandLyricsFontSize;
            foreach (var text in new[] { PrimaryLyricText, IncomingPrimaryLyricText })
            {
                text.EnglishFontFamily = englishFontFamily;
                text.ChineseFontFamily = chineseFontFamily;
                text.FontSize = lyricFontSize;
                text.LineHeight = Math.Ceiling(lyricFontSize * 1.2);
            }
            foreach (var text in new[] { SecondaryLyricText, IncomingSecondaryLyricText })
            {
                var value = text.Text;
                text.FontSize = lyricFontSize * 0.75;
                text.LineHeight = Math.Ceiling(lyricFontSize * 0.86);
                IslandAppearanceRendering.SetMixedText(text, value, chineseFontFamily, englishFontFamily);
            }
            // Keep the typographic line height unchanged, but give italic and script fonts
            // extra clip space for descenders. The secondary row starts after that allowance
            // instead of visually covering the bottom of the primary line.
            var primaryClipHeight = Math.Ceiling(lyricFontSize * 1.34);
            var secondaryClipHeight = Math.Ceiling(lyricFontSize * 0.95);
            Height = Math.Max(48, primaryClipHeight + secondaryClipHeight + 17);
            PrimaryLyricClip.Height = IncomingPrimaryLyricClip.Height = primaryClipHeight;
            PrimaryLyricCanvas.Height = IncomingPrimaryLyricCanvas.Height = primaryClipHeight;
            SecondaryLyricClip.Height = IncomingSecondaryLyricClip.Height = secondaryClipHeight;
            SecondaryLyricCanvas.Height = IncomingSecondaryLyricCanvas.Height = secondaryClipHeight;
            ApplyCurrentLyricsText(displayedPrimary ?? string.Empty, displayedSecondary ?? string.Empty, displayedAccent ?? string.Empty);
            QueueViewportLayoutRefresh();
        }

        public bool AnimationsEnabled
        {
            get => animationsEnabled;
            set => animationsEnabled = value;
        }

        public LyricsModuleView()
        {
            InitializeComponent();
            SizeChanged += LyricsModuleView_SizeChanged;
            ApplyModuleSettings(IslandModuleInstance.DefaultLyricsWidth);
        }

        public void ApplyModuleSettings(double lyricsWidth)
        {
            Width = IslandModuleInstance.NormalizeLyricsWidth(lyricsWidth);
        }

        public void Update(IslandRenderState state)
        {
            state = state ?? new IslandRenderState();
            var primary = state.PrimaryLyric ?? string.Empty;
            var secondary = state.SecondaryLyric ?? string.Empty;
            var accent = state.PrimaryAccent ?? string.Empty;
            var textChanged = displayedPrimary != primary || displayedSecondary != secondary || displayedAccent != accent;

            displayedWordTrackingLine = state.PrimaryWordTrackingLine;
            displayedWordTrackingPosition = state.WordTrackingPosition;
            displayedWordTrackingPlaying = ResolveIsPlaying(state);
            displayedWordTrackingProgress = state.PrimaryWordTrackingProgress;

            if (!textChanged)
            {
                UpdateActiveWordTracking();
                return;
            }

            SetIslandText(primary, secondary, accent, state.LineDuration);
        }

        private void SetIslandText(string primary, string secondary, string accent, TimeSpan lineDuration)
        {
            primary = primary ?? string.Empty;
            secondary = secondary ?? string.Empty;
            accent = accent ?? string.Empty;

            var shouldAnimate = lyricTextTransitionTracker.Update(primary, secondary);
            displayedPrimary = primary;
            displayedSecondary = secondary;
            displayedAccent = accent;
            displayedLineDuration = lineDuration;
            var transitionVersion = ++lyricsTransitionVersion;
            lyricsTransitionInProgress = false;

            if (!animationsEnabled)
            {
                ApplyCurrentLyricsText(primary, secondary, accent);
                ResetLyricsAnimationState();
                return;
            }

            if (!shouldAnimate)
            {
                ApplyCurrentLyricsText(primary, secondary, accent);
                ResetLyricsAnimationState();
                StartMarquee(lineDuration);
                return;
            }

            ResetLyricsAnimationState();
            StopMarquee();
            // The outgoing line is only being translated/faded now. Freeze its word
            // projection so a page transition never leaves two render callbacks active.
            PrimaryLyricText.StopPlaybackProjection();
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
            lyricsTransitionInProgress = true;

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

                // Hand the live incoming projection to the settled layer. Reusing the
                // last media snapshot here would move the first word backwards briefly.
                displayedWordTrackingPosition = IncomingPrimaryLyricText.CapturePlaybackPosition();
                lyricsTransitionInProgress = false;
                ApplyCurrentLyricsText(displayedPrimary, displayedSecondary, displayedAccent);
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
            IncomingPrimaryLyricText.StopPlaybackProjection();
            CurrentLyricsPanel.BeginAnimation(OpacityProperty, null);
            CurrentLyricsTransform.BeginAnimation(TranslateTransform.YProperty, null);
            IncomingLyricsPanel.BeginAnimation(OpacityProperty, null);
            IncomingLyricsTransform.BeginAnimation(TranslateTransform.YProperty, null);

            CurrentLyricsPanel.Opacity = 1;
            CurrentLyricsTransform.Y = 0;
            IncomingLyricsPanel.Opacity = 0;
            IncomingLyricsTransform.Y = 10;
        }

        private void UpdateActiveWordTracking()
        {
            var target = lyricsTransitionInProgress ? IncomingPrimaryLyricText : PrimaryLyricText;
            PresentWordTracking(target, displayedPrimary);
        }

        private void PresentWordTracking(WordTrackingTextBlock textBlock, string text)
        {
            textBlock.Present(
                text,
                displayedWordTrackingLine,
                displayedWordTrackingPosition,
                displayedWordTrackingPlaying,
                displayedWordTrackingProgress);
        }

        private static bool ResolveIsPlaying(IslandRenderState state)
        {
            var status = state.PendingPlaybackStatus ?? state.Session?.PlaybackStatus;
            return status == LyricHover.Core.Media.MediaPlaybackStatus.Playing;
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
            if (!animationsEnabled || !placement.RequiresMarquee)
            {
                return;
            }

            var duration = TimeSpan.FromMilliseconds(Math.Max(1800, lineDuration.TotalMilliseconds - 450));
            var dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = IslandAppearanceRendering.SnapToDevicePixel(-placement.Overflow, dpiScale),
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
            IslandAppearanceRendering.SetMixedText(textBlock, text, chineseFontFamily, englishFontFamily);
            textBlock.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            textBlock.Width = double.NaN;
            Canvas.SetLeft(textBlock, 0);
            if (visible)
            {
                MeasureAndPositionElement(textBlock, transform, clip);
            }
            else
            {
                clip.OpacityMask = null;
            }
        }

        private void PreparePrimaryLine(
            StackPanel panel,
            WordTrackingTextBlock textBlock,
            TextBlock accentTextBlock,
            TranslateTransform transform,
            FrameworkElement clip,
            string text,
            string accent,
            bool visible)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            PresentWordTracking(textBlock, text);
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

            var fadeWidth = IslandAppearanceRendering.CalculateOverflowFadeWidth(lyricFontSize);
            // A font's visible ink can extend beyond its layout advance width. Reserve a
            // fade-width guard on both sides so very large or wide-stroke glyphs enter the
            // viewport mask before an outer clip can cut them off abruptly.
            var naturalPlacement = LyricTextPlacement.Calculate(availableWidth, textWidth);
            var guardedPlacement = LyricTextPlacement.Calculate(availableWidth, textWidth + (fadeWidth * 2));
            var placement = guardedPlacement.RequiresMarquee ? guardedPlacement : naturalPlacement;
            var dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var elementLeft = guardedPlacement.RequiresMarquee ? fadeWidth : naturalPlacement.Left;
            Canvas.SetLeft(element, IslandAppearanceRendering.SnapToDevicePixel(elementLeft, dpiScale));
            clip.OpacityMask = placement.RequiresMarquee
                ? IslandAppearanceRendering.CreateHorizontalOverflowFadeMask(availableWidth, fadeWidth)
                : null;
            return placement;
        }

        private void LyricsModuleView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
            {
                return;
            }

            // ApplyLayout can reuse this view and change its width while the lyric text
            // remains identical. Update() then intentionally skips the text path, so wait
            // for layout to expose the new clip width before rebuilding placement/masks.
            QueueViewportLayoutRefresh();
        }

        private void QueueViewportLayoutRefresh()
        {
            var refreshVersion = ++viewportRefreshVersion;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (refreshVersion == viewportRefreshVersion)
                {
                    RefreshViewportLayout();
                }
            }));
        }

        private void RefreshViewportLayout()
        {
            if (refreshingViewport)
            {
                return;
            }

            refreshingViewport = true;
            try
            {
                if (lyricsTransitionInProgress)
                {
                    RefreshLayerOverflow(
                        PrimaryLyricLinePanel,
                        PrimaryLyricTransform,
                        PrimaryLyricClip,
                        SecondaryLyricText,
                        SecondaryLyricTransform,
                        SecondaryLyricClip);
                    RefreshLayerOverflow(
                        IncomingPrimaryLyricLinePanel,
                        IncomingPrimaryLyricTextTransform,
                        IncomingPrimaryLyricClip,
                        IncomingSecondaryLyricText,
                        IncomingSecondaryLyricTextTransform,
                        IncomingSecondaryLyricClip);
                    return;
                }

                StopMarquee();
                RefreshLayerOverflow(
                    PrimaryLyricLinePanel,
                    PrimaryLyricTransform,
                    PrimaryLyricClip,
                    SecondaryLyricText,
                    SecondaryLyricTransform,
                    SecondaryLyricClip);
                StartMarquee(displayedLineDuration);
            }
            finally
            {
                refreshingViewport = false;
            }
        }

        private void RefreshLayerOverflow(
            FrameworkElement primary,
            TranslateTransform primaryTransform,
            FrameworkElement primaryClip,
            FrameworkElement secondary,
            TranslateTransform secondaryTransform,
            FrameworkElement secondaryClip)
        {
            RefreshElementOverflow(primary, primaryTransform, primaryClip);
            RefreshElementOverflow(secondary, secondaryTransform, secondaryClip);
        }

        private void RefreshElementOverflow(
            FrameworkElement element,
            TranslateTransform transform,
            FrameworkElement clip)
        {
            if (element.Visibility == Visibility.Visible)
            {
                MeasureAndPositionElement(element, transform, clip);
            }
            else
            {
                clip.OpacityMask = null;
            }
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
