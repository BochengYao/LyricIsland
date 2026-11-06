using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LyricHover.App.Modules;
using LyricHover.Core;
using LyricHover.Core.Media;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace LyricHover.App.Fullscreen
{
    public sealed class FullscreenLyricsPresentation
    {
        public MediaSessionSnapshot Session { get; set; }
        public TimedLyrics Lyrics { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Offset { get; set; }
        public bool LyricsSearchFinished { get; set; }
    }

    public partial class FullscreenLyricsWindow : Window, IDisposable
    {
        // Starting the visual transition slightly ahead of the audible boundary
        // compensates for SMTC delivery, UI scheduling and the easing interval. The
        // progress bar continues to use the unmodified media position.
        private static readonly TimeSpan LyricVisualLead = TimeSpan.FromMilliseconds(380);

        private readonly AlbumThemeExtractor themeExtractor = new AlbumThemeExtractor();
        private readonly ArtworkEnhancer artworkEnhancer = new ArtworkEnhancer();
        private readonly OnlineArtworkResolver onlineArtworkResolver = new OnlineArtworkResolver();
        private readonly SystemOutputAudioAnalyzer audioAnalyzer = new SystemOutputAudioAnalyzer();
        private readonly DispatcherTimer auxiliaryTimer;
        private readonly DispatcherTimer followResumeTimer;
        private readonly DispatcherTimer followCenterTimer;
        private readonly DispatcherTimer manualScrollTimer;
        private readonly DispatcherTimer audioTimer;
        private readonly List<LyricVisual> lyricVisuals = new List<LyricVisual>();
        private readonly TranslateTransform lyricsMotionTransform = new TranslateTransform();
        private readonly RectangleGeometry artworkClip = new RectangleGeometry();
        private OverlayPlacementSettings appliedSettings;
        private OverlayPlacementSettings draftSettings;
        private FullscreenLyricsPresentation presentation = new FullscreenLyricsPresentation();
        private TimedLyrics renderedLyrics;
        private string renderedTrackKey = string.Empty;
        private string renderedArtworkKey = string.Empty;
        private string previousTrackArtworkKey = string.Empty;
        private string acceptedSmtcArtworkKey = string.Empty;
        private string onlineArtworkRequestKey = string.Empty;
        private byte[] lastObservedArtworkBytes;
        private string lastObservedArtworkKey = string.Empty;
        private int currentLineIndex = -1;
        private int centeredLineIndex = -1;
        private int themeGeneration;
        private int scrollAnimationGeneration;
        private bool currentLineScrollQueued;
        private bool queuedCurrentLineScrollAnimated;
        private bool lyricRowMeasurementQueued;
        private DateTime trackChangedUtc = DateTime.MinValue;
        private bool updatingSettings;
        private bool manualBrowsing;
        private bool scrollAnimationActive;
        private DateTime lastAudioStartAttemptUtc = DateTime.MinValue;
        private bool disposed;
        private bool fadeInStarted;
        private bool fadeOutStarted;
        private bool allowImmediateClose;
        private MMDevice volumeDevice;
        private bool updatingVolume;
        private DateTime presentationReceivedUtc = DateTime.UtcNow;
        private bool progressRenderingSubscribed;
        private bool settingsPanelOpen;
        private double manualScrollTarget;
        private DateTime manualScrollLastTickUtc = DateTime.UtcNow;

        public event EventHandler PreviousRequested;
        public event EventHandler PlayPauseRequested;
        public event EventHandler NextRequested;
        public event EventHandler<TimeSpan> SeekRequested;
        public event EventHandler<OverlayPlacementSettings> SettingsApplyRequested;

        public FullscreenLyricsWindow(OverlayPlacementSettings settings)
        {
            InitializeComponent();
            LyricsStack.RenderTransform = lyricsMotionTransform;
            ArtworkImage.Clip = artworkClip;
            ArtworkImage.SizeChanged += ArtworkImage_SizeChanged;
            appliedSettings = (settings ?? new OverlayPlacementSettings()).DeepClone();
            appliedSettings.Normalize();
            draftSettings = appliedSettings.DeepClone();
            PopulateFontChoices();
            LoadSettingsControls(draftSettings);
            ApplyVisualSettings(draftSettings, true);
            auxiliaryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            auxiliaryTimer.Tick += (sender, args) => FadeAuxiliaryControls(false);
            followResumeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            followResumeTimer.Tick += (sender, args) => ResumeAutoFollow();
            manualScrollTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            manualScrollTimer.Tick += ManualScrollTimer_Tick;
            followCenterTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            followCenterTimer.Tick += (sender, args) =>
            {
                // Detect the line boundary from the projected playback clock and
                // start emphasis, colour and scrolling in this same render tick.
                UpdateLyricsPosition(true);
                EnsureCurrentLineCentered();
            };
            audioTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            audioTimer.Tick += AudioTimer_Tick;
            LyricsScroll.PreviewMouseWheel += LyricsScroll_PreviewMouseWheel;
            LyricsScroll.PreviewMouseDown += (sender, args) => BeginManualBrowsing();
            Loaded += Window_Loaded;
            ContentRendered += Window_ContentRendered;
            Closing += Window_Closing;
            Closed += (sender, args) => Dispose();
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        public void Present(FullscreenLyricsPresentation value)
        {
            if (disposed) return;
            presentation = value ?? new FullscreenLyricsPresentation();
            presentationReceivedUtc = DateTime.UtcNow;
            var session = presentation.Session;
            TitleText.Text = string.IsNullOrWhiteSpace(session?.Title) ? "等待播放" : session.Title;
            ArtistAlbumText.Text = FormatArtistAlbum(session);
            var effectiveDuration = ResolveDuration(presentation);
            var duration = Math.Max(0, effectiveDuration.TotalSeconds);
            ProgressSlider.Maximum = Math.Max(1, duration);
            UpdatePlaybackProgress();
            DurationText.Text = FormatTime(effectiveDuration);
            PlayPauseIcon.Data = (Geometry)FindResource(session?.PlaybackStatus == MediaPlaybackStatus.Playing
                ? "PauseIconGeometry"
                : "PlayIconGeometry");
            AmbientBackground.SetPlayback(session?.PlaybackStatus == MediaPlaybackStatus.Playing);
            UpdateProgressRenderingSubscription();

            var trackKey = GetTrackKey(session);
            if (!string.Equals(trackKey, renderedTrackKey, StringComparison.Ordinal))
            {
                previousTrackArtworkKey = renderedArtworkKey;
                renderedTrackKey = trackKey;
                renderedLyrics = null;
                renderedArtworkKey = string.Empty;
                acceptedSmtcArtworkKey = string.Empty;
                onlineArtworkRequestKey = string.Empty;
                lastObservedArtworkBytes = null;
                lastObservedArtworkKey = string.Empty;
                trackChangedUtc = DateTime.UtcNow;
                SetArtwork(null);
                themeGeneration++;
                AmbientBackground.TransitionTo(AlbumThemePalette.Default);
            }
            AcceptArtworkWhenStable(trackKey, session?.ArtworkBytes);

            if (session == null)
            {
                SetArtwork(null);
            }

            if (!ReferenceEquals(renderedLyrics, presentation.Lyrics)) BuildLyrics();
            UpdateLyricSeekAvailability(session?.Controls?.CanSeek == true);
            UpdateLyricsPosition();
        }

        public void ApplyCommittedSettings(OverlayPlacementSettings settings)
        {
            appliedSettings = (settings ?? new OverlayPlacementSettings()).DeepClone();
            appliedSettings.Normalize();
            draftSettings = appliedSettings.DeepClone();
            LoadSettingsControls(draftSettings);
            ApplyVisualSettings(draftSettings, true);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Focus();
            FadeAuxiliaryControls(true);
            InitializeVolume();
            EnsureAudioState();
            audioTimer.Start();
            followCenterTimer.Start();
            UpdateProgressRenderingSubscription();
            Dispatcher.BeginInvoke(new Action(UpdateResponsiveLayout), DispatcherPriority.Loaded);
        }

        private void ArtworkImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = Math.Max(0, e.NewSize.Width);
            var height = Math.Max(0, e.NewSize.Height);
            var radius = Math.Min(14, Math.Min(width, height) * 0.5);
            artworkClip.Rect = new Rect(0, 0, width, height);
            artworkClip.RadiusX = radius;
            artworkClip.RadiusY = radius;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (fadeInStarted || disposed) return;
            fadeInStarted = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (disposed || fadeOutStarted) return;
                BeginAnimation(OpacityProperty, null);
                Opacity = 1;
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                }, HandoffBehavior.SnapshotAndReplace);
            }), DispatcherPriority.Render);
        }

        private void BuildLyrics()
        {
            renderedLyrics = presentation.Lyrics;
            LyricsStack.Children.Clear();
            lyricVisuals.Clear();
            currentLineIndex = -1;
            centeredLineIndex = -1;
            var lines = renderedLyrics?.Lines ?? Array.Empty<LyricLine>();
            EmptyStateText.Visibility = lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LyricsScroll.Visibility = lines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (lines.Count == 0)
            {
                EmptyStateText.Text = presentation.Session == null ? "等待播放" :
                    presentation.LyricsSearchFinished ? "未找到同步歌词\n也可能是纯音乐" : "正在加载歌词…";
                return;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var content = new Grid { VerticalAlignment = VerticalAlignment.Center };
                var panel = new Border
                {
                    Margin = new Thickness(0, 8, 0, 8),
                    Padding = new Thickness(18, 10, 18, 10),
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(24),
                    Tag = i,
                    Cursor = Cursors.Arrow,
                    Child = content
                };
                var primary = new WordTrackingTextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left,
                    Foreground = Brushes.White,
                    DimOpacity = 0.64,
                    UseWrappedLineHighlight = true,
                    Text = line.Text
                };
                TextOptions.SetTextFormattingMode(primary, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(primary, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(primary, TextHintingMode.Fixed);
                RenderOptions.SetClearTypeHint(primary, ClearTypeHint.Enabled);
                var translationLine = renderedLyrics?.GetTranslationForLine(line, TimeSpan.FromSeconds(1.5));
                var translation = new TextBlock
                {
                    Text = translationLine?.Text ?? string.Empty,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromArgb(205, 255, 255, 255))
                };
                var activeContent = new StackPanel
                {
                    Opacity = 1,
                    RenderTransform = new ScaleTransform(1, 1),
                    RenderTransformOrigin = new Point(0, 0.5)
                };
                TextOptions.SetTextFormattingMode(translation, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(translation, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(translation, TextHintingMode.Fixed);
                RenderOptions.SetClearTypeHint(translation, ClearTypeHint.Enabled);
                activeContent.Children.Add(primary);
                activeContent.Children.Add(translation);
                content.Children.Add(activeContent);
                var hitTarget = new Button
                {
                    Style = (Style)FindResource("LyricHitButton"),
                    Tag = i
                };
                Panel.SetZIndex(hitTarget, 2);
                hitTarget.MouseEnter += LyricHitTarget_MouseEnter;
                hitTarget.MouseLeave += LyricHitTarget_MouseLeave;
                hitTarget.PreviewMouseLeftButtonDown += LyricHitTarget_PreviewMouseLeftButtonDown;
                hitTarget.PreviewMouseLeftButtonUp += LyricHitTarget_PreviewMouseLeftButtonUp;
                hitTarget.Click += LyricHitTarget_Click;
                content.Children.Add(hitTarget);
                LyricsStack.Children.Add(panel);
                lyricVisuals.Add(new LyricVisual(
                    line, panel, content, activeContent, primary, translation, hitTarget));
            }
            ApplyTypography();
        }

        private void UpdateLyricSeekAvailability(bool canSeek)
        {
            foreach (var visual in lyricVisuals)
            {
                visual.HitTarget.IsHitTestVisible = canSeek;
                visual.HitTarget.Cursor = canSeek ? Cursors.Hand : Cursors.Arrow;
                visual.Container.Cursor = canSeek ? Cursors.Hand : Cursors.Arrow;
                if (canSeek || !(visual.Container.Background is SolidColorBrush brush)) continue;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.Color = Colors.Transparent;
            }
        }

        private void ApplyTypography()
        {
            var primaryChineseName = FullscreenLyricsPresets.ResolveInstalledFont(draftSettings.FullscreenPrimaryChineseFontFamily, OverlayPlacementSettings.DefaultChineseFontFamily);
            var primaryEnglishName = FullscreenLyricsPresets.ResolveInstalledFont(draftSettings.FullscreenPrimaryEnglishFontFamily, OverlayPlacementSettings.DefaultEnglishFontFamily);
            var translationChineseName = FullscreenLyricsPresets.ResolveInstalledFont(draftSettings.FullscreenTranslationChineseFontFamily, OverlayPlacementSettings.DefaultChineseFontFamily);
            var missing = new List<string>();
            if (!string.Equals(primaryChineseName, draftSettings.FullscreenPrimaryChineseFontFamily, StringComparison.OrdinalIgnoreCase)) missing.Add(draftSettings.FullscreenPrimaryChineseFontFamily);
            if (!string.Equals(primaryEnglishName, draftSettings.FullscreenPrimaryEnglishFontFamily, StringComparison.OrdinalIgnoreCase)) missing.Add(draftSettings.FullscreenPrimaryEnglishFontFamily);
            if (!string.Equals(translationChineseName, draftSettings.FullscreenTranslationChineseFontFamily, StringComparison.OrdinalIgnoreCase)) missing.Add(draftSettings.FullscreenTranslationChineseFontFamily);
            FontFallbackText.Text = missing.Count == 0 ? string.Empty : "字体不可用，已临时回退（仍保留原选择）：" + string.Join("、", missing.Distinct());
            FontFallbackText.Visibility = missing.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            var primaryChinese = new FontFamily(primaryChineseName);
            var primaryEnglish = new FontFamily(primaryEnglishName);
            var translationChinese = new FontFamily(translationChineseName);
            var translationEnglish = translationChinese;
            foreach (var visual in lyricVisuals)
            {
                visual.Primary.ChineseFontFamily = primaryChinese;
                visual.Primary.EnglishFontFamily = primaryEnglish;
                visual.Primary.FontSize = draftSettings.FullscreenPrimaryFontSize * 1.10;
                visual.Primary.FontWeight = FontWeight.FromOpenTypeWeight(draftSettings.FullscreenPrimaryFontWeight);
                visual.Primary.LineHeight = visual.Primary.FontSize * draftSettings.FullscreenLineHeightRatio;
                visual.Translation.FontFamily = translationEnglish;
                visual.Translation.FontSize = draftSettings.FullscreenTranslationFontSize * 1.10;
                visual.Translation.FontWeight = FontWeight.FromOpenTypeWeight(draftSettings.FullscreenTranslationFontWeight);
                visual.Translation.LineHeight = visual.Translation.FontSize * draftSettings.FullscreenLineHeightRatio;
                IslandAppearanceRendering.SetMixedText(visual.Translation, visual.Translation.Text, translationChinese, translationEnglish);
                visual.Translation.Visibility = draftSettings.FullscreenShowTranslation && !string.IsNullOrWhiteSpace(visual.Translation.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            QueueLyricRowMeasurement();
        }

        private void QueueLyricRowMeasurement()
        {
            if (lyricRowMeasurementQueued || lyricVisuals.Count == 0) return;
            lyricRowMeasurementQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                lyricRowMeasurementQueued = false;
                if (disposed || lyricVisuals.Count == 0) return;
                var availableWidth = LyricsStack.ActualWidth > 1 ? LyricsStack.ActualWidth : LyricsScroll.ViewportWidth;
                if (availableWidth <= 1) return;

                foreach (var visual in lyricVisuals)
                {
                    visual.Container.MinHeight = 0;
                    visual.Container.Measure(new Size(availableWidth, double.PositiveInfinity));
                    visual.ReservedHeight = Math.Max(0, Math.Ceiling(visual.Container.DesiredSize.Height -
                        visual.Container.Margin.Top - visual.Container.Margin.Bottom));
                    visual.Container.MinHeight = visual.ReservedHeight;
                }
                QueueScrollToCurrent(false);
            }), DispatcherPriority.Loaded);
        }

        private void UpdateLyricsPosition(bool lineBoundaryOnly = false)
        {
            if (lyricVisuals.Count == 0) return;
            var adjusted = GetProjectedAdjustedPosition();
            var nextIndex = 0;
            var lyricLayoutChanged = false;
            for (var i = 0; i < lyricVisuals.Count; i++)
            {
                if (lyricVisuals[i].Line.Timestamp <= adjusted) nextIndex = i;
                else break;
            }
            if (lineBoundaryOnly && nextIndex == currentLineIndex) return;
            var playing = presentation.Session?.PlaybackStatus == MediaPlaybackStatus.Playing;
            // Build word/line geometry before a line becomes current. Doing this in the
            // same frame as the scale/scroll transition causes a very visible hitch on
            // long mixed-language lines. Two lines of look-ahead is enough even for
            // short lyric intervals, and each visual is prepared only once.
            for (var index = nextIndex; index <= Math.Min(nextIndex + 2, lyricVisuals.Count - 1); index++)
                PrepareWordTracking(lyricVisuals[index]);
            for (var i = 0; i < lyricVisuals.Count; i++)
            {
                var visual = lyricVisuals[i];
                var distance = Math.Abs(i - nextIndex);
                var isCurrent = i == nextIndex;
                if (ApplyLyricEmphasis(visual, isCurrent)) lyricLayoutChanged = true;
                if (isCurrent)
                {
                    var trackingLine = visual.Line.HasWordTiming ? visual.Line : null;
                    visual.Primary.Present(visual.Line.Text, trackingLine, adjusted, playing, 0);
                    visual.WasTrackingCurrent = true;
                }
                else if (visual.WasTrackingCurrent)
                {
                    // Keep the prepared timing map. Clearing the tracking line here
                    // forces wrapped-word geometry to be rebuilt at the next activation.
                    visual.Primary.Present(visual.Line.Text,
                        visual.Line.HasWordTiming ? visual.Line : null,
                        visual.Line.Timestamp, false, 0);
                    visual.WasTrackingCurrent = false;
                }
                var targetOpacity = isCurrent ? 1.0 : distance == 1 ? 0.34 : distance == 2 ? 0.20 : 0.12;
                ApplyLyricOpacity(visual, targetOpacity);
            }
            if (currentLineIndex != nextIndex)
            {
                currentLineIndex = nextIndex;
            }
            var centerTargetChanged = centeredLineIndex != nextIndex;
            centeredLineIndex = nextIndex;
            if (centerTargetChanged || lyricLayoutChanged)
            {
                var targetVisualReady = nextIndex >= 0 && nextIndex < lyricVisuals.Count &&
                    lyricVisuals[nextIndex].Container.ActualHeight > 0 && LyricsScroll.ViewportHeight > 0;
                if (!manualBrowsing && targetVisualReady) ScrollToCurrent(true);
                else QueueScrollToCurrent(true);
            }
        }

        private static void PrepareWordTracking(LyricVisual visual)
        {
            if (visual.WordTrackingPrepared) return;
            visual.Primary.Present(visual.Line.Text,
                visual.Line.HasWordTiming ? visual.Line : null,
                visual.Line.Timestamp, false, 0);
            visual.WordTrackingPrepared = true;
        }


        private TimeSpan GetProjectedMediaPosition()
        {
            return ProjectMediaPosition(
                presentation.Position,
                presentationReceivedUtc,
                DateTime.UtcNow,
                presentation.Session?.PlaybackStatus == MediaPlaybackStatus.Playing,
                ResolveDuration(presentation));
        }

        private TimeSpan GetProjectedAdjustedPosition() => ApplyLyricVisualLead(
            GetProjectedMediaPosition(), presentation.Offset);

        private static TimeSpan ApplyLyricVisualLead(TimeSpan mediaPosition, TimeSpan userOffset)
        {
            var adjusted = mediaPosition + userOffset + LyricVisualLead;
            return adjusted < TimeSpan.Zero ? TimeSpan.Zero : adjusted;
        }

        private void UpdatePlaybackProgress()
        {
            if (ProgressSlider.IsMouseCaptureWithin) return;
            var position = GetProjectedMediaPosition();
            var maximum = Math.Max(0, ProgressSlider.Maximum);
            ProgressSlider.Value = Math.Max(0, Math.Min(maximum, position.TotalSeconds));
            PositionText.Text = FormatTime(position);
        }

        private void UpdateProgressRenderingSubscription()
        {
            var shouldSubscribe = !disposed && IsLoaded &&
                presentation.Session?.PlaybackStatus == MediaPlaybackStatus.Playing;
            if (progressRenderingSubscribed == shouldSubscribe) return;
            progressRenderingSubscribed = shouldSubscribe;
            if (shouldSubscribe) CompositionTarget.Rendering += ProgressRendering;
            else CompositionTarget.Rendering -= ProgressRendering;
        }

        private void ProgressRendering(object sender, EventArgs e)
        {
            UpdatePlaybackProgress();
        }

        private static TimeSpan ProjectMediaPosition(
            TimeSpan position,
            DateTime receivedUtc,
            DateTime nowUtc,
            bool playing,
            TimeSpan duration)
        {
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (playing)
            {
                var elapsed = nowUtc - receivedUtc;
                if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(5)) position += elapsed;
            }
            if (duration > TimeSpan.Zero && position > duration) position = duration;
            return position;
        }

        private bool ApplyLyricEmphasis(LyricVisual visual, bool isCurrent)
        {
            if (visual.IsCurrentVisual == isCurrent) return false;
            var animate = visual.IsCurrentVisual.HasValue && !draftSettings.FullscreenReduceMotion;
            ApplySingleLayerTransition(visual, isCurrent, animate);
            visual.IsCurrentVisual = isCurrent;
            return false;
        }

        private static void ApplySingleLayerTransition(LyricVisual visual, bool isCurrent, bool animated)
        {
            // Non-current lyrics are exactly ten percent smaller than the active
            // line. The previous 0.90/1.10 ratio was an 18% visual jump and made
            // intermediate glyph rasterization unnecessarily obvious.
            const double inactiveScale = 0.90;
            var activeScale = (ScaleTransform)visual.ActiveContent.RenderTransform;
            var currentScale = activeScale.ScaleX;
            activeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            activeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            var targetScale = isCurrent ? 1.0 : inactiveScale;

            if (!animated)
            {
                activeScale.ScaleX = activeScale.ScaleY = targetScale;
                return;
            }

            var duration = TimeSpan.FromMilliseconds(285);
            activeScale.ScaleX = activeScale.ScaleY = targetScale;
            activeScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                CreateSmoothAnimation(currentScale, targetScale, duration), HandoffBehavior.SnapshotAndReplace);
            activeScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                CreateSmoothAnimation(currentScale, targetScale, duration), HandoffBehavior.SnapshotAndReplace);
        }

        private void ApplyLyricOpacity(LyricVisual visual, double targetOpacity)
        {
            if (visual.TargetOpacity.HasValue && Math.Abs(visual.TargetOpacity.Value - targetOpacity) < 0.001) return;
            var previousOpacity = visual.Content.Opacity;
            visual.Content.BeginAnimation(OpacityProperty, null);
            visual.Content.Opacity = targetOpacity;
            if (!draftSettings.FullscreenReduceMotion && Math.Abs(previousOpacity - targetOpacity) > 0.001)
            {
                visual.Content.BeginAnimation(OpacityProperty,
                    CreateSmoothAnimation(previousOpacity, targetOpacity, TimeSpan.FromMilliseconds(260)),
                    HandoffBehavior.SnapshotAndReplace);
            }
            visual.TargetOpacity = targetOpacity;
        }

        private static DoubleAnimationUsingKeyFrames CreateSmoothAnimation(
            double from,
            double to,
            TimeSpan duration)
        {
            var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration),
                new KeySpline(0.22, 0.68, 0.30, 1.0)));
            return animation;
        }

        private void QueueScrollToCurrent(bool animated)
        {
            queuedCurrentLineScrollAnimated |= animated;
            if (currentLineScrollQueued) return;
            currentLineScrollQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                currentLineScrollQueued = false;
                var useAnimation = queuedCurrentLineScrollAnimated;
                queuedCurrentLineScrollAnimated = false;
                if (!manualBrowsing) ScrollToCurrent(useAnimation);
            }), DispatcherPriority.Loaded);
        }

        private void ScrollToCurrent(bool animated)
        {
            CommitActiveScrollAnimation();
            var followIndex = centeredLineIndex >= 0 ? centeredLineIndex : currentLineIndex;
            if (followIndex < 0 || followIndex >= lyricVisuals.Count || LyricsScroll.ViewportHeight <= 0) return;
            var visual = lyricVisuals[followIndex].Container;
            var currentLineCenterInViewport = visual.TranslatePoint(new Point(0, visual.ActualHeight * 0.5), LyricsScroll).Y;
            var target = CalculateCenteredScrollOffset(
                LyricsScroll.VerticalOffset,
                currentLineCenterInViewport,
                LyricsScroll.ViewportHeight,
                LyricsScroll.ScrollableHeight);
            var travel = Math.Abs(target - LyricsScroll.VerticalOffset);
            if (travel < 0.75) return;
            // A longer ease-out keeps the line change calm while the visual lead makes
            // sure the new line is settled by the audible lyric boundary.
            var duration = animated ? (int)Math.Max(300, Math.Min(420, 280 + travel * 0.18)) : 0;
            duration = LimitFollowAnimationToTargetTime(duration, followIndex, GetProjectedAdjustedPosition());
            AnimateScrollTo(target, duration);
        }

        private int LimitFollowAnimationToTargetTime(int requestedDuration, int followIndex, TimeSpan adjustedPosition)
        {
            if (requestedDuration <= 0 || followIndex < 0 || followIndex >= lyricVisuals.Count) return requestedDuration;
            var remaining = lyricVisuals[followIndex].Line.Timestamp - adjustedPosition;
            if (remaining <= TimeSpan.Zero) return requestedDuration;
            if (remaining <= TimeSpan.FromMilliseconds(120)) return 0;
            return Math.Min(requestedDuration, Math.Max(80, (int)remaining.TotalMilliseconds - 30));
        }

        private void EnsureCurrentLineCentered()
        {
            if (disposed || manualBrowsing || scrollAnimationActive ||
                centeredLineIndex < 0 || centeredLineIndex >= lyricVisuals.Count || LyricsScroll.ViewportHeight <= 0) return;
            var visual = lyricVisuals[centeredLineIndex].Container;
            var currentLineCenterInViewport = visual.TranslatePoint(new Point(0, visual.ActualHeight * 0.5), LyricsScroll).Y;
            if (ShouldCorrectCenter(currentLineCenterInViewport, LyricsScroll.ViewportHeight)) ScrollToCurrent(true);
        }

        private static bool ShouldCorrectCenter(double currentLineCenterInViewport, double viewportHeight)
        {
            return viewportHeight > 0 && Math.Abs(currentLineCenterInViewport - viewportHeight * 0.5) > 2.0;
        }

        private static double CalculateCenteredScrollOffset(
            double currentOffset,
            double currentLineCenterInViewport,
            double viewportHeight,
            double scrollableHeight)
        {
            var target = currentOffset + currentLineCenterInViewport - viewportHeight * 0.5;
            return Math.Max(0, Math.Min(scrollableHeight, target));
        }

        private void AnimateScrollTo(double target, int durationMilliseconds)
        {
            CommitActiveScrollAnimation();
            target = Math.Max(0, Math.Min(LyricsScroll.ScrollableHeight, target));
            var generation = ++scrollAnimationGeneration;
            var startOffset = LyricsScroll.VerticalOffset;
            var translation = -(target - startOffset);
            if (durationMilliseconds <= 0 || draftSettings.FullscreenReduceMotion)
            {
                scrollAnimationActive = false;
                LyricsScroll.ScrollToVerticalOffset(target);
                return;
            }

            scrollAnimationActive = true;
            lyricsMotionTransform.BeginAnimation(TranslateTransform.YProperty, null);
            lyricsMotionTransform.Y = 0;
            var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
            var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(translation, KeyTime.FromTimeSpan(duration),
                new KeySpline(0.18, 0.70, 0.22, 1.0)));
            animation.Completed += (sender, args) =>
            {
                if (generation != scrollAnimationGeneration) return;
                lyricsMotionTransform.BeginAnimation(TranslateTransform.YProperty, null);
                lyricsMotionTransform.Y = 0;
                LyricsScroll.ScrollToVerticalOffset(target);
                scrollAnimationActive = false;
            };
            lyricsMotionTransform.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void CommitActiveScrollAnimation()
        {
            var currentTranslation = lyricsMotionTransform.Y;
            if (!scrollAnimationActive && Math.Abs(currentTranslation) < 0.01) return;
            scrollAnimationGeneration++;
            var effectiveOffset = Math.Max(0, Math.Min(LyricsScroll.ScrollableHeight,
                LyricsScroll.VerticalOffset - currentTranslation));
            lyricsMotionTransform.BeginAnimation(TranslateTransform.YProperty, null);
            lyricsMotionTransform.Y = 0;
            scrollAnimationActive = false;
            LyricsScroll.ScrollToVerticalOffset(effectiveOffset);
        }

        private void BeginManualBrowsing()
        {
            CommitActiveScrollAnimation();
            manualBrowsing = true;
            followResumeTimer.Stop(); followResumeTimer.Start();
        }

        private static double CalculateWheelScrollTarget(
            double currentOffset,
            int wheelDelta,
            double scrollableHeight)
        {
            const double pixelsPerWheelDelta = 1.65;
            var target = currentOffset - wheelDelta * pixelsPerWheelDelta;
            return Math.Max(0, Math.Min(Math.Max(0, scrollableHeight), target));
        }

        private static double CalculateManualScrollPosition(
            double currentOffset,
            double targetOffset,
            double elapsedSeconds)
        {
            var seconds = Math.Max(0, Math.Min(0.05, elapsedSeconds));
            var progress = 1 - Math.Exp(-22 * seconds);
            return currentOffset + (targetOffset - currentOffset) * progress;
        }

        private void QueueManualWheelScroll(int wheelDelta)
        {
            var baseOffset = manualScrollTimer.IsEnabled
                ? manualScrollTarget
                : LyricsScroll.VerticalOffset;
            manualScrollTarget = CalculateWheelScrollTarget(
                baseOffset,
                wheelDelta,
                LyricsScroll.ScrollableHeight);
            manualScrollLastTickUtc = DateTime.UtcNow;
            followResumeTimer.Stop();
            manualScrollTimer.Start();
        }

        private void ManualScrollTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var elapsed = Math.Max(0.001, (now - manualScrollLastTickUtc).TotalSeconds);
            manualScrollLastTickUtc = now;
            manualScrollTarget = Math.Max(0, Math.Min(LyricsScroll.ScrollableHeight, manualScrollTarget));
            var current = LyricsScroll.VerticalOffset;
            var remaining = manualScrollTarget - current;
            if (draftSettings.FullscreenReduceMotion || Math.Abs(remaining) < 0.5)
            {
                LyricsScroll.ScrollToVerticalOffset(manualScrollTarget);
                manualScrollTimer.Stop();
                followResumeTimer.Stop();
                followResumeTimer.Start();
                return;
            }

            LyricsScroll.ScrollToVerticalOffset(CalculateManualScrollPosition(
                current,
                manualScrollTarget,
                elapsed));
        }

        private void StopManualWheelScroll()
        {
            manualScrollTimer.Stop();
            manualScrollTarget = LyricsScroll.VerticalOffset;
        }

        private void ResumeAutoFollow()
        {
            followResumeTimer.Stop();
            StopManualWheelScroll();
            manualBrowsing = false;
            ScrollToCurrent(true);
        }

        private async Task UpdateThemeAsync(string trackKey, byte[] artwork, int generation)
        {
            var palette = await themeExtractor.ExtractAsync(trackKey, artwork);
            if (disposed || generation != themeGeneration || !string.Equals(trackKey, renderedTrackKey, StringComparison.Ordinal)) return;
            AmbientBackground.TransitionTo(palette);
        }

        private void AcceptArtworkWhenStable(string trackKey, byte[] artwork)
        {
            if (artwork == null || artwork.Length == 0) return;
            if (!ReferenceEquals(artwork, lastObservedArtworkBytes))
            {
                lastObservedArtworkBytes = artwork;
                lastObservedArtworkKey = ComputeArtworkKey(artwork);
            }
            var artworkKey = lastObservedArtworkKey;
            if (string.Equals(artworkKey, renderedArtworkKey, StringComparison.Ordinal)) return;
            if (string.Equals(artworkKey, acceptedSmtcArtworkKey, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(renderedArtworkKey)) return;
            if (string.Equals(artworkKey, previousTrackArtworkKey, StringComparison.Ordinal) &&
                DateTime.UtcNow - trackChangedUtc < TimeSpan.FromMilliseconds(1200)) return;
            renderedArtworkKey = artworkKey;
            acceptedSmtcArtworkKey = artworkKey;
            SetArtwork(artwork);
            _ = UpdateThemeAsync(trackKey, artwork, ++themeGeneration);
            _ = BeginOnlineArtworkUpgradeAsync(trackKey, artworkKey, artwork, presentation.Session);
        }

        private async Task BeginOnlineArtworkUpgradeAsync(
            string trackKey,
            string smtcArtworkKey,
            byte[] smtcArtwork,
            MediaSessionSnapshot session)
        {
            var requestKey = trackKey + "|" + smtcArtworkKey;
            if (string.Equals(requestKey, onlineArtworkRequestKey, StringComparison.Ordinal)) return;
            onlineArtworkRequestKey = requestKey;
            try
            {
                var upgraded = await onlineArtworkResolver.ResolveAsync(session, smtcArtwork);
                if (disposed || upgraded == null || upgraded.Length == 0 ||
                    !string.Equals(trackKey, renderedTrackKey, StringComparison.Ordinal) ||
                    !string.Equals(smtcArtworkKey, acceptedSmtcArtworkKey, StringComparison.Ordinal) ||
                    !string.Equals(requestKey, onlineArtworkRequestKey, StringComparison.Ordinal)) return;

                var upgradedKey = ComputeArtworkKey(upgraded);
                if (string.Equals(upgradedKey, renderedArtworkKey, StringComparison.Ordinal)) return;
                renderedArtworkKey = upgradedKey;
                SetArtwork(upgraded);
                await UpdateThemeAsync(trackKey, upgraded, ++themeGeneration);
            }
            catch (Exception)
            {
                // Online artwork is optional enrichment. A truncated response,
                // malformed remote image or disposed request must never close the
                // fullscreen window; keep the already accepted SMTC artwork.
            }
        }

        private void SetArtwork(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) { ArtworkImage.Source = null; return; }
            try
            {
                var displayBytes = artworkEnhancer.Enhance(bytes);
                using (var stream = new MemoryStream(displayBytes, false))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    ArtworkImage.Source = image;
                }
            }
            catch { ArtworkImage.Source = null; }
        }

        private void ApplyVisualSettings(OverlayPlacementSettings settings, bool rebuild)
        {
            draftSettings = settings.DeepClone();
            draftSettings.Normalize();
            AmbientBackground.Configure(draftSettings);
            TranslationButton.Opacity = draftSettings.FullscreenShowTranslation ? 1 : 0.52;
            EnsureAudioState();
            if (rebuild) ApplyTypography();
        }

        private void EnsureAudioState()
        {
            var requested = draftSettings.FullscreenMotionMode == FullscreenMotionMode.AudioReactive &&
                !draftSettings.FullscreenReduceMotion && draftSettings.FullscreenRenderQuality != FullscreenRenderQuality.Static;
            if (!requested)
            {
                audioAnalyzer.Stop();
                AmbientBackground.SetAudio(new AudioEnergySnapshot());
                AudioStatusText.Text = draftSettings.FullscreenMotionMode == FullscreenMotionMode.Static ? "静态" : "氛围流动";
                return;
            }
            if (audioAnalyzer.Current.IsAvailable) { AudioStatusText.Text = "系统输出律动"; return; }
            if ((DateTime.UtcNow - lastAudioStartAttemptUtc).TotalSeconds < 4) return;
            lastAudioStartAttemptUtc = DateTime.UtcNow;
            AudioStatusText.Text = audioAnalyzer.TryStart() ? "系统输出律动" : "音频不可用，已回退氛围流动";
        }

        private void AudioTimer_Tick(object sender, EventArgs e)
        {
            EnsureAudioState();
            AmbientBackground.SetAudio(audioAnalyzer.Current);
        }

        private void InitializeVolume()
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                    volumeDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                updatingVolume = true;
                VolumeSlider.Value = volumeDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                updatingVolume = false;
            }
            catch { VolumeSlider.IsEnabled = false; }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updatingVolume || volumeDevice == null) return;
            try { volumeDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Max(0, Math.Min(1, e.NewValue)); }
            catch { VolumeSlider.IsEnabled = false; }
        }

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Slider slider) || slider.ActualWidth <= 0) return;
            for (var current = e.OriginalSource as DependencyObject; current != null; current = VisualTreeHelper.GetParent(current))
                if (current is Thumb) return;
            var point = e.GetPosition(slider);
            var ratio = Math.Max(0, Math.Min(1, point.X / slider.ActualWidth));
            slider.Value = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
            if (ReferenceEquals(slider, ProgressSlider))
                SeekRequested?.Invoke(this, TimeSpan.FromSeconds(slider.Value));
            e.Handled = true;
        }

        private void PopulateFontChoices()
        {
            var allFamilies = ChineseFontFamilyCatalog.GetInstalledFamilies(false, true).ToList();
            var chineseFamilies = ChineseFontFamilyCatalog.GetInstalledFamilies(true, true).ToList();
            AddFontOptionIfMissing(chineseFamilies, draftSettings.FullscreenPrimaryChineseFontFamily);
            AddFontOptionIfMissing(chineseFamilies, draftSettings.FullscreenTranslationChineseFontFamily);
            AddFontOptionIfMissing(allFamilies, draftSettings.FullscreenPrimaryEnglishFontFamily);
            PrimaryChineseFontCombo.ItemsSource = chineseFamilies;
            PrimaryEnglishFontCombo.ItemsSource = allFamilies;
            TranslationChineseFontCombo.ItemsSource = chineseFamilies;
        }

        private static void AddFontOptionIfMissing(List<FontFamilyOption> families, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || families.Any(option =>
                string.Equals(option.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))) return;
            families.Add(new FontFamilyOption(sourceName, sourceName));
            families.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(
                left.DisplayName,
                right.DisplayName));
        }

        private void LoadSettingsControls(OverlayPlacementSettings settings)
        {
            updatingSettings = true;
            PrimaryChineseFontCombo.SelectedValue = settings.FullscreenPrimaryChineseFontFamily;
            PrimaryEnglishFontCombo.SelectedValue = settings.FullscreenPrimaryEnglishFontFamily;
            TranslationChineseFontCombo.SelectedValue = settings.FullscreenTranslationChineseFontFamily;
            PrimarySizeSlider.Value = settings.FullscreenPrimaryFontSize;
            TranslationSizeSlider.Value = settings.FullscreenTranslationFontSize;
            LineHeightSlider.Value = settings.FullscreenLineHeightRatio;
            SelectByTag(PrimaryWeightCombo, settings.FullscreenPrimaryFontWeight.ToString());
            SelectByTag(TranslationWeightCombo, settings.FullscreenTranslationFontWeight.ToString());
            SelectByTag(MotionModeCombo, settings.FullscreenMotionMode.ToString());
            SelectByTag(IntensityCombo, settings.FullscreenMotionIntensity.ToString());
            SelectByTag(QualityCombo, settings.FullscreenRenderQuality.ToString());
            ReduceMotionCheck.IsChecked = settings.FullscreenReduceMotion;
            updatingSettings = false;
        }

        private void CaptureSettingsControls()
        {
            draftSettings.FullscreenPrimaryChineseFontFamily = PrimaryChineseFontCombo.SelectedValue as string ?? draftSettings.FullscreenPrimaryChineseFontFamily;
            draftSettings.FullscreenPrimaryEnglishFontFamily = PrimaryEnglishFontCombo.SelectedValue as string ?? draftSettings.FullscreenPrimaryEnglishFontFamily;
            var translationFont = TranslationChineseFontCombo.SelectedValue as string ?? draftSettings.FullscreenTranslationChineseFontFamily;
            draftSettings.FullscreenTranslationChineseFontFamily = translationFont;
            draftSettings.FullscreenTranslationEnglishFontFamily = translationFont;
            draftSettings.FullscreenPrimaryFontSize = PrimarySizeSlider.Value;
            draftSettings.FullscreenTranslationFontSize = TranslationSizeSlider.Value;
            draftSettings.FullscreenLineHeightRatio = LineHeightSlider.Value;
            draftSettings.FullscreenPrimaryFontWeight = ReadIntTag(PrimaryWeightCombo, 600);
            draftSettings.FullscreenTranslationFontWeight = ReadIntTag(TranslationWeightCombo, 400);
            draftSettings.FullscreenMotionMode = ReadEnumTag(MotionModeCombo, FullscreenMotionMode.Ambient);
            draftSettings.FullscreenMotionIntensity = ReadEnumTag(IntensityCombo, FullscreenMotionIntensity.Medium);
            draftSettings.FullscreenRenderQuality = ReadEnumTag(QualityCombo, FullscreenRenderQuality.High60Fps);
            draftSettings.FullscreenReduceMotion = ReduceMotionCheck.IsChecked == true;
            draftSettings.Normalize();
        }

        private void SettingControl_Changed(object sender, RoutedEventArgs e)
        {
            if (updatingSettings || draftSettings == null) return;
            CaptureSettingsControls();
            ApplyVisualSettings(draftSettings, true);
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            CaptureSettingsControls();
            SettingsApplyRequested?.Invoke(this, draftSettings.DeepClone());
            HideSettingsPanel();
        }

        private void CancelSettings_Click(object sender, RoutedEventArgs e)
        {
            draftSettings = appliedSettings.DeepClone();
            LoadSettingsControls(draftSettings); ApplyVisualSettings(draftSettings, true);
            HideSettingsPanel();
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e) { FullscreenLyricsPresets.Reset(draftSettings); LoadSettingsControls(draftSettings); ApplyVisualSettings(draftSettings, true); }
        private void ClassicPreset_Click(object sender, RoutedEventArgs e) { FullscreenLyricsPresets.ApplyClassic(draftSettings); LoadSettingsControls(draftSettings); ApplyVisualSettings(draftSettings, true); }
        private void AiryPreset_Click(object sender, RoutedEventArgs e) { FullscreenLyricsPresets.ApplyAiry(draftSettings); LoadSettingsControls(draftSettings); ApplyVisualSettings(draftSettings, true); }

        private void Translation_Click(object sender, RoutedEventArgs e)
        {
            draftSettings.FullscreenShowTranslation = !draftSettings.FullscreenShowTranslation;
            ApplyVisualSettings(draftSettings, true);
            SettingsApplyRequested?.Invoke(this, draftSettings.DeepClone());
        }

        private void LyricHitTarget_Click(object sender, RoutedEventArgs e)
        {
            if (presentation.Session?.Controls?.CanSeek != true) return;
            if (!(sender is Button button) || !(button.Tag is int index) ||
                index < 0 || index >= lyricVisuals.Count) return;
            var target = lyricVisuals[index].Line.Timestamp;
            presentation.Position = target;
            presentationReceivedUtc = DateTime.UtcNow;
            SeekRequested?.Invoke(this, target);
            UpdateLyricsPosition();
            ResumeAutoFollow();
            e.Handled = true;
        }

        private void LyricHitTarget_MouseEnter(object sender, MouseEventArgs e)
        {
            if (TryGetLyricPanel(sender, out var panel)) AnimateLyricLineSurface(panel, true, false);
        }

        private void LyricHitTarget_MouseLeave(object sender, MouseEventArgs e)
        {
            if (TryGetLyricPanel(sender, out var panel)) AnimateLyricLineSurface(panel, false, false);
        }

        private void LyricHitTarget_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && TryGetLyricPanel(sender, out var panel))
                AnimateLyricLineSurface(panel, true, true);
        }

        private void LyricHitTarget_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && TryGetLyricPanel(sender, out var panel))
                AnimateLyricLineSurface(panel, panel.IsMouseOver, false);
        }

        private bool TryGetLyricPanel(object sender, out Border panel)
        {
            panel = null;
            if (!(sender is Button button) || !(button.Tag is int index) ||
                index < 0 || index >= lyricVisuals.Count) return false;
            panel = lyricVisuals[index].Container;
            return true;
        }

        private void AnimateLyricLineSurface(Border panel, bool hovered, bool pressed)
        {
            if (!(panel.Background is SolidColorBrush brush)) return;
            var color = pressed
                ? Color.FromArgb(58, 255, 255, 255)
                : hovered ? Color.FromArgb(30, 255, 255, 255) : Colors.Transparent;
            var duration = draftSettings.FullscreenReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(pressed ? 90 : 160);
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(color, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            if (panel.Child is FrameworkElement content && content.RenderTransform is ScaleTransform transform)
            {
                var scale = pressed ? 0.985 : 1.0;
                transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(scale, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(scale, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }

        private void UpdateResponsiveLayout()
        {
            var narrow = ActualWidth < 1050 || ActualWidth / Math.Max(1, ActualHeight) < 1.25;
            ContentRoot.Margin = narrow ? new Thickness(55, 76, 55, 45) : new Thickness(ActualWidth * .07, ActualHeight * .07, ActualWidth * .07, ActualHeight * .06);
            if (narrow)
            {
                LeftColumn.Width = new GridLength(1, GridUnitType.Star); RightColumn.Width = new GridLength(0);
                TopRow.Height = new GridLength(42, GridUnitType.Star); BottomRow.Height = new GridLength(58, GridUnitType.Star);
                Grid.SetColumn(NowPlayingPanel, 0); Grid.SetRow(NowPlayingPanel, 0); Grid.SetColumn(LyricsPanel, 0); Grid.SetRow(LyricsPanel, 1);
                NowPlayingPanel.Margin = new Thickness(0, 0, 0, 22); LyricsPanel.Margin = new Thickness(0);
                ArtworkFrame.MaxHeight = Math.Max(150, ActualHeight * .24); ArtworkFrame.MaxWidth = ArtworkFrame.MaxHeight;
            }
            else
            {
                LeftColumn.Width = new GridLength(42, GridUnitType.Star); RightColumn.Width = new GridLength(58, GridUnitType.Star);
                TopRow.Height = new GridLength(1, GridUnitType.Star); BottomRow.Height = new GridLength(0);
                Grid.SetColumn(NowPlayingPanel, 0); Grid.SetRow(NowPlayingPanel, 0); Grid.SetColumn(LyricsPanel, 1); Grid.SetRow(LyricsPanel, 0);
                NowPlayingPanel.Margin = new Thickness(0, 18, 58, 0); LyricsPanel.Margin = new Thickness(25, 0, 0, 0);
                ArtworkFrame.MaxHeight = double.PositiveInfinity; ArtworkFrame.MaxWidth = 560;
            }
            var lyricViewportHeight = LyricsScroll.ViewportHeight > 0
                ? LyricsScroll.ViewportHeight
                : Math.Max(1, LyricsPanel.ActualHeight);
            LyricsStack.Margin = new Thickness(0, lyricViewportHeight * .5, 0, lyricViewportHeight * .5);
            var artSize = Math.Min(NowPlayingPanel.ActualWidth > 0 ? NowPlayingPanel.ActualWidth : ActualWidth * .30, ActualHeight * (narrow ? .24 : .43));
            ArtworkFrame.Width = ArtworkFrame.Height = Math.Max(140, artSize);
            QueueLyricRowMeasurement();
        }

        private void FadeAuxiliaryControls(bool show)
        {
            auxiliaryTimer.Stop();
            AuxiliaryControls.IsHitTestVisible = show;
            AuxiliaryControls.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(220)));
            if (show) auxiliaryTimer.Start();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e) => FadeAuxiliaryControls(true);
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout();
        private void LyricsScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Mouse wheel, touch/mouse press and lyric clicks are handled explicitly. A delayed
            // ScrollChanged from ScrollToVerticalOffset must not suspend following for five seconds.
        }
        private void LyricsScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            BeginManualBrowsing();
            QueueManualWheelScroll(e.Delta);
            e.Handled = true;
        }
        private void Previous_Click(object sender, RoutedEventArgs e) => PreviousRequested?.Invoke(this, EventArgs.Empty);
        private void PlayPause_Click(object sender, RoutedEventArgs e) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        private void Next_Click(object sender, RoutedEventArgs e) => NextRequested?.Invoke(this, EventArgs.Empty);
        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => SeekRequested?.Invoke(this, TimeSpan.FromSeconds(ProgressSlider.Value));
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (settingsPanelOpen)
            {
                HideSettingsPanel();
                return;
            }
            draftSettings = appliedSettings.DeepClone();
            LoadSettingsControls(draftSettings);
            ApplyVisualSettings(draftSettings, true);
            ShowSettingsPanel();
            auxiliaryTimer.Stop();
        }

        private void ShowSettingsPanel()
        {
            settingsPanelOpen = true;
            SettingsPanel.BeginAnimation(OpacityProperty, null);
            SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SettingsPanel.Visibility = Visibility.Visible;
            SettingsPanel.IsHitTestVisible = false;
            var start = Math.Max(430, SettingsPanel.ActualWidth + 44);
            SettingsPanelTransform.X = 0;
            SettingsPanel.Opacity = 1;
            if (draftSettings.FullscreenReduceMotion)
            {
                SettingsPanel.IsHitTestVisible = true;
                return;
            }

            var duration = TimeSpan.FromMilliseconds(330);
            var easing = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var slide = new DoubleAnimation(start, 0, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            slide.Completed += (completedSender, completedArgs) =>
            {
                if (SettingsPanel.Visibility == Visibility.Visible) SettingsPanel.IsHitTestVisible = true;
            };
            SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
            SettingsPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.18, 1, duration)
                {
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);
        }

        private void HideSettingsPanel()
        {
            if (SettingsPanel.Visibility != Visibility.Visible) return;
            settingsPanelOpen = false;
            SettingsPanel.IsHitTestVisible = false;
            SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SettingsPanel.BeginAnimation(OpacityProperty, null);
            if (draftSettings.FullscreenReduceMotion)
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                SettingsPanel.Opacity = 0;
                SettingsPanelTransform.X = 430;
                return;
            }

            var target = Math.Max(430, SettingsPanel.ActualWidth + 44);
            var duration = TimeSpan.FromMilliseconds(240);
            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
            SettingsPanelTransform.X = target;
            SettingsPanel.Opacity = 0;
            var slide = new DoubleAnimation(0, target, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            slide.Completed += (completedSender, completedArgs) => SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
            SettingsPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration)
                {
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);
        }
        private void SettingsClose_Click(object sender, RoutedEventArgs e) => ApplySettings_Click(sender, e);
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { if (SettingsPanel.Visibility == Visibility.Visible) CancelSettings_Click(sender, e); else Close(); e.Handled = true; } }
        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (WindowState != WindowState.Maximized) WindowState = WindowState.Maximized;
            UpdateResponsiveLayout();
        }));

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (allowImmediateClose || disposed) return;
            e.Cancel = true;
            if (fadeOutStarted) return;
            fadeOutStarted = true;
            AuxiliaryControls.IsHitTestVisible = false;
            SettingsPanel.IsHitTestVisible = false;
            var currentOpacity = Opacity;
            BeginAnimation(OpacityProperty, null);
            Opacity = currentOpacity;
            var animation = new DoubleAnimation(currentOpacity, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (completedSender, completedArgs) =>
            {
                allowImmediateClose = true;
                Opacity = 0;
                Close();
            };
            BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (progressRenderingSubscribed)
            {
                CompositionTarget.Rendering -= ProgressRendering;
                progressRenderingSubscribed = false;
            }
            ArtworkImage.SizeChanged -= ArtworkImage_SizeChanged;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            auxiliaryTimer?.Stop(); followResumeTimer?.Stop(); followCenterTimer?.Stop(); manualScrollTimer?.Stop(); audioTimer?.Stop();
            AmbientBackground?.Dispose(); audioAnalyzer.Dispose();
            onlineArtworkResolver.Dispose();
            volumeDevice?.Dispose();
        }

        private static string GetTrackKey(MediaSessionSnapshot session) => session == null ? string.Empty : string.Join("\u001f", session.SessionId, session.Title, session.Artist, session.Album);
        private static TimeSpan ResolveDuration(FullscreenLyricsPresentation value)
        {
            var reported = value?.Session?.Duration ?? TimeSpan.Zero;
            if (reported > TimeSpan.Zero) return reported;
            var lines = value?.Lyrics?.Lines;
            if (lines != null && lines.Count > 0)
            {
                var last = lines[lines.Count - 1].Timestamp;
                var tail = TimeSpan.FromSeconds(8);
                if (lines.Count > 1)
                {
                    var gaps = new List<double>();
                    for (var i = Math.Max(1, lines.Count - 8); i < lines.Count; i++)
                    {
                        var gap = (lines[i].Timestamp - lines[i - 1].Timestamp).TotalSeconds;
                        if (gap > 0.5 && gap < 30) gaps.Add(gap);
                    }
                    if (gaps.Count > 0) tail = TimeSpan.FromSeconds(Math.Max(5, Math.Min(18, gaps.Average() * 1.6)));
                }
                return last + tail;
            }
            return value?.Position > TimeSpan.Zero ? value.Position + TimeSpan.FromSeconds(1) : TimeSpan.Zero;
        }

        private static string ComputeArtworkKey(byte[] artwork)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(artwork));
        }
        private static string FormatArtistAlbum(MediaSessionSnapshot session) => session == null ? string.Empty : string.Join(" — ", new[] { session.Artist, session.Album }.Where(value => !string.IsNullOrWhiteSpace(value)));
        private static string FormatTime(TimeSpan value) { if (value < TimeSpan.Zero) value = TimeSpan.Zero; return value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss"); }
        private static void SelectByTag(ComboBox combo, string tag) { combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)); }
        private static int ReadIntTag(ComboBox combo, int fallback) => combo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var value) ? value : fallback;
        private static T ReadEnumTag<T>(ComboBox combo, T fallback) where T : struct => combo.SelectedItem is ComboBoxItem item && Enum.TryParse(item.Tag?.ToString(), out T value) ? value : fallback;

        private sealed class LyricVisual
        {
            public LyricVisual(
                LyricLine line,
                Border container,
                Grid content,
                StackPanel activeContent,
                WordTrackingTextBlock primary,
                TextBlock translation,
                Button hitTarget)
            {
                Line = line;
                Container = container;
                Content = content;
                ActiveContent = activeContent;
                Primary = primary;
                Translation = translation;
                HitTarget = hitTarget;
            }
            public LyricLine Line { get; }
            public Border Container { get; }
            public Grid Content { get; }
            public StackPanel ActiveContent { get; }
            public WordTrackingTextBlock Primary { get; }
            public TextBlock Translation { get; }
            public Button HitTarget { get; }
            public bool? IsCurrentVisual { get; set; }
            public bool WasTrackingCurrent { get; set; }
            public bool WordTrackingPrepared { get; set; }
            public double? TargetOpacity { get; set; }
            public double ReservedHeight { get; set; }
        }
    }
}
