using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;
using AppleMusicDesktopLyrics.App.LayoutEditing;
using AppleMusicDesktopLyrics.App.Modules;
using AppleMusicDesktopLyrics.Core;
using AppleMusicDesktopLyrics.App.Media;
using AppleMusicDesktopLyrics.Core.Layout;
using AppleMusicDesktopLyrics.Core.Media;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AppleMusicDesktopLyrics.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer hoverProximityTimer;
        private readonly IMediaSessionService mediaSessions;
        private readonly TimelineCoordinator timelineCoordinator;
        private readonly IslandInteractionController interactionController = new IslandInteractionController();
        private readonly DateTimeOffset interactionClockOriginUtc = DateTimeOffset.UtcNow;
        private MediaSessionSnapshot currentSession;
        private GlobalHotkeyService hotkeyService;
        private DateTimeOffset? pausedSinceUtc;
        private DateTimeOffset? noPlaybackSinceUtc;
        private int lyricLoadGeneration;
        private readonly LyricsCache cache;
        private readonly OverlaySettingsStore settingsStore;
        private readonly ScreenCatalog screenCatalog = new ScreenCatalog();
        private ILyricsClient lyricsClient;
        private TimedLyrics currentLyrics = new TimedLyrics(new LyricLine[0]);
        private TrackIdentity currentTrack;
        private OverlayPlacementSettings placementSettings;
        private LyricsSourcePreference selectedLyricsSource = LyricsSourcePreference.Automatic;
        private LayoutEditSession layoutEditSession;
        private IslandLayoutMode layoutEditingMode = IslandLayoutMode.HorizontalBlocks;
        private bool layoutEditing;
        private bool refreshingState;
        private bool lyricsSearchFinished;
        private bool islandVisible;
        private TimeSpan lyricOffset = TimeSpan.FromMilliseconds(800);
        private TimeSpan currentEffectivePosition;
        private TimelineReliability currentTimelineReliability;
        private DispatcherTimer startupHintTimer;
        private bool startupHintAwaitingConfirmation;
        private Forms.NotifyIcon trayIcon;
        private int positionAnimationVersion;
        private bool horizontalDragActive;
        private bool horizontalDragPending;
        private Point horizontalDragStartScreenPoint;
        private RadialGradientBrush backgroundHoverOpacityMask;
        private RadialGradientBrush lyricsHoverOpacityMask;
        private int hoverFadeAnimationVersion;
        private bool hoverFadeOutActive;
        private IslandInteractionState appliedInteractionState = IslandInteractionState.Collapsed;
        private PlacementSettingsWindow settingsWindow;
        private int islandSizeAnimationVersion;
        private bool moduleDragActive;
        private const double DragStartThreshold = 4.0;
        private const double IslandHorizontalShapePadding = 144;
        private const double HoverMaskContentRadiusScale = 1.0;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int VK_RBUTTON = 0x02;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        public MainWindow()
        {
            InitializeComponent();
            ModuleHost.PreviousRequested += async (sender, args) => await PreviousRequested();
            ModuleHost.PlayPauseRequested += async (sender, args) => await PlayPauseRequested();
            ModuleHost.NextRequested += async (sender, args) => await NextRequested();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheRoot = System.IO.Path.Combine(appData, "AppleMusicDesktopLyrics", "lyrics");
            var settingsPath = System.IO.Path.Combine(appData, "AppleMusicDesktopLyrics", "settings.json");

            mediaSessions = new SmTcMediaSessionService();
            timelineCoordinator = new TimelineCoordinator(new StopwatchClock());
            mediaSessions.SessionsChanged += (sender, args) =>
                Dispatcher.BeginInvoke(new Action(async () => await RefreshAsync()));
            settingsStore = new OverlaySettingsStore(settingsPath);
            placementSettings = settingsStore.Load();
            selectedLyricsSource = placementSettings.LyricsSource;
            lyricOffset = TimeSpan.FromMilliseconds(placementSettings.DefaultLyricOffsetMilliseconds);
            interactionController.ExpandedDuration = TimeSpan.FromSeconds(placementSettings.ExpandedAutoCollapseSeconds);
            cache = new LyricsCache(cacheRoot, GetCacheLimitBytes(placementSettings));
            UpdateIslandShape();
            lyricsClient = CreateLyricsClient(selectedLyricsSource);
            InitializeTrayIcon();

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += async (sender, args) => await RefreshAsync();
            timer.Start();

            hoverProximityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            hoverProximityTimer.Tick += (sender, args) => UpdateHoverProximity();

            Loaded += async (sender, args) =>
            {
                HideIsland(false);
                Focus();
                ShowWaitingForPlaybackHint();
                try
                {
                    await mediaSessions.InitializeAsync();
                    await RefreshAsync();
                }
                catch (Exception ex)
                {
                    SetIslandText("读取播放状态失败", ex.Message);
                    ShowIsland();
                }
            };
            SourceInitialized += (sender, args) =>
            {
                InstallWindowMessageHook();
                RegisterGlobalHotkeys();
            };
            Closed += (sender, args) =>
            {
                timer.Stop();
                hotkeyService?.Dispose();
                mediaSessions.Dispose();
                DisposeTrayIcon();
            };
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon
            {
                Text = "Lyric Island 歌词岛",
                Icon = LoadTrayIcon(),
                ContextMenuStrip = new Forms.ContextMenuStrip()
            };
            trayIcon.ContextMenuStrip.Items.Add("偏好设置", null, (sender, args) => Dispatcher.BeginInvoke(new Action(OpenPlacementSettingsWindow)));
            trayIcon.ContextMenuStrip.Items.Add("退出", null, (sender, args) => Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown())));
            trayIcon.DoubleClick += (sender, args) => Dispatcher.BeginInvoke(new Action(OpenPlacementSettingsWindow));
            trayIcon.Visible = true;
        }

        private static Drawing.Icon LoadTrayIcon()
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                return new Drawing.Icon(iconPath);
            }

            return Drawing.SystemIcons.Application;
        }

        private void DisposeTrayIcon()
        {
            if (trayIcon == null)
            {
                return;
            }

            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        private void InstallWindowMessageHook()
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessageHook);
        }

        private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (hotkeyService != null && hotkeyService.HandleMessage(msg, wParam))
            {
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_NCHITTEST)
            {
                handled = false;
            }

            return IntPtr.Zero;
        }

        private void RegisterGlobalHotkeys()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            hotkeyService?.Dispose();
            hotkeyService = new GlobalHotkeyService(hwnd);
            var configured = placementSettings?.LyricOffsetHotkeys ?? HotkeySettings.CreateDefault();
            RegisterConfiguredHotkey(1, configured.Earlier, () => AdjustLyricOffset(-500, false));
            RegisterConfiguredHotkey(2, configured.Later, () => AdjustLyricOffset(500, false));
            RegisterConfiguredHotkey(3, configured.Reset, () => AdjustLyricOffset(0, true));
        }

        private void RegisterConfiguredHotkey(int id, string gesture, Action action)
        {
            uint modifiers;
            uint virtualKey;
            if (HotkeyGestureParser.TryParseGlobal(gesture, out modifiers, out virtualKey))
            {
                hotkeyService.Register(id, modifiers, virtualKey, action);
            }
        }

        private void AdjustLyricOffset(int deltaMilliseconds, bool reset)
        {
            var milliseconds = reset
                ? placementSettings.DefaultLyricOffsetMilliseconds
                : (int)lyricOffset.TotalMilliseconds + deltaMilliseconds;
            milliseconds = Math.Max(-10000, Math.Min(10000, milliseconds));
            lyricOffset = TimeSpan.FromMilliseconds(milliseconds);
            ModuleHost.ShowTransientMessage(
                "歌词偏移 " + (milliseconds / 1000.0).ToString("+0.0;-0.0;0.0") + "s",
                TimeSpan.FromSeconds(1.2));
        }

        private bool ShouldPassThroughMouseHit()
        {
            if (placementSettings == null ||
                !placementSettings.PassThroughOnHover ||
                layoutEditing ||
                !islandVisible ||
                !IsVisible ||
                IsRightMouseButtonDown())
            {
                return false;
            }

            var cursor = Forms.Cursor.Position;
            var localPoint = PointFromScreen(new Point(cursor.X, cursor.Y));
            return localPoint.X >= 0 &&
                localPoint.X <= Width &&
                localPoint.Y >= 0 &&
                localPoint.Y <= Height;
        }

        private static bool IsRightMouseButtonDown()
        {
            return (GetAsyncKeyState(VK_RBUTTON) & unchecked((short)0x8000)) != 0;
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

            startupHintAwaitingConfirmation = true;
            startupHintTimer?.Stop();
            SetIslandText("歌词岛已启动，等待播放内容", "这不是故障：点击歌词岛或按任意键确认");
            ShowIsland();
            if (startupHintTimer == null)
            {
                startupHintTimer = new DispatcherTimer();
                startupHintTimer.Tick += (sender, args) =>
                {
                    startupHintTimer.Stop();
                    startupHintAwaitingConfirmation = false;
                    if (currentTrack == null)
                    {
                        noPlaybackSinceUtc = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(placementSettings.NoPlaybackAutoRetractSeconds);
                        HideIsland(true);
                    }
                };
            }
            startupHintTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, placementSettings.NoPlaybackAutoRetractSeconds));
        }

        private void ConfirmStartupHint()
        {
            if (!startupHintAwaitingConfirmation)
            {
                return;
            }

            startupHintAwaitingConfirmation = false;
            var autoRetractSeconds = placementSettings.NoPlaybackAutoRetractSeconds;
            SetIslandText(
                "确认收到",
                autoRetractSeconds == 0
                    ? "未播放内容时，歌词岛将保持显示"
                    : "未播放内容时，歌词岛将在 " + autoRetractSeconds + " 秒后自动收起");
            ShowIsland();
            startupHintTimer?.Stop();
            if (startupHintTimer != null)
            {
                startupHintTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, autoRetractSeconds));
                if (autoRetractSeconds > 0)
                {
                    startupHintTimer.Start();
                }
            }
        }

        private async Task RefreshAsync()
        {
            await Task.CompletedTask;
            if (refreshingState)
            {
                return;
            }

            refreshingState = true;
            try
            {
                var selected = SessionSelectionPolicy.Select(
                    mediaSessions.Sessions,
                    placementSettings.LockedSourceAppUserModelId,
                    mediaSessions.WindowsCurrentSessionId);
                currentSession = selected;

                if (selected == null)
                {
                    if (IsStartupHintActive())
                    {
                        noPlaybackSinceUtc = null;
                        ShowIsland();
                        return;
                    }

                    if (placementSettings.NoPlaybackAutoRetractSeconds == 0)
                    {
                        noPlaybackSinceUtc = null;
                        if (islandVisible)
                        {
                            ShowIsland();
                        }
                        return;
                    }

                    if (!noPlaybackSinceUtc.HasValue)
                    {
                        noPlaybackSinceUtc = DateTimeOffset.UtcNow;
                    }

                    var noPlaybackFor = DateTimeOffset.UtcNow - noPlaybackSinceUtc.Value;
                    if (noPlaybackFor < TimeSpan.FromSeconds(placementSettings.NoPlaybackAutoRetractSeconds))
                    {
                        if (islandVisible)
                        {
                            ShowIsland();
                        }
                        return;
                    }

                    currentTrack = null;
                    currentLyrics = new TimedLyrics(new LyricLine[0]);
                    lyricsSearchFinished = false;
                    pausedSinceUtc = null;
                    lyricLoadGeneration++;

                    HideIsland(true);
                    return;
                }

                noPlaybackSinceUtc = null;
                startupHintAwaitingConfirmation = false;
                startupHintTimer?.Stop();

                if (selected.PlaybackStatus != MediaPlaybackStatus.Playing)
                {
                    if (!pausedSinceUtc.HasValue) pausedSinceUtc = DateTimeOffset.UtcNow;
                }
                else
                {
                    pausedSinceUtc = null;
                }

                var pausedFor = pausedSinceUtc.HasValue
                    ? DateTimeOffset.UtcNow - pausedSinceUtc.Value
                    : TimeSpan.Zero;
                if (PlaybackVisibilityPolicy.ShouldHide(
                    true,
                    selected.Title,
                    selected.PlaybackStatus,
                    pausedFor,
                    IsStartupHintActive(),
                    false,
                    placementSettings.NoPlaybackAutoRetractSeconds == 0
                        ? TimeSpan.MaxValue
                        : TimeSpan.FromSeconds(placementSettings.NoPlaybackAutoRetractSeconds)))
                {
                    HideIsland(true);
                    return;
                }

                var timeline = timelineCoordinator.Update(
                    selected.Position,
                    selected.HasReliableTimeline,
                    selected.PlaybackStatus);
                currentEffectivePosition = timeline.Position;
                currentTimelineReliability = timeline.Reliability;
                var track = TrackIdentityCleaner.Clean(
                    new TrackIdentity(selected.Title, selected.Artist, selected.Duration, selected.Album));
                if (IsNewTrack(track))
                {
                    currentTrack = track;
                    currentLyrics = new TimedLyrics(new LyricLine[0]);
                    lyricsSearchFinished = false;
                    SetIslandText(FormatTrack(track), "正在搜索同步歌词...");
                    ShowIsland();
                    _ = LoadLyricsAsync(track, false, ++lyricLoadGeneration);
                    return;
                }

                if (currentLyrics.Lines.Count == 0)
                {
                    SetIslandText(
                        FormatTrack(track),
                        lyricsSearchFinished ? "未找到同步歌词" : "正在搜索同步歌词...");
                    ShowIsland();
                    return;
                }

                var lines = LyricsDisplaySelector.Select(
                    currentLyrics,
                    timeline.Position,
                    lyricOffset,
                    placementSettings.UseMultiLineDisplay,
                    placementSettings.ShowTranslation);
                var lineDuration = currentLyrics.GetCurrentLineDuration(
                    timeline.Position,
                    lyricOffset,
                    TimeSpan.FromSeconds(4));
                SetIslandText(
                    lines.Count > 0 ? lines[0].Text : string.Empty,
                    lines.Count > 1 ? lines[1].Text : string.Empty,
                    lineDuration);
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

        private async Task LoadLyricsAsync(TrackIdentity track, bool forceRefresh, int generation)
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

                if (generation == lyricLoadGeneration && IsSameTrack(track, currentTrack))
                {
                    currentLyrics = LyricsPackageParser.Parse(lrc);
                    lyricsSearchFinished = true;
                }
            }
            catch
            {
                if (generation == lyricLoadGeneration && IsSameTrack(track, currentTrack))
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
            ApplyInteractionState(interactionController.GetState(GetInteractionClock()));
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
            interactionController.PointerLeft(GetInteractionClock());
            ApplyInteractionState(IslandInteractionState.Collapsed);
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
            var retracting = targetTop < Top;
            var leftAnimation = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new QuarticEase
                {
                    EasingMode = retracting ? EasingMode.EaseIn : EasingMode.EaseOut
                }
            };
            var topAnimation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new QuarticEase
                {
                    EasingMode = retracting ? EasingMode.EaseIn : EasingMode.EaseOut
                }
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
            if (IsHoverTransparencySuppressed())
            {
                if (backgroundHoverOpacityMask != null || lyricsHoverOpacityMask != null)
                {
                    HideHoverTransparency();
                }

                return;
            }

            if (hoverFadeOutActive)
            {
                HideHoverTransparency();
            }

            if (backgroundHoverOpacityMask == null)
            {
                var backgroundRadius = GetHoverMaskRadius(1.0);
                var lyricsRadius = GetHoverMaskRadius(HoverMaskContentRadiusScale);
                backgroundHoverOpacityMask = CreateHoverOpacityMask(
                    backgroundRadius.Width,
                    backgroundRadius.Height,
                    placementSettings.HoverSpectrumStops,
                    0);
                lyricsHoverOpacityMask = CreateHoverOpacityMask(
                    lyricsRadius.Width,
                    lyricsRadius.Height,
                    placementSettings.HoverSpectrumStops,
                    0);
                IslandShape.OpacityMask = backgroundHoverOpacityMask;
                ModuleHost.OpacityMask = lyricsHoverOpacityMask;
            }

            UpdateHoverOpacityMaskIntensity(backgroundHoverOpacityMask, placementSettings.HoverSpectrumStops, 0, intensity);
            UpdateHoverOpacityMaskIntensity(lyricsHoverOpacityMask, placementSettings.HoverSpectrumStops, 0, intensity);

            backgroundHoverOpacityMask.Center = localPoint;
            backgroundHoverOpacityMask.GradientOrigin = localPoint;

            var lyricsPoint = IslandShell.TranslatePoint(localPoint, ModuleHost);
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
                    Math.Max(0, Math.Min(0.9, stop.PositionPercent / 100.0 * 0.9))));
            }
            mask.GradientStops.Add(new GradientStop(Colors.White, 1.0));

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
            var suppressHoverTransparency = IsHoverTransparencySuppressed();
            ModuleHost.SetPlaybackInteractionEnabled(suppressHoverTransparency);
            if (!islandVisible || !IsVisible)
            {
                HideHoverTransparency();
                return;
            }

            if (suppressHoverTransparency)
            {
                HideHoverTransparency();
                return;
            }

            UpdateInteractionStateLayout();
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

        private bool IsHoverTransparencySuppressed()
        {
            var gesture = placementSettings?.LyricOffsetHotkeys?.TemporaryInteraction ?? "Ctrl";
            return moduleDragActive || HotkeyGestureParser.IsPressed(gesture);
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
            ModuleHost.OpacityMask = null;
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
            return startupHintAwaitingConfirmation ||
                startupHintTimer != null && startupHintTimer.IsEnabled;
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
            var editedLayouts = placementSettings.IslandLayouts;
            placementSettings = settings ?? new OverlayPlacementSettings();
            placementSettings.IslandLayouts = editedLayouts ?? placementSettings.IslandLayouts;
            placementSettings.Normalize();
            interactionController.ExpandedDuration = TimeSpan.FromSeconds(placementSettings.ExpandedAutoCollapseSeconds);
            cache.SetMaxBytes(GetCacheLimitBytes(placementSettings));
            selectedLyricsSource = placementSettings.LyricsSource;
            lyricsClient = CreateLyricsClient(selectedLyricsSource);
            settingsStore.Save(placementSettings);
            RegisterGlobalHotkeys();
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

        private void BeginPotentialHorizontalDrag(MouseButtonEventArgs e)
        {
            Focus();
            horizontalDragPending = true;
            horizontalDragActive = false;
            horizontalDragStartScreenPoint = GetPointerScreenPoint(e.GetPosition(this));
            CaptureMouse();
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

        private static double GetDragDistance(Point current, Point start)
        {
            var deltaX = current.X - start.X;
            var deltaY = current.Y - start.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private void FinishHorizontalDrag()
        {
            if (!horizontalDragActive && !horizontalDragPending)
            {
                return;
            }

            horizontalDragActive = false;
            horizontalDragPending = false;
            ReleaseMouseCapture();
            settingsStore.Save(placementSettings);
            UpdateIslandShape();
            UpdateHoverProximity();
        }

        private bool ShouldForwardLeftClickThrough()
        {
            return placementSettings != null &&
                placementSettings.PassThroughOnHover &&
                islandVisible &&
                IsVisible;
        }

        private void ForwardClickThroughToUnderlyingWindow()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var originalStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, originalStyle | WS_EX_TRANSPARENT);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                }
                finally
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, originalStyle);
                }
            }), DispatcherPriority.Input);
        }

        private void UpdateIslandShape()
        {
            ApplyInteractionState(interactionController.GetState(GetInteractionClock()));
            IslandShape.Visibility = Visibility.Visible;
            IslandBackground.Opacity = 1.0;
            HideHoverTransparency();
        }

        private void ApplyInteractionState(IslandInteractionState state)
        {
            var layouts = placementSettings?.IslandLayouts ?? IslandLayoutDefaults.Create();
            layouts.Normalize();
            var animateSize = !layoutEditing &&
                layouts.Mode == IslandLayoutMode.Expandable &&
                (appliedInteractionState == IslandInteractionState.Collapsed && state == IslandInteractionState.Expanded ||
                 appliedInteractionState == IslandInteractionState.Expanded && state == IslandInteractionState.Collapsed);

            var profile = layoutEditing && layoutEditSession != null
                ? layoutEditSession.Draft
                : layouts.Mode == IslandLayoutMode.HorizontalBlocks
                ? layouts.Horizontal
                : state == IslandInteractionState.Collapsed || state == IslandInteractionState.Hidden
                    ? layouts.CompactCollapsed
                    : layouts.CompactExpanded;

            appliedInteractionState = state;
            ModuleHost.ApplyLayout(profile);
            ApplyMeasuredIslandSize(animateSize);
            if (islandVisible && !animateSize)
            {
                var point = GetVisiblePosition();
                ClearPositionAnimation();
                Left = point.Left;
                Top = point.Top;
            }
        }

        private void UpdateInteractionStateLayout()
        {
            var state = interactionController.GetState(GetInteractionClock());
            if (state != appliedInteractionState)
            {
                ApplyInteractionState(state);
            }
        }

        private TimeSpan GetInteractionClock()
        {
            return DateTimeOffset.UtcNow - interactionClockOriginUtc;
        }

        private void ApplyMeasuredIslandSize(bool animated = false)
        {
            ModuleHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var screen = ResolveScreen();
            var targetWidth = Math.Min(screen.WorkWidth, Math.Max(240, ModuleHost.DesiredSize.Width + IslandHorizontalShapePadding));
            var targetHeight = Math.Max(60, ModuleHost.DesiredSize.Height + 18);
            if (animated && IsLoaded)
            {
                AnimateIslandSize(targetWidth, targetHeight);
                return;
            }

            islandSizeAnimationVersion++;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            Width = targetWidth;
            Height = targetHeight;
            UpdateIslandSizeVisuals(targetWidth, targetHeight);
        }

        private void AnimateIslandSize(double targetWidth, double targetHeight)
        {
            var startWidth = ActualWidth > 0 ? ActualWidth : Width;
            var startHeight = ActualHeight > 0 ? ActualHeight : Height;
            var expanding = targetWidth > startWidth || targetHeight > startHeight;
            var version = ++islandSizeAnimationVersion;
            var duration = TimeSpan.FromMilliseconds(240);
            var widthAnimation = new DoubleAnimation(startWidth, targetWidth, duration)
            {
                EasingFunction = new QuarticEase
                {
                    EasingMode = expanding ? EasingMode.EaseOut : EasingMode.EaseInOut
                }
            };
            var heightAnimation = new DoubleAnimation(startHeight, targetHeight, duration)
            {
                EasingFunction = new QuarticEase
                {
                    EasingMode = expanding ? EasingMode.EaseOut : EasingMode.EaseInOut
                }
            };
            var contentAnimation = new DoubleAnimation(0.58, 1.0, TimeSpan.FromMilliseconds(210))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };

            Width = targetWidth;
            Height = targetHeight;
            heightAnimation.Completed += (sender, args) =>
            {
                if (version != islandSizeAnimationVersion)
                {
                    return;
                }

                BeginAnimation(WidthProperty, null);
                BeginAnimation(HeightProperty, null);
                ModuleHost.BeginAnimation(OpacityProperty, null);
                Width = targetWidth;
                Height = targetHeight;
                ModuleHost.Opacity = 1.0;
                UpdateIslandSizeVisuals(targetWidth, targetHeight);
                if (islandVisible)
                {
                    var point = GetVisiblePosition();
                    Left = point.Left;
                    Top = point.Top;
                }
            };

            BeginAnimation(WidthProperty, widthAnimation);
            BeginAnimation(HeightProperty, heightAnimation);
            ModuleHost.BeginAnimation(OpacityProperty, contentAnimation);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateIslandSizeVisuals(ActualWidth, ActualHeight);
            if (islandVisible && !horizontalDragActive)
            {
                var point = GetVisiblePosition();
                Left = point.Left;
                Top = point.Top;
            }
        }

        private void UpdateIslandSizeVisuals(double width, double height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            IslandShell.Width = width;
            IslandShell.Height = height;
            IslandShape.Data = Geometry.Parse(IslandGeometryBuilder.BuildTopPath(width, height));
        }

        private void SetIslandText(string primary, string secondary)
        {
            SetIslandText(primary, secondary, TimeSpan.FromSeconds(4));
        }

        private void SetIslandText(string primary, string secondary, TimeSpan lineDuration)
        {
            ModuleHost.Update(new IslandRenderState
            {
                Session = currentSession,
                PrimaryLyric = primary ?? string.Empty,
                SecondaryLyric = secondary ?? string.Empty,
                TimelineReliability = currentTimelineReliability,
                EffectivePosition = currentEffectivePosition,
                LineDuration = lineDuration
            });
        }

        private Task PreviousRequested()
        {
            return ExecuteMediaCommandAsync(mediaSessions.TrySkipPreviousAsync);
        }

        private Task PlayPauseRequested()
        {
            return currentSession != null && currentSession.PlaybackStatus == MediaPlaybackStatus.Playing
                ? ExecuteMediaCommandAsync(mediaSessions.TryPauseAsync)
                : ExecuteMediaCommandAsync(mediaSessions.TryPlayAsync);
        }

        private Task NextRequested()
        {
            return ExecuteMediaCommandAsync(mediaSessions.TrySkipNextAsync);
        }

        private async Task ExecuteMediaCommandAsync(Func<string, Task<bool>> command)
        {
            var session = currentSession;
            if (session == null) return;

            try
            {
                if (await command(session.SessionId))
                {
                    await mediaSessions.RefreshAsync();
                }
            }
            catch
            {
                // SMTC commands can fail when the player closes or changes sessions between click and dispatch.
            }
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
            _ = LoadLyricsAsync(currentTrack, forceRefresh, ++lyricLoadGeneration);
        }

        private void OpenPlacementSettingsWindow()
        {
            if (settingsWindow != null)
            {
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new PlacementSettingsWindow(
                screenCatalog.GetScreens(),
                placementSettings,
                ApplyPlacementSettings,
                mediaSessions.Sessions,
                BeginLayoutEditing,
                SaveLayoutEditing,
                CancelLayoutEditing,
                UpdateLyricsWidth,
                UpdateDividerSettings,
                RemoveDividers,
                RemoveModuleFromToolbox,
                SetModuleDragActive)
            {
                Owner = this
            };
            settingsWindow.Closed += (sender, args) => settingsWindow = null;
            settingsWindow.Show();
        }

        private void BeginLayoutEditing(IslandLayoutMode mode, bool resetToDefault)
        {
            placementSettings.IslandLayouts = placementSettings.IslandLayouts ?? IslandLayoutDefaults.Create();
            placementSettings.IslandLayouts.Mode = mode;
            layoutEditingMode = mode;
            var profile = resetToDefault
                ? GetDefaultLayoutProfile(mode)
                : GetEditableLayoutProfile(mode);
            layoutEditSession = new LayoutEditSession(profile);
            layoutEditing = true;
            ModuleHost.LayoutEditingEnabled = true;
            ModuleHost.IsHitTestVisible = true;
            interactionController.SetEditing(true);
            ApplyInteractionState(IslandInteractionState.Editing);
            ShowIsland();
        }

        private void UpdateLyricsWidth(IslandLayoutMode mode, double width)
        {
            if (!layoutEditing || layoutEditSession == null)
            {
                return;
            }

            var normalized = IslandModuleInstance.NormalizeLyricsWidth(width);
            foreach (var module in layoutEditSession.Draft.Modules.Where(module => module.Type == IslandModuleType.Lyrics))
            {
                module.LyricsWidth = normalized;
            }

            ApplyInteractionState(IslandInteractionState.Editing);
        }

        private void UpdateDividerSettings(IslandLayoutMode mode, double opacity, double spacing)
        {
            if (!layoutEditing || layoutEditSession == null || mode != layoutEditingMode)
            {
                return;
            }

            var normalizedOpacity = Math.Max(0, Math.Min(1, opacity));
            var normalizedSpacing = Math.Max(0, Math.Min(64, spacing));
            foreach (var divider in layoutEditSession.Draft.Modules.Where(module => module.Type == IslandModuleType.Divider))
            {
                divider.DividerOpacity = normalizedOpacity;
                divider.MarginBefore = normalizedSpacing;
                divider.MarginAfter = normalizedSpacing;
            }

            ApplyInteractionState(IslandInteractionState.Editing);
        }

        private void RemoveDividers(IslandLayoutMode mode)
        {
            if (!layoutEditing || layoutEditSession == null || mode != layoutEditingMode)
            {
                return;
            }

            layoutEditSession.Draft.Modules.RemoveAll(module => module.Type == IslandModuleType.Divider);
            ApplyInteractionState(IslandInteractionState.Editing);
        }

        private void RemoveModuleFromToolbox(string instanceId)
        {
            if (!layoutEditing || layoutEditSession == null || string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            layoutEditSession.Remove(instanceId);
            ModuleHost.ClearInsertionPreview();
            ApplyInteractionState(IslandInteractionState.Editing);
        }

        private void SetModuleDragActive(bool value)
        {
            moduleDragActive = value;
            if (value)
            {
                HideHoverTransparency();
            }
            else
            {
                UpdateHoverProximity();
            }
        }

        private void SaveLayoutEditing()
        {
            if (!layoutEditing || layoutEditSession == null)
            {
                return;
            }

            var committed = layoutEditSession.Commit();
            SetEditableLayoutProfile(layoutEditingMode, committed);
            layoutEditSession = null;
            layoutEditing = false;
            ModuleHost.LayoutEditingEnabled = false;
            interactionController.SetEditing(false);
            placementSettings.Normalize();
            settingsStore.Save(placementSettings);
            UpdateIslandShape();
        }

        private void CancelLayoutEditing()
        {
            if (!layoutEditing)
            {
                return;
            }

            layoutEditSession?.Cancel();
            layoutEditSession = null;
            layoutEditing = false;
            ModuleHost.LayoutEditingEnabled = false;
            interactionController.SetEditing(false);
            UpdateIslandShape();
        }

        private IslandLayoutProfile GetEditableLayoutProfile(IslandLayoutMode mode)
        {
            placementSettings.IslandLayouts.Normalize();
            return mode == IslandLayoutMode.HorizontalBlocks
                ? placementSettings.IslandLayouts.Horizontal
                : placementSettings.IslandLayouts.CompactExpanded;
        }

        private static IslandLayoutProfile GetDefaultLayoutProfile(IslandLayoutMode mode)
        {
            return mode == IslandLayoutMode.HorizontalBlocks
                ? IslandLayoutDefaults.CreateHorizontal()
                : IslandLayoutDefaults.CreateExpanded();
        }

        private void SetEditableLayoutProfile(IslandLayoutMode mode, IslandLayoutProfile profile)
        {
            placementSettings.IslandLayouts = placementSettings.IslandLayouts ?? IslandLayoutDefaults.Create();
            if (mode == IslandLayoutMode.HorizontalBlocks)
            {
                placementSettings.IslandLayouts.Horizontal = profile;
            }
            else
            {
                placementSettings.IslandLayouts.CompactExpanded = profile;
            }
        }

        private void ModuleHost_DragOver(object sender, DragEventArgs e)
        {
            SetModuleDragActive(true);
            if (!layoutEditing || layoutEditSession == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var index = FindModuleInsertionIndex(e.GetPosition(ModuleHost).X);
            var payload = e.Data.GetData(typeof(IslandLayoutDragPayload)) as IslandLayoutDragPayload;
            var acceptedEffect = payload?.NewType.HasValue == true
                ? DragDropEffects.Copy
                : DragDropEffects.Move;
            e.Effects = index >= 0 && payload != null ? acceptedEffect : DragDropEffects.None;
            if (e.Effects != DragDropEffects.None)
            {
                ModuleHost.ShowInsertionPreview(index, ModuleHost.GetDragPreviewWidth(payload));
            }
            else
            {
                ModuleHost.ClearInsertionPreview();
            }
            e.Handled = true;
        }

        private void ModuleHost_Drop(object sender, DragEventArgs e)
        {
            ModuleHost.ClearInsertionPreview();
            SetModuleDragActive(false);
            if (!layoutEditing || layoutEditSession == null)
            {
                return;
            }

            var index = FindModuleInsertionIndex(e.GetPosition(ModuleHost).X);
            var payload = e.Data.GetData(typeof(IslandLayoutDragPayload)) as IslandLayoutDragPayload;
            if (index >= 0 && payload != null)
            {
                if (payload.NewType.HasValue)
                {
                    layoutEditSession.Add(payload.NewType.Value, index);
                }
                else if (!string.IsNullOrWhiteSpace(payload.ExistingInstanceId))
                {
                    layoutEditSession.Move(payload.ExistingInstanceId, index);
                }

                ApplyInteractionState(IslandInteractionState.Editing);
            }

            e.Handled = true;
        }

        private void ModuleHost_DragLeave(object sender, DragEventArgs e)
        {
            ModuleHost.ClearInsertionPreview();
            SetModuleDragActive(false);
            e.Handled = true;
        }

        private int FindModuleInsertionIndex(double pointerX)
        {
            var targets = ModuleHost.BuildInsertionTargets();
            var snapped = LayoutEditSession.FindInsertionIndex(pointerX, targets, 18);
            if (snapped >= 0)
            {
                return snapped;
            }

            var nearest = targets.OrderBy(target => Math.Abs(target.X - pointerX)).FirstOrDefault();
            return nearest?.Index ?? 0;
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
            var clone = placementSettings.DeepClone();
            clone.ScreenName = placement.ScreenName;
            clone.Edge = placement.Edge;
            clone.OffsetRatio = placement.OffsetRatio;
            return clone;
        }

        private static byte GetOpacityAlpha(int transparencyPercent)
        {
            var normalized = Math.Max(0, Math.Min(100, transparencyPercent));
            return (byte)Math.Round(255 * (100 - normalized) / 100.0);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (settingsWindow != null)
            {
                return;
            }

            if (layoutEditing && IsModuleHostMouseSource(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (IsInteractiveMouseSource(e.OriginalSource as DependencyObject))
            {
                return;
            }

            BeginPotentialHorizontalDrag(e);
            e.Handled = true;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (islandVisible)
            {
                ShowHoverTransparency(e.GetPosition(IslandShell));
            }

            if (horizontalDragPending && !horizontalDragActive)
            {
                if (e.LeftButton != MouseButtonState.Pressed)
                {
                    FinishHorizontalDrag();
                    return;
                }

                var pointer = GetPointerScreenPoint(e.GetPosition(this));
                if (GetDragDistance(pointer, horizontalDragStartScreenPoint) < DragStartThreshold)
                {
                    e.Handled = true;
                    return;
                }

                ClearPositionAnimation();
                horizontalDragActive = true;
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
            if (IsInteractiveMouseSource(e.OriginalSource as DependencyObject))
            {
                FinishHorizontalDrag();
                return;
            }

            var wasStartupHintAwaitingConfirmation = startupHintAwaitingConfirmation;
            var shouldForwardClick = horizontalDragPending && !horizontalDragActive && ShouldForwardLeftClickThrough();
            var shouldConfirmStartupHint = horizontalDragPending && !horizontalDragActive;
            var shouldToggleExpandableLayout = shouldConfirmStartupHint &&
                !wasStartupHintAwaitingConfirmation &&
                placementSettings?.IslandLayouts?.Mode == IslandLayoutMode.Expandable;
            FinishHorizontalDrag();
            if (shouldConfirmStartupHint)
            {
                ConfirmStartupHint();
            }

            if (shouldToggleExpandableLayout)
            {
                interactionController.ToggleExpanded(GetInteractionClock());
                UpdateInteractionStateLayout();
                shouldForwardClick = false;
            }

            if (shouldForwardClick)
            {
                ForwardClickThroughToUnderlyingWindow();
            }

            e.Handled = true;
        }

        private static bool IsInteractiveMouseSource(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private bool IsModuleHostMouseSource(DependencyObject source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, ModuleHost))
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            OpenPlacementSettingsWindow();
            e.Handled = true;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            interactionController.PointerEntered(GetInteractionClock());
            UpdateInteractionStateLayout();
            if (islandVisible)
            {
                ShowHoverTransparency(e.GetPosition(IslandShell));
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            interactionController.PointerLeft(GetInteractionClock());
            UpdateInteractionStateLayout();
            UpdateHoverProximity();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsHoverTransparencySuppressed())
            {
                ModuleHost.SetPlaybackInteractionEnabled(true);
                HideHoverTransparency();
                e.Handled = true;
                return;
            }

            if (startupHintAwaitingConfirmation)
            {
                ConfirmStartupHint();
                e.Handled = true;
                return;
            }

        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            ModuleHost.SetPlaybackInteractionEnabled(IsHoverTransparencySuppressed());
            UpdateHoverProximity();
        }
    }
}
