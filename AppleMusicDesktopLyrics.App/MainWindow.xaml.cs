using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AppleMusicDesktopLyrics.Core;
using Forms = System.Windows.Forms;

namespace AppleMusicDesktopLyrics.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer hoverProximityTimer;
        private readonly PowerShellNowPlayingProvider nowPlayingProvider;
        private readonly LyricsCache cache;
        private readonly OverlaySettingsStore settingsStore;
        private readonly ScreenCatalog screenCatalog = new ScreenCatalog();
        private ILyricsClient lyricsClient;
        private TimedLyrics currentLyrics = new TimedLyrics(new LyricLine[0]);
        private TrackIdentity currentTrack;
        private OverlayPlacementSettings placementSettings;
        private LyricsSourcePreference selectedLyricsSource = LyricsSourcePreference.Automatic;
        private bool refreshingState;
        private bool lyricsSearchFinished;
        private bool islandVisible;
        private TimeSpan lyricOffset = TimeSpan.FromMilliseconds(800);
        private DispatcherTimer startupHintTimer;
        private readonly LyricTextTransitionTracker lyricTextTransitionTracker = new LyricTextTransitionTracker();
        private string displayedPrimary;
        private string displayedSecondary;
        private int positionAnimationVersion;
        private bool horizontalDragActive;
        private RadialGradientBrush backgroundHoverOpacityMask;
        private RadialGradientBrush lyricsHoverOpacityMask;
        private int hoverFadeAnimationVersion;
        private bool hoverFadeOutActive;

        public MainWindow()
        {
            InitializeComponent();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheRoot = System.IO.Path.Combine(appData, "AppleMusicDesktopLyrics", "lyrics");
            var settingsPath = System.IO.Path.Combine(appData, "AppleMusicDesktopLyrics", "settings.json");
            var scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "now-playing.ps1");

            nowPlayingProvider = new PowerShellNowPlayingProvider(scriptPath);
            settingsStore = new OverlaySettingsStore(settingsPath);
            placementSettings = settingsStore.Load();
            selectedLyricsSource = placementSettings.LyricsSource;
            cache = new LyricsCache(cacheRoot, GetCacheLimitBytes(placementSettings));
            UpdateIslandShape();
            lyricsClient = CreateLyricsClient(selectedLyricsSource);

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += async (sender, args) => await RefreshAsync();
            timer.Start();

            hoverProximityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            hoverProximityTimer.Tick += (sender, args) => UpdateHoverProximity();

            Loaded += (sender, args) =>
            {
                HideIsland(false);
                Focus();
                ShowWaitingForPlaybackHint();
            };
        }

        public void ShowWaitingForPlaybackHint()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();

            if (currentTrack != null)
            {
                ShowIsland();
                return;
            }

            SetIslandText("Apple Music 桌面歌词已启动", "等待 Apple Music 播放...");
            ShowIsland();
            if (startupHintTimer == null)
            {
                startupHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                startupHintTimer.Tick += (sender, args) =>
                {
                    startupHintTimer.Stop();
                    if (currentTrack == null)
                    {
                        HideIsland(true);
                    }
                };
            }

            startupHintTimer.Stop();
            startupHintTimer.Start();
        }

        private async Task RefreshAsync()
        {
            if (refreshingState)
            {
                return;
            }

            refreshingState = true;
            try
            {
                var state = await nowPlayingProvider.GetCurrentAsync();
                if (PlaybackVisibilityPolicy.ShouldHide(state.HasSession, state.Title, state.IsPlaying, IsStartupHintActive()))
                {
                    if (!state.HasSession || string.IsNullOrWhiteSpace(state.Title))
                    {
                        currentTrack = null;
                        currentLyrics = new TimedLyrics(new LyricLine[0]);
                        lyricsSearchFinished = false;
                    }

                    HideIsland(true);
                    return;
                }

                var track = TrackIdentityCleaner.Clean(new TrackIdentity(state.Title, state.Artist, TimeSpan.FromSeconds(state.DurationSeconds), state.Album));
                if (IsNewTrack(track))
                {
                    currentTrack = track;
                    currentLyrics = new TimedLyrics(new LyricLine[0]);
                    lyricsSearchFinished = false;
                    SetIslandText(FormatTrack(track), "正在搜索同步歌词...");
                    ShowIsland();
                    _ = LoadLyricsAsync(track, false);
                    return;
                }

                if (currentLyrics.Lines.Count == 0)
                {
                    if (lyricsSearchFinished)
                    {
                        SetIslandText(FormatTrack(track), "未找到同步歌词");
                        ShowIsland();
                    }
                    else
                    {
                        SetIslandText(FormatTrack(track), "正在搜索同步歌词...");
                        ShowIsland();
                    }

                    return;
                }

                var lines = LyricsDisplaySelector.Select(
                    currentLyrics,
                    TimeSpan.FromSeconds(state.PositionSeconds),
                    lyricOffset,
                    placementSettings.UseMultiLineDisplay,
                    placementSettings.ShowTranslation);
                var lineDuration = currentLyrics.GetCurrentLineDuration(
                    TimeSpan.FromSeconds(state.PositionSeconds),
                    lyricOffset,
                    TimeSpan.FromSeconds(4));
                SetIslandText(lines.Count > 0 ? lines[0].Text : string.Empty, lines.Count > 1 ? lines[1].Text : string.Empty, lineDuration);
                ShowIsland();
            }
            catch (Exception ex)
            {
                SetIslandText("读取播放状态失败", ex.Message);
                ShowIsland();
            }
            finally
            {
                refreshingState = false;
            }
        }

        private async Task LoadLyricsAsync(TrackIdentity track, bool forceRefresh)
        {
            try
            {
                string lrc;
                var cacheHit = cache.TryRead(track, out lrc);
                if (forceRefresh || !cacheHit || (placementSettings.ShowTranslation && !LyricsPackageParser.HasTranslation(lrc)))
                {
                    var freshLrc = await lyricsClient.GetSyncedLyricsAsync(track);
                    if (!string.IsNullOrWhiteSpace(freshLrc))
                    {
                        lrc = freshLrc;
                        cache.Write(track, lrc);
                    }
                }

                if (IsSameTrack(track, currentTrack))
                {
                    currentLyrics = LyricsPackageParser.Parse(lrc);
                    lyricsSearchFinished = true;
                }
            }
            catch
            {
                if (IsSameTrack(track, currentTrack))
                {
                    currentLyrics = new TimedLyrics(new LyricLine[0]);
                    lyricsSearchFinished = true;
                }
            }
        }

        private bool IsNewTrack(TrackIdentity track)
        {
            return currentTrack == null ||
                !currentTrack.Title.Equals(track.Title, StringComparison.OrdinalIgnoreCase) ||
                !currentTrack.Artist.Equals(track.Artist, StringComparison.OrdinalIgnoreCase) ||
                Math.Abs((currentTrack.Duration - track.Duration).TotalSeconds) > 2;
        }

        private static bool IsSameTrack(TrackIdentity first, TrackIdentity second)
        {
            return first != null &&
                second != null &&
                first.Title.Equals(second.Title, StringComparison.OrdinalIgnoreCase) &&
                first.Artist.Equals(second.Artist, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs((first.Duration - second.Duration).TotalSeconds) <= 2;
        }

        private static string FormatTrack(TrackIdentity track)
        {
            if (string.IsNullOrWhiteSpace(track.Artist))
            {
                return track.Title;
            }

            return track.Title + " - " + track.Artist;
        }

        private void ShowIsland()
        {
            var point = GetVisiblePosition();
            AnimateTo(point.Left, point.Top);
            islandVisible = true;
            hoverProximityTimer.Start();
            UpdateHoverProximity();
        }

        private void HideIsland(bool animated)
        {
            hoverProximityTimer.Stop();
            HideHoverTransparency();
            var point = GetHiddenPosition();
            if (animated)
            {
                AnimateTo(point.Left, point.Top);
            }
            else
            {
                ClearPositionAnimation();
                Left = point.Left;
                Top = point.Top;
            }

            islandVisible = false;
        }

        private void AnimateTo(double targetLeft, double targetTop)
        {
            var version = ++positionAnimationVersion;
            var leftAnimation = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var topAnimation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            topAnimation.Completed += (sender, args) =>
            {
                if (version != positionAnimationVersion)
                {
                    return;
                }

                ClearPositionAnimation();
                Left = targetLeft;
                Top = targetTop;
            };
            BeginAnimation(LeftProperty, leftAnimation);
            BeginAnimation(TopProperty, topAnimation);
        }

        private void ClearPositionAnimation()
        {
            positionAnimationVersion++;
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
        }

        private void ShowHoverTransparency(Point localPoint)
        {
            ShowHoverTransparency(localPoint, 1.0);
        }

        private void ShowHoverTransparency(Point localPoint, double intensity)
        {
            if (hoverFadeOutActive)
            {
                HideHoverTransparency();
            }

            if (backgroundHoverOpacityMask == null)
            {
                var backgroundRadius = GetHoverMaskRadius(1.0);
                var lyricsRadius = GetHoverMaskRadius(0.9);
                backgroundHoverOpacityMask = CreateHoverOpacityMask(
                    backgroundRadius.Width,
                    backgroundRadius.Height,
                    placementSettings.HoverSpectrumStops,
                    0);
                lyricsHoverOpacityMask = CreateHoverOpacityMask(
                    lyricsRadius.Width,
                    lyricsRadius.Height,
                    placementSettings.HoverSpectrumStops,
                    16);
                IslandShape.OpacityMask = backgroundHoverOpacityMask;
                LyricsContent.OpacityMask = lyricsHoverOpacityMask;
            }

            UpdateHoverOpacityMaskIntensity(backgroundHoverOpacityMask, placementSettings.HoverSpectrumStops, 0, intensity);
            UpdateHoverOpacityMaskIntensity(lyricsHoverOpacityMask, placementSettings.HoverSpectrumStops, 16, intensity);

            backgroundHoverOpacityMask.Center = localPoint;
            backgroundHoverOpacityMask.GradientOrigin = localPoint;

            var lyricsPoint = IslandShell.TranslatePoint(localPoint, LyricsContent);
            lyricsHoverOpacityMask.Center = lyricsPoint;
            lyricsHoverOpacityMask.GradientOrigin = lyricsPoint;
        }

        private Size GetHoverMaskRadius(double scale)
        {
            var aspectRatio = Math.Max(OverlayPlacementSettings.MinHoverAuraAspectRatio, Math.Min(OverlayPlacementSettings.MaxHoverAuraAspectRatio, placementSettings.HoverAuraAspectRatio));
            var shapeScale = Math.Sqrt(aspectRatio);
            return new Size(
                placementSettings.HoverAuraSize * scale * shapeScale,
                placementSettings.HoverAuraSize * scale / shapeScale);
        }

        private static RadialGradientBrush CreateHoverOpacityMask(double radiusX, double radiusY, System.Collections.Generic.IEnumerable<HoverSpectrumStop> spectrumStops, int extraTransparencyPercent)
        {
            var mask = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                RadiusX = radiusX,
                RadiusY = radiusY
            };
            foreach (var stop in (spectrumStops ?? OverlayPlacementSettings.CreateDefaultHoverSpectrumStops()).OrderBy(item => item.PositionPercent))
            {
                var transparency = Math.Max(0, Math.Min(100, stop.TransparencyPercent + extraTransparencyPercent));
                mask.GradientStops.Add(new GradientStop(
                    Color.FromArgb(GetOpacityAlpha(transparency), 255, 255, 255),
                    Math.Max(0, Math.Min(1, stop.PositionPercent / 100.0))));
            }

            return mask;
        }

        private static void UpdateHoverOpacityMaskIntensity(RadialGradientBrush mask, System.Collections.Generic.IEnumerable<HoverSpectrumStop> spectrumStops, int extraTransparencyPercent, double intensity)
        {
            var stops = (spectrumStops ?? OverlayPlacementSettings.CreateDefaultHoverSpectrumStops())
                .OrderBy(item => item.PositionPercent)
                .ToArray();
            var normalizedIntensity = Math.Max(0, Math.Min(1, intensity));
            for (var index = 0; index < mask.GradientStops.Count && index < stops.Length; index++)
            {
                var transparency = Math.Max(0, Math.Min(100, stops[index].TransparencyPercent + extraTransparencyPercent));
                var fadedTransparency = (int)Math.Round(transparency * normalizedIntensity);
                mask.GradientStops[index].Color = Color.FromArgb(GetOpacityAlpha(fadedTransparency), 255, 255, 255);
            }
        }

        private void UpdateHoverProximity()
        {
            if (!islandVisible || !IsVisible)
            {
                HideHoverTransparency();
                return;
            }

            var cursor = Forms.Cursor.Position;
            var localPoint = PointFromScreen(new Point(cursor.X, cursor.Y));
            var detectionRange = GetHoverDetectionRange();
            var distance = GetDistanceToIsland(localPoint);
            if (distance >= detectionRange)
            {
                if (!horizontalDragActive)
                {
                    FadeOutHoverTransparency();
                }

                return;
            }

            var intensity = 1.0 - Math.Max(0, distance) / detectionRange;
            intensity = 1.0 - Math.Pow(1.0 - intensity, 2.0);
            ShowHoverTransparency(ClampToIsland(localPoint), intensity);
        }

        private double GetHoverDetectionRange()
        {
            return Math.Max(OverlayPlacementSettings.MinHoverDetectionRange, placementSettings.HoverDetectionRange);
        }

        private double GetDistanceToIsland(Point localPoint)
        {
            var dx = Math.Max(Math.Max(-localPoint.X, 0), localPoint.X - Width);
            var dy = Math.Max(Math.Max(-localPoint.Y, 0), localPoint.Y - Height);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private Point ClampToIsland(Point localPoint)
        {
            return new Point(
                Math.Max(0, Math.Min(Width, localPoint.X)),
                Math.Max(0, Math.Min(Height, localPoint.Y)));
        }

        private void HideHoverTransparency()
        {
            hoverFadeAnimationVersion++;
            hoverFadeOutActive = false;
            IslandShape.OpacityMask = null;
            LyricsContent.OpacityMask = null;
            backgroundHoverOpacityMask = null;
            lyricsHoverOpacityMask = null;
        }

        private void FadeOutHoverTransparency()
        {
            if (backgroundHoverOpacityMask == null || lyricsHoverOpacityMask == null || hoverFadeOutActive)
            {
                return;
            }

            hoverFadeOutActive = true;
            var version = ++hoverFadeAnimationVersion;
            var attachedCompletion = false;
            foreach (var stop in backgroundHoverOpacityMask.GradientStops.Concat(lyricsHoverOpacityMask.GradientStops))
            {
                var animation = new ColorAnimation
                {
                    To = Color.FromArgb(255, 255, 255, 255),
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                if (!attachedCompletion)
                {
                    animation.Completed += (sender, args) =>
                    {
                        if (version == hoverFadeAnimationVersion)
                        {
                            HideHoverTransparency();
                        }
                    };
                    attachedCompletion = true;
                }

                stop.BeginAnimation(GradientStop.ColorProperty, animation);
            }
        }

        private bool IsStartupHintActive()
        {
            return startupHintTimer != null && startupHintTimer.IsEnabled;
        }

        private OverlayPoint GetVisiblePosition()
        {
            var screen = ResolveScreen();
            return OverlayPositioner.GetVisiblePosition(placementSettings.ToPlacement(), screen, GetOverlaySize());
        }

        private OverlayPoint GetHiddenPosition()
        {
            var screen = ResolveScreen();
            return OverlayPositioner.GetHiddenPosition(placementSettings.ToPlacement(), screen, GetOverlaySize());
        }

        private OverlayScreenArea ResolveScreen()
        {
            var screens = screenCatalog.GetScreens();
            var screen = screens.FirstOrDefault(item => item.Name == placementSettings.ScreenName) ?? screens.FirstOrDefault();
            if (screen != null && screen.Name != placementSettings.ScreenName)
            {
                placementSettings.ScreenName = screen.Name;
            }

            return screen ?? new OverlayScreenArea("primary", 0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight, 0, 0, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        }

        private OverlaySize GetOverlaySize()
        {
            return new OverlaySize(Width, Height);
        }

        private void ApplyPlacementSettings(OverlayPlacementSettings settings)
        {
            var previousSource = placementSettings.LyricsSource;
            var previousShowTranslation = placementSettings.ShowTranslation;
            placementSettings = settings ?? new OverlayPlacementSettings();
            placementSettings.Normalize();
            cache.SetMaxBytes(GetCacheLimitBytes(placementSettings));
            selectedLyricsSource = placementSettings.LyricsSource;
            lyricsClient = CreateLyricsClient(selectedLyricsSource);
            settingsStore.Save(placementSettings);
            UpdateIslandShape();
            if (currentTrack != null && previousSource != placementSettings.LyricsSource)
            {
                RefreshCurrentTrackLyrics(true);
                return;
            }

            if (currentTrack != null && !previousShowTranslation && placementSettings.ShowTranslation)
            {
                RefreshCurrentTrackLyrics(false);
                return;
            }

            if (currentTrack == null)
            {
                ShowWaitingForPlaybackHint();
            }
            else
            {
                ShowIsland();
            }
        }

        private void SnapCurrentPositionToNearestEdge()
        {
            var screens = screenCatalog.GetScreens();
            var placement = OverlayPositioner.SnapToNearestEdge(Left, Top, GetOverlaySize(), screens);
            placementSettings = CreateSettingsFromPlacement(placement);
            settingsStore.Save(placementSettings);
            UpdateIslandShape();
            if (currentTrack == null)
            {
                ShowWaitingForPlaybackHint();
            }
            else
            {
                ShowIsland();
            }
        }

        private void MoveOverlayToPointer(MouseEventArgs e)
        {
            var pointer = GetPointerScreenPoint(e.GetPosition(this));
            var screens = screenCatalog.GetScreens();
            var placement = OverlayPositioner.GetHorizontalDragPlacement(pointer.X, pointer.Y, GetOverlaySize(), screens);
            placementSettings = CreateSettingsFromPlacement(placement);

            var screen = screenCatalog.FindScreen(placement.ScreenName) ?? ResolveScreen();
            var position = OverlayPositioner.GetVisiblePosition(placement, screen, GetOverlaySize());
            ClearPositionAnimation();
            Left = position.Left;
            Top = position.Top;
        }

        private Point GetPointerScreenPoint(Point localPoint)
        {
            var point = PointToScreen(localPoint);
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                point = source.CompositionTarget.TransformFromDevice.Transform(point);
            }

            return point;
        }

        private void FinishHorizontalDrag()
        {
            if (!horizontalDragActive)
            {
                return;
            }

            horizontalDragActive = false;
            ReleaseMouseCapture();
            settingsStore.Save(placementSettings);
            UpdateIslandShape();
            UpdateHoverProximity();
        }

        private void UpdateIslandShape()
        {
            IslandShape.Data = Geometry.Parse(OverlayShapePath.GetPath(placementSettings.Edge));
            IslandShape.Visibility = Visibility.Visible;
            IslandBackground.Opacity = 1.0;
            HideHoverTransparency();

            var primaryBrush = Brushes.White;
            var secondaryBrush = new SolidColorBrush(Color.FromArgb(217, 255, 255, 255));

            PrimaryLyricText.Foreground = primaryBrush;
            IncomingPrimaryLyricText.Foreground = primaryBrush;
            SecondaryLyricText.Foreground = secondaryBrush;
            IncomingSecondaryLyricText.Foreground = secondaryBrush;
        }

        private void SetIslandText(string primary, string secondary)
        {
            SetIslandText(primary, secondary, TimeSpan.FromSeconds(4));
        }

        private void SetIslandText(string primary, string secondary, TimeSpan lineDuration)
        {
            primary = primary ?? string.Empty;
            secondary = secondary ?? string.Empty;

            if (displayedPrimary == primary && displayedSecondary == secondary)
            {
                return;
            }

            var shouldAnimate = lyricTextTransitionTracker.Update(primary, secondary);
            displayedPrimary = primary;
            displayedSecondary = secondary;

            if (!shouldAnimate)
            {
                ApplyCurrentLyricsText(primary, secondary);
                ResetLyricsAnimationState();
                QueueMarquee(lineDuration);
                return;
            }

            IncomingPrimaryLyricText.Text = primary;
            IncomingSecondaryLyricText.Text = secondary;
            IncomingSecondaryLyricText.Visibility = string.IsNullOrWhiteSpace(secondary) ? Visibility.Collapsed : Visibility.Visible;
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
                ApplyCurrentLyricsText(primary, secondary);
                ResetLyricsAnimationState();
                QueueMarquee(lineDuration);
            };

            CurrentLyricsPanel.BeginAnimation(OpacityProperty, outgoingOpacity);
            CurrentLyricsTransform.BeginAnimation(TranslateTransform.YProperty, outgoingMove);
            IncomingLyricsPanel.BeginAnimation(OpacityProperty, incomingOpacity);
            IncomingLyricsTransform.BeginAnimation(TranslateTransform.YProperty, incomingMove);
        }

        private void ApplyCurrentLyricsText(string primary, string secondary)
        {
            StopMarquee();
            PrimaryLyricText.Text = primary ?? string.Empty;
            SecondaryLyricText.Text = secondary ?? string.Empty;
            SecondaryLyricText.Visibility = string.IsNullOrWhiteSpace(secondary) ? Visibility.Collapsed : Visibility.Visible;
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

        private void QueueMarquee(TimeSpan lineDuration)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StartMarqueeIfNeeded(PrimaryLyricText, PrimaryLyricTransform, PrimaryLyricClip, lineDuration);
                StartMarqueeIfNeeded(SecondaryLyricText, SecondaryLyricTransform, SecondaryLyricClip, lineDuration);
            }), DispatcherPriority.Background);
        }

        private void StartMarqueeIfNeeded(System.Windows.Controls.TextBlock textBlock, TranslateTransform transform, FrameworkElement clip, TimeSpan lineDuration)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            textBlock.Width = double.NaN;
            System.Windows.Controls.Canvas.SetLeft(textBlock, 0);

            if (string.IsNullOrWhiteSpace(textBlock.Text) || textBlock.Visibility != Visibility.Visible)
            {
                return;
            }

            var availableWidth = clip.ActualWidth;
            if (availableWidth <= 0)
            {
                availableWidth = 436;
            }

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = textBlock.DesiredSize.Width;
            textBlock.Width = textWidth;

            if (textWidth <= availableWidth)
            {
                System.Windows.Controls.Canvas.SetLeft(textBlock, (availableWidth - textWidth) / 2);
                return;
            }

            var overflow = textWidth - availableWidth + 28;
            var duration = TimeSpan.FromMilliseconds(Math.Max(1800, lineDuration.TotalMilliseconds - 450));
            System.Windows.Controls.Canvas.SetLeft(textBlock, 0);

            var animation = new DoubleAnimation
            {
                From = 0,
                To = -overflow,
                BeginTime = TimeSpan.FromMilliseconds(260),
                Duration = duration,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void StopMarquee()
        {
            PrimaryLyricTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SecondaryLyricTransform.BeginAnimation(TranslateTransform.XProperty, null);
            PrimaryLyricTransform.X = 0;
            SecondaryLyricTransform.X = 0;
            PrimaryLyricText.Width = double.NaN;
            SecondaryLyricText.Width = double.NaN;
            System.Windows.Controls.Canvas.SetLeft(PrimaryLyricText, 0);
            System.Windows.Controls.Canvas.SetLeft(SecondaryLyricText, 0);
        }

        private void RefreshCurrentTrackLyrics(bool forceRefresh)
        {
            if (currentTrack == null)
            {
                return;
            }

            currentLyrics = new TimedLyrics(new LyricLine[0]);
            lyricsSearchFinished = false;
            SetIslandText(FormatTrack(currentTrack), "正在搜索同步歌词...");
            ShowIsland();
            _ = LoadLyricsAsync(currentTrack, forceRefresh);
        }

        private void OpenPlacementSettingsWindow()
        {
            var window = new PlacementSettingsWindow(screenCatalog.GetScreens(), placementSettings, ApplyPlacementSettings)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private static ILyricsClient CreateLyricsClient(LyricsSourcePreference source)
        {
            return new CompositeLyricsClient(CreateLyricsSourceChain(source).Select(CreateSingleLyricsClient).ToArray());
        }

        private static LyricsSourcePreference[] CreateLyricsSourceChain(LyricsSourcePreference preferredSource)
        {
            var fallbackOrder = new[]
            {
                LyricsSourcePreference.LrcLib,
                LyricsSourcePreference.QQMusic,
                LyricsSourcePreference.KuGou,
                LyricsSourcePreference.NetEase
            };

            if (preferredSource == LyricsSourcePreference.Automatic)
            {
                return fallbackOrder;
            }

            return new[] { preferredSource }
                .Concat(fallbackOrder.Where(source => source != preferredSource))
                .ToArray();
        }

        private static ILyricsClient CreateSingleLyricsClient(LyricsSourcePreference source)
        {
            switch (source)
            {
                case LyricsSourcePreference.LrcLib:
                    return new LrcLibClient();
                case LyricsSourcePreference.QQMusic:
                    return new QQMusicLyricsClient();
                case LyricsSourcePreference.KuGou:
                    return new KuGouLyricsClient();
                case LyricsSourcePreference.NetEase:
                    return new NetEaseLyricsClient();
                default:
                    return new LrcLibClient();
            }
        }

        private static long GetCacheLimitBytes(OverlayPlacementSettings settings)
        {
            var megabytes = settings?.CacheLimitMegabytes ?? LyricsCache.DefaultMaxMegabytes;
            return megabytes * 1024L * 1024L;
        }

        private OverlayPlacementSettings CreateSettingsFromPlacement(OverlayPlacement placement)
        {
            return new OverlayPlacementSettings
            {
                ScreenName = placement.ScreenName,
                Edge = placement.Edge,
                OffsetRatio = placement.OffsetRatio,
                CacheLimitMegabytes = placementSettings.CacheLimitMegabytes,
                HoverAuraSize = placementSettings.HoverAuraSize,
                HoverDetectionRange = placementSettings.HoverDetectionRange,
                HoverAuraAspectRatio = placementSettings.HoverAuraAspectRatio,
                HoverTransparencyPercent = placementSettings.HoverTransparencyPercent,
                HoverSpectrumStops = placementSettings.HoverSpectrumStops,
                LyricsSource = placementSettings.LyricsSource,
                UseMultiLineDisplay = placementSettings.UseMultiLineDisplay,
                ShowTranslation = placementSettings.ShowTranslation
            };
        }

        private static byte GetOpacityAlpha(int transparencyPercent)
        {
            var normalized = Math.Max(0, Math.Min(100, transparencyPercent));
            return (byte)Math.Round(255 * (100 - normalized) / 100.0);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            ClearPositionAnimation();
            horizontalDragActive = true;
            CaptureMouse();
            MoveOverlayToPointer(e);
            e.Handled = true;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (islandVisible)
            {
                ShowHoverTransparency(e.GetPosition(IslandShell));
            }

            if (!horizontalDragActive)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                FinishHorizontalDrag();
                return;
            }

            MoveOverlayToPointer(e);
            e.Handled = true;
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            FinishHorizontalDrag();
            e.Handled = true;
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            OpenPlacementSettingsWindow();
            e.Handled = true;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            if (islandVisible)
            {
                ShowHoverTransparency(e.GetPosition(IslandShell));
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            UpdateHoverProximity();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right || e.Key == Key.Up)
            {
                lyricOffset += TimeSpan.FromMilliseconds(200);
            }
            else if (e.Key == Key.Left || e.Key == Key.Down)
            {
                lyricOffset -= TimeSpan.FromMilliseconds(200);
            }
            else if (e.Key == Key.R)
            {
                lyricOffset = TimeSpan.FromMilliseconds(800);
            }
        }
    }
}
