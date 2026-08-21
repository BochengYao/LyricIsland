using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;
using LyricHover.App.LayoutEditing;
using LyricHover.App.Modules;
using LyricHover.App.LyricDock;
using LyricHover.Core;
using LyricHover.App.Media;
using LyricHover.Core.Layout;
using LyricHover.Core.Media;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LyricHover.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer hoverProximityTimer;
        private readonly IMediaSessionService mediaSessions;
        private readonly TimelineCoordinator timelineCoordinator;
        private readonly PlaybackIntentCoordinator playbackIntents = new PlaybackIntentCoordinator();
        private readonly SemaphoreSlim playbackCommandGate = new SemaphoreSlim(1, 1);
        private readonly IslandInteractionController interactionController = new IslandInteractionController();
        private readonly DateTimeOffset interactionClockOriginUtc = DateTimeOffset.UtcNow;
        private MediaSessionSnapshot currentSession;
        private string timelineSessionId = string.Empty;
        private GlobalHotkeyService hotkeyService;
        private DateTimeOffset? pausedSinceUtc;
        private DateTimeOffset? noPlaybackSinceUtc;
        private int lyricLoadGeneration;
        private readonly LyricsCache cache;
        private readonly OverlaySettingsStore settingsStore;
        private readonly WindowsLyricDockEnvironment lyricDockEnvironment;
        private readonly LyricDockController LyricDockController;
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
        private string currentPrimaryText = string.Empty;
        private string currentSecondaryText = string.Empty;
        private TimeSpan currentLineDuration = TimeSpan.FromSeconds(4);
        private DispatcherTimer startupHintTimer;
        private Forms.NotifyIcon trayIcon;
        private int positionAnimationVersion;
        private readonly AnimationTargetTracker positionAnimationTargets = new AnimationTargetTracker();
        private EventHandler positionAnimationFrameHandler;
        private bool horizontalDragActive;
        private bool horizontalDragPending;
        private Point horizontalDragStartScreenPoint;
        private RadialGradientBrush backgroundHoverOpacityMask;
        private RadialGradientBrush lyricsHoverOpacityMask;
        private HoverSpectrumStop[] activeHoverSpectrumStops;
        private readonly HoverSampleTracker hoverSamples = new HoverSampleTracker();
        private int hoverFadeAnimationVersion;
        private bool hoverFadeOutActive;
        private IslandInteractionState appliedInteractionState = IslandInteractionState.Collapsed;
        private PlacementSettingsWindow settingsWindow;
        private int islandSizeAnimationVersion;
        private EventHandler islandSizeAnimationFrameHandler;
        private bool islandSizeAnimationActive;
        private double islandVisualWidth;
        private double islandVisualHeight;
        private bool moduleDragActive;
        private bool settingsWindowHoverSuppressed;
        private readonly TutorialFlowController tutorialFlow = new TutorialFlowController();
        private bool shouldStartFirstRunTutorial;
        private CancellationTokenSource tutorialCancellation;
        private TutorialMaskWindow tutorialMaskWindow;
        private TutorialActionWindow tutorialExitWindow;
        private TutorialActionWindow tutorialNextWindow;
        private IslandLayoutProfile tutorialLayoutOverride;
        private string tutorialPrimaryText = string.Empty;
        private string tutorialSecondaryText = string.Empty;
        private string tutorialAccentText = string.Empty;
        private bool tutorialHoverSuppressed;
        private TaskCompletionSource<bool> tutorialHoverEnteredCompletion;
        private readonly string tutorialLyricsModuleId = "tutorial-lyrics";
        private readonly string tutorialDividerModuleId = "tutorial-divider";
        private readonly string tutorialControlsModuleId = "tutorial-controls";
        private readonly SemaphoreSlim tutorialStartGate = new SemaphoreSlim(1, 1);
        private Task tutorialStopTask = Task.CompletedTask;
        private bool tutorialStopping;
        private double moduleDragPreviewWidth;
        private readonly LayoutDropCommitGuard committedModuleDrops = new LayoutDropCommitGuard();
        private bool moduleDragPreviewQueued;
        private bool moduleContentSizeUpdateQueued;
        private IslandLayoutDragPayload queuedModuleDragPayload;
        private double queuedModuleDragPointerX;
        private int trackChangeGeneration;
        private bool runtimeStopped;
        private const double DragStartThreshold = 4.0;
        private const double IslandHorizontalShapePadding = 144;
        private const double HoverMaskContentRadiusScale = 1.0;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int VK_RBUTTON = 0x02;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int TutorialEscapeHotkeyId = 99;
        private const uint VkEscape = 0x1B;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        public MainWindow()
        {
            InitializeComponent();
            ModuleHost.PreviousRequested += async (sender, args) => await PreviousRequested();
            ModuleHost.PlayPauseRequested += async (sender, args) => await PlayPauseRequested();
            ModuleHost.NextRequested += async (sender, args) => await NextRequested();
            ModuleHost.ContentSizeChanged += (sender, args) => QueueModuleContentSizeUpdate();
            ModuleHost.ModuleDragStarted += (sender, args) => SetModuleDragActive(true);
            ModuleHost.ModuleDragCompleted += (sender, args) =>
            {
                if (args.DroppedOutside && committedModuleDrops.TryCommit(args.OperationId))
                {
                    RemoveModuleAfterOutsideDrop(args.InstanceId);
                }
                ModuleHost.ClearInsertionPreview();
                ClearModuleDragSizePreview(true);
                SetModuleDragActive(false);
            };

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var productDataRoot = ProductDataDirectory.Prepare(appData);
            var cacheRoot = System.IO.Path.Combine(productDataRoot, "lyrics");
            var settingsPath = System.IO.Path.Combine(productDataRoot, "settings.json");
            var settingsFileExisted = System.IO.File.Exists(settingsPath);

            mediaSessions = new SmTcMediaSessionService();
            timelineCoordinator = new TimelineCoordinator(new StopwatchClock());
            mediaSessions.SessionsChanged += (sender, args) =>
                Dispatcher.BeginInvoke(new Action(async () => await RefreshAsync()));
            settingsStore = new OverlaySettingsStore(settingsPath);
            placementSettings = settingsStore.Load();
            lyricDockEnvironment = new WindowsLyricDockEnvironment();
            var taskbarLease = new WidgetVisibilityLease(
                lyricDockEnvironment,
                System.IO.Path.Combine(productDataRoot, "taskbar-widgets-lease.txt"));
            LyricDockController = new LyricDockController(
                lyricDockEnvironment,
                taskbarLease,
                new LyricDockWindow());
            LyricDockController.SettingsRequested += (sender, args) =>
                Dispatcher.BeginInvoke(new Action(() => OpenPlacementSettingsWindow(true)));
            LyricDockController.FeatureDisabled += (sender, reason) =>
                Dispatcher.BeginInvoke(new Action(() => DisableTaskbarLyrics(reason)));
            LyricDockController.WidgetsHidingDegraded += (sender, args) =>
                Dispatcher.BeginInvoke(new Action(() => ShowWidgetsHidingDegradedNotice()));
            LyricDockController.Start(placementSettings.LyricDockEnabled, placementSettings.ScreenName, placementSettings.LyricDockAlignment);
            shouldStartFirstRunTutorial = !settingsFileExisted;
            if (settingsFileExisted && !placementSettings.HasSeenTutorial)
            {
                // Existing installations predate the tutorial flag and must not be mistaken for a first launch.
                placementSettings.HasSeenTutorial = true;
                settingsStore.Save(placementSettings);
            }
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

                if (shouldStartFirstRunTutorial)
                {
                    shouldStartFirstRunTutorial = false;
                    placementSettings.HasSeenTutorial = true;
                    settingsStore.Save(placementSettings);
                    await StartTutorialAsync();
                }
            };
            SourceInitialized += (sender, args) =>
            {
                InstallWindowMessageHook();
                RegisterGlobalHotkeys();
            };
            Closed += (sender, args) =>
            {
                StopRuntimeActivity();
                tutorialCancellation?.Cancel();
                CloseTutorialWindows();
                hotkeyService?.Dispose();
                LyricDockController?.Dispose();
                lyricDockEnvironment?.Dispose();
                mediaSessions.Dispose();
                DisposeTrayIcon();
            };
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon
            {
                Text = "LyricHover | LYRIC HOVER",
                Icon = LoadTrayIcon(),
                ContextMenuStrip = new Forms.ContextMenuStrip()
            };
            trayIcon.ContextMenuStrip.Items.Add("偏好设置", null, (sender, args) => Dispatcher.BeginInvoke(new Action(() => OpenPlacementSettingsWindow())));
            trayIcon.ContextMenuStrip.Items.Add("退出", null, (sender, args) => Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown())));
            trayIcon.DoubleClick += (sender, args) => Dispatcher.BeginInvoke(new Action(() => OpenPlacementSettingsWindow()));
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
            if (tutorialFlow.IsActive)
            {
                RegisterTutorialEscapeHotkey();
            }
        }

        private void RegisterTutorialEscapeHotkey()
        {
            hotkeyService?.Unregister(TutorialEscapeHotkeyId);
            hotkeyService?.Register(TutorialEscapeHotkeyId, 0, VkEscape, () => TryExitTutorial());
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

            startupHintTimer?.Stop();
            var autoRetractSeconds = placementSettings.NoPlaybackAutoRetractSeconds;
            SetIslandText(
                "暂无播放内容",
                autoRetractSeconds == 0
                    ? "未播放内容时，LyricHover将保持显示"
                    : "LyricHover将在 " + autoRetractSeconds + " 秒后自动收起");
            ShowIsland();
            if (startupHintTimer == null)
            {
                startupHintTimer = new DispatcherTimer();
                startupHintTimer.Tick += (sender, args) =>
                {
                    startupHintTimer.Stop();
                    if (currentTrack == null)
                    {
                        noPlaybackSinceUtc = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(placementSettings.NoPlaybackAutoRetractSeconds);
                        HideIsland(true);
                    }
                };
            }
            startupHintTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, autoRetractSeconds));
            if (autoRetractSeconds > 0)
            {
                startupHintTimer.Start();
            }
        }

        private async Task RefreshAsync()
        {
            await Task.CompletedTask;
            if (runtimeStopped || refreshingState)
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
                    playbackIntents.CancelUnless(string.Empty);
                    timelineSessionId = string.Empty;
                    timelineCoordinator.Reset();
                    if (settingsWindow != null)
                    {
                        noPlaybackSinceUtc = null;
                        ShowIsland();
                        return;
                    }

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

                playbackIntents.CancelUnless(selected.SessionId);
                playbackIntents.Confirm(selected.SessionId, selected.PlaybackStatus);

                noPlaybackSinceUtc = null;
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
                    IsStartupHintActive() || settingsWindow != null,
                    false,
                    placementSettings.NoPlaybackAutoRetractSeconds == 0
                        ? TimeSpan.MaxValue
                        : TimeSpan.FromSeconds(placementSettings.NoPlaybackAutoRetractSeconds)))
                {
                    HideIsland(true);
                    return;
                }

                var track = TrackIdentityCleaner.Clean(
                    new TrackIdentity(selected.Title, selected.Artist, selected.Duration, selected.Album));
                var isNewTrack = IsNewTrack(track);
                if (!string.Equals(timelineSessionId, selected.SessionId, StringComparison.OrdinalIgnoreCase) || isNewTrack)
                {
                    timelineSessionId = selected.SessionId ?? string.Empty;
                    timelineCoordinator.Reset();
                }

                var timeline = timelineCoordinator.Update(
                    selected.Position,
                    selected.HasReliableTimeline,
                    selected.PlaybackStatus);
                currentEffectivePosition = timeline.Position;
                currentTimelineReliability = timeline.Reliability;
                if (isNewTrack)
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
                var cachedLyrics = cacheHit ? LyricsPackageParser.Parse(lrc) : null;
                var needsTranslation = placementSettings.ShowTranslation &&
                    !LyricsPackageParser.HasTranslation(lrc) &&
                    !LyricsDisplaySelector.ShouldIgnoreTranslation(cachedLyrics);
                if (forceRefresh || !cacheHit || needsTranslation)
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
            if (islandVisible)
            {
                UpdateInteractionStateLayout();
                hoverProximityTimer.Start();
                UpdateHoverProximity();
                return;
            }

            ApplyInteractionState(interactionController.GetState(GetInteractionClock()));
            var point = GetVisiblePosition();
            AnimateTo(point.Left, point.Top);
            islandVisible = true;
            hoverProximityTimer.Start();
            UpdateHoverProximity();
        }

        private void HideIsland(bool animated)
        {
            if (settingsWindow != null || tutorialFlow.IsActive)
            {
                ShowIsland();
                return;
            }

            if (animated && !islandVisible)
            {
                return;
            }

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
            if (!positionAnimationTargets.TrySet(targetLeft, targetTop))
            {
                return;
            }

            var version = ++positionAnimationVersion;
            var startLeft = Left;
            var startTop = Top;
            var retracting = targetTop < startTop;
            var duration = TimeSpan.FromMilliseconds(retracting ? 300 : 360);
            var started = DateTime.UtcNow;

            StopPositionAnimationFrames();
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            positionAnimationFrameHandler = (sender, args) =>
            {
                if (version != positionAnimationVersion)
                {
                    StopPositionAnimationFrames();
                    return;
                }

                var progress = Math.Max(
                    0,
                    Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / duration.TotalMilliseconds));
                var eased = retracting
                    ? Math.Pow(progress, 3)
                    : 1 - Math.Pow(1 - progress, 4);
                Left = startLeft + (targetLeft - startLeft) * eased;
                Top = startTop + (targetTop - startTop) * eased;

                if (progress < 1)
                {
                    return;
                }

                StopPositionAnimationFrames();
                positionAnimationTargets.Clear();
                Left = targetLeft;
                Top = targetTop;
            };
            CompositionTarget.Rendering += positionAnimationFrameHandler;
        }

        private void ClearPositionAnimation()
        {
            positionAnimationVersion++;
            positionAnimationTargets.Clear();
            StopPositionAnimationFrames();
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
        }

        private void StopPositionAnimationFrames()
        {
            if (positionAnimationFrameHandler == null)
            {
                return;
            }

            CompositionTarget.Rendering -= positionAnimationFrameHandler;
            positionAnimationFrameHandler = null;
        }

        private void StopRuntimeActivity()
        {
            runtimeStopped = true;
            timer.Stop();
            hoverProximityTimer.Stop();
            startupHintTimer?.Stop();
            islandSizeAnimationVersion++;
            StopIslandSizeAnimationFrames();
            ClearPositionAnimation();
            HideHoverTransparency();
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
                activeHoverSpectrumStops = (placementSettings.HoverSpectrumStops ?? OverlayPlacementSettings.CreateDefaultHoverSpectrumStops())
                    .OrderBy(item => item.PositionPercent)
                    .ToArray();
                backgroundHoverOpacityMask = CreateHoverOpacityMask(
                    backgroundRadius.Width,
                    backgroundRadius.Height,
                    activeHoverSpectrumStops,
                    0);
                lyricsHoverOpacityMask = CreateHoverOpacityMask(
                    lyricsRadius.Width,
                    lyricsRadius.Height,
                    activeHoverSpectrumStops,
                    0);
                IslandShape.OpacityMask = backgroundHoverOpacityMask;
                ModuleHost.OpacityMask = lyricsHoverOpacityMask;
            }

            if (!hoverSamples.TryUpdate(localPoint.X, localPoint.Y, intensity))
            {
                return;
            }

            UpdateHoverOpacityMaskIntensity(backgroundHoverOpacityMask, activeHoverSpectrumStops, 0, intensity);
            UpdateHoverOpacityMaskIntensity(lyricsHoverOpacityMask, activeHoverSpectrumStops, 0, intensity);

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

        private static RadialGradientBrush CreateHoverOpacityMask(double radiusX, double radiusY, System.Collections.Generic.IReadOnlyList<HoverSpectrumStop> spectrumStops, int extraTransparencyPercent)
        {
            var mask = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                RadiusX = radiusX,
                RadiusY = radiusY
            };
            foreach (var stop in spectrumStops)
            {
                var transparency = Math.Max(0, Math.Min(100, stop.TransparencyPercent + extraTransparencyPercent));
                mask.GradientStops.Add(new GradientStop(
                    Color.FromArgb(GetOpacityAlpha(transparency), 255, 255, 255),
                    Math.Max(0, Math.Min(0.9, stop.PositionPercent / 100.0 * 0.9))));
            }
            mask.GradientStops.Add(new GradientStop(Colors.White, 1.0));

            return mask;
        }

        private static void UpdateHoverOpacityMaskIntensity(RadialGradientBrush mask, System.Collections.Generic.IReadOnlyList<HoverSpectrumStop> spectrumStops, int extraTransparencyPercent, double intensity)
        {
            var normalizedIntensity = Math.Max(0, Math.Min(1, intensity));
            for (var index = 0; index < mask.GradientStops.Count && index < spectrumStops.Count; index++)
            {
                var transparency = Math.Max(0, Math.Min(100, spectrumStops[index].TransparencyPercent + extraTransparencyPercent));
                var fadedTransparency = (int)Math.Round(transparency * normalizedIntensity);
                mask.GradientStops[index].Color = Color.FromArgb(GetOpacityAlpha(fadedTransparency), 255, 255, 255);
            }
        }

        private void UpdateHoverProximity()
        {
            var temporaryInteractionHeld = IsTemporaryInteractionHeld();
            var temporaryExpansionChanged = interactionController.SetTemporaryExpanded(
                placementSettings?.IslandLayouts?.Mode == IslandLayoutMode.Expandable &&
                temporaryInteractionHeld,
                GetInteractionClock());
            if (temporaryExpansionChanged)
            {
                RestartNoPlaybackAutoRetractCountdown();
            }
            UpdateInteractionStateLayout();
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
            return tutorialHoverSuppressed || settingsWindowHoverSuppressed || moduleDragActive || IsTemporaryInteractionHeld();
        }

        private string GetTemporaryInteractionGesture()
        {
            var gesture = placementSettings?.LyricOffsetHotkeys?.TemporaryInteraction;
            return string.IsNullOrWhiteSpace(gesture) ? "Ctrl" : gesture.Trim();
        }

        private bool IsTemporaryInteractionHeld()
        {
            return HotkeyGestureParser.IsPressed(GetTemporaryInteractionGesture());
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
            activeHoverSpectrumStops = null;
            hoverSamples.Clear();
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
            var editedLayouts = placementSettings.IslandLayouts;
            placementSettings = settings ?? new OverlayPlacementSettings();
            placementSettings.IslandLayouts = editedLayouts ?? placementSettings.IslandLayouts;
            placementSettings.Normalize();
            interactionController.ExpandedDuration = TimeSpan.FromSeconds(placementSettings.ExpandedAutoCollapseSeconds);
            cache.SetMaxBytes(GetCacheLimitBytes(placementSettings));
            selectedLyricsSource = placementSettings.LyricsSource;
            lyricsClient = CreateLyricsClient(selectedLyricsSource);
            var taskbarWasRequested = placementSettings.LyricDockEnabled;
            if (!LyricDockController.Configure(placementSettings.LyricDockEnabled, placementSettings.ScreenName, placementSettings.LyricDockAlignment) && taskbarWasRequested)
            {
                placementSettings.LyricDockEnabled = false;
                settings.LyricDockEnabled = false;
                ShowTaskbarLyricsFailure(LyricDockController.LastFailureReason);
            }
            settingsStore.Save(placementSettings);
            RegisterGlobalHotkeys();
            UpdateIslandShape();
            if (currentTrack != null && previousSource != placementSettings.LyricsSource)
            {
                RefreshCurrentTrackLyrics(true);
                return;
            }

            if (currentTrack != null &&
                !previousShowTranslation &&
                placementSettings.ShowTranslation &&
                !LyricsDisplaySelector.ShouldIgnoreTranslation(currentLyrics))
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

        private void ApplyInteractionState(IslandInteractionState state, bool forceSizeAnimation = false)
        {
            var layouts = placementSettings?.IslandLayouts ?? IslandLayoutDefaults.Create();
            layouts.Normalize();
            var animateSize = forceSizeAnimation ||
                (!layoutEditing &&
                 layouts.Mode == IslandLayoutMode.Expandable &&
                 (appliedInteractionState == IslandInteractionState.Collapsed && state == IslandInteractionState.Expanded ||
                  appliedInteractionState == IslandInteractionState.Expanded && state == IslandInteractionState.Collapsed));

            var tutorialOwnsLayout = tutorialFlow.IsActive &&
                tutorialLayoutOverride != null &&
                (!layoutEditing || tutorialFlow.Step == TutorialStep.ShowingLayouts);
            var profile = tutorialOwnsLayout
                ? tutorialLayoutOverride
                : layoutEditing && layoutEditSession != null
                ? layoutEditSession.Draft
                : layouts.Mode == IslandLayoutMode.HorizontalBlocks
                ? layouts.Horizontal
                : state == IslandInteractionState.Collapsed || state == IslandInteractionState.Hidden
                    ? layouts.CompactCollapsed
                    : layouts.CompactExpanded;

            appliedInteractionState = state;
            ModuleHost.ApplyLayout(EnsureTutorialLyricsVisible(profile));
            var sizeAnimated = ApplyMeasuredIslandSize(animateSize);
            if (islandVisible && !sizeAnimated && positionAnimationFrameHandler == null)
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

        private bool ApplyMeasuredIslandSize(bool animated = false)
        {
            var contentSize = ModuleHost.MeasureContentSize();
            var screen = ResolveScreen();
            var targetWidth = Math.Min(screen.WorkWidth, Math.Max(240, contentSize.Width + IslandHorizontalShapePadding));
            var targetHeight = Math.Max(60, contentSize.Height + 18);
            var currentWidth = islandVisualWidth > 0 ? islandVisualWidth : ActualWidth > 0 ? ActualWidth : Width;
            var currentHeight = islandVisualHeight > 0 ? islandVisualHeight : ActualHeight > 0 ? ActualHeight : Height;
            var sizeChanged = Math.Abs(targetWidth - currentWidth) >= 0.5 ||
                Math.Abs(targetHeight - currentHeight) >= 0.5;
            if (IsLoaded && sizeChanged)
            {
                AnimateIslandSize(targetWidth, targetHeight, animated ? 360 : 320);
                return true;
            }

            islandSizeAnimationVersion++;
            StopIslandSizeAnimationFrames();
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            Width = targetWidth;
            Height = targetHeight;
            IslandShellTranslate.X = 0;
            UpdateIslandSizeVisuals(targetWidth, targetHeight);
            return false;
        }

        private void QueueModuleContentSizeUpdate()
        {
            if (moduleContentSizeUpdateQueued)
            {
                return;
            }

            moduleContentSizeUpdateQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                moduleContentSizeUpdateQueued = false;
                if (IsLoaded && !runtimeStopped)
                {
                    ApplyMeasuredIslandSize(true);
                }
            }), DispatcherPriority.Loaded);
        }

        private void PreviewModuleDragSize(double previewWidth)
        {
            previewWidth = Math.Max(0, previewWidth);
            if (Math.Abs(moduleDragPreviewWidth - previewWidth) < 0.5)
            {
                return;
            }

            moduleDragPreviewWidth = previewWidth;
            var contentSize = ModuleHost.MeasureContentSize();
            var screen = ResolveScreen();
            var targetWidth = Math.Min(
                screen.WorkWidth,
                Math.Max(240, contentSize.Width + IslandHorizontalShapePadding + 10));
            var targetHeight = Math.Max(60, contentSize.Height + 18);
            AnimateIslandSize(targetWidth, targetHeight, 180);
        }

        private void ClearModuleDragSizePreview(bool animated)
        {
            if (moduleDragPreviewWidth <= 0)
            {
                return;
            }

            moduleDragPreviewWidth = 0;
            ApplyMeasuredIslandSize(animated);
        }

        private void AnimateIslandSize(double targetWidth, double targetHeight, int durationMilliseconds = 360)
        {
            var startWidth = islandVisualWidth > 0 ? islandVisualWidth : ActualWidth > 0 ? ActualWidth : Width;
            var startHeight = islandVisualHeight > 0 ? islandVisualHeight : ActualHeight > 0 ? ActualHeight : Height;
            if (Math.Abs(targetWidth - startWidth) < 0.5 && Math.Abs(targetHeight - startHeight) < 0.5)
            {
                islandSizeAnimationVersion++;
                StopIslandSizeAnimationFrames();
                Width = targetWidth;
                Height = targetHeight;
                IslandShellTranslate.X = 0;
                UpdateIslandSizeVisuals(targetWidth, targetHeight);
                return;
            }
            var expanding = targetWidth > startWidth || targetHeight > startHeight;
            var version = ++islandSizeAnimationVersion;
            var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
            var started = DateTime.UtcNow;
            var outerWidth = Math.Max(startWidth, targetWidth);
            var outerHeight = Math.Max(startHeight, targetHeight);
            var screen = ResolveScreen();
            var placement = placementSettings.ToPlacement();
            var outerPosition = OverlayPositioner.GetVisiblePosition(
                placement, screen, new OverlaySize(outerWidth, outerHeight));

            StopIslandSizeAnimationFrames();
            islandSizeAnimationActive = true;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            ModuleHost.BeginAnimation(OpacityProperty, null);
            ModuleHost.Opacity = 1.0;
            Width = Math.Max(startWidth, targetWidth);
            Height = Math.Max(startHeight, targetHeight);
            Left = outerPosition.Left;
            Top = outerPosition.Top;
            var startPosition = OverlayPositioner.GetVisiblePosition(
                placement, screen, new OverlaySize(startWidth, startHeight));
            IslandShellTranslate.X = startPosition.Left - outerPosition.Left;
            UpdateIslandSizeVisuals(startWidth, startHeight);

            islandSizeAnimationFrameHandler = (sender, args) =>
            {
                if (version != islandSizeAnimationVersion)
                {
                    StopIslandSizeAnimationFrames();
                    return;
                }

                var progress = Math.Max(0, Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / duration.TotalMilliseconds));
                var eased = expanding
                    ? 1 - Math.Pow(1 - progress, 4)
                    : progress < 0.5
                        ? 8 * Math.Pow(progress, 4)
                        : 1 - Math.Pow(-2 * progress + 2, 4) / 2;
                var width = startWidth + (targetWidth - startWidth) * eased;
                var height = startHeight + (targetHeight - startHeight) * eased;
                var visualPosition = OverlayPositioner.GetVisiblePosition(
                    placement, screen, new OverlaySize(width, height));
                IslandShellTranslate.X = visualPosition.Left - outerPosition.Left;
                UpdateIslandSizeVisuals(width, height);

                if (progress < 1)
                {
                    return;
                }

                StopIslandSizeAnimationFrames();
                Width = targetWidth;
                Height = targetHeight;
                IslandShellTranslate.X = 0;
                UpdateIslandSizeVisuals(targetWidth, targetHeight);
                var targetPosition = OverlayPositioner.GetVisiblePosition(
                    placement, screen, new OverlaySize(targetWidth, targetHeight));
                Left = targetPosition.Left;
                Top = targetPosition.Top;
            };

            CompositionTarget.Rendering += islandSizeAnimationFrameHandler;
        }

        private void StopIslandSizeAnimationFrames()
        {
            islandSizeAnimationActive = false;
            if (islandSizeAnimationFrameHandler == null)
            {
                return;
            }

            CompositionTarget.Rendering -= islandSizeAnimationFrameHandler;
            islandSizeAnimationFrameHandler = null;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!islandSizeAnimationActive)
            {
                UpdateIslandSizeVisuals(ActualWidth, ActualHeight);
            }
            if (islandVisible && !horizontalDragActive && positionAnimationFrameHandler == null)
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
            islandVisualWidth = width;
            islandVisualHeight = height;
            IslandShape.Data = CreateIslandGeometry(width, height);
        }

        private static Geometry CreateIslandGeometry(double width, double height)
        {
            var w = Math.Max(240, width);
            var h = Math.Max(60, height);
            var bottom = h - 5;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, 0), true, true);
                context.LineTo(new Point(w, 0), true, false);
                context.BezierTo(new Point(w - 28, 0), new Point(w - 38, 5), new Point(w - 44, 16), true, false);
                context.BezierTo(new Point(w - 48, 24), new Point(w - 48, h - 36), new Point(w - 48, h - 24), true, false);
                context.BezierTo(new Point(w - 48, h - 11), new Point(w - 56, bottom), new Point(w - 69, bottom), true, false);
                context.LineTo(new Point(69, bottom), true, false);
                context.BezierTo(new Point(56, bottom), new Point(48, h - 11), new Point(48, h - 24), true, false);
                context.BezierTo(new Point(48, h - 36), new Point(48, 24), new Point(44, 16), true, false);
                context.BezierTo(new Point(38, 5), new Point(28, 0), new Point(0, 0), true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private void SetIslandText(string primary, string secondary)
        {
            SetIslandText(primary, secondary, TimeSpan.FromSeconds(4));
        }

        private void SetIslandText(string primary, string secondary, TimeSpan lineDuration)
        {
            currentPrimaryText = primary ?? string.Empty;
            currentSecondaryText = secondary ?? string.Empty;
            currentLineDuration = lineDuration;
            RenderCurrentModuleState();
        }

        private void RenderCurrentModuleState()
        {
            var tutorialActive = tutorialFlow.IsActive;
            var taskbarSnapshot = new LyricsPresentationSnapshot
            {
                Session = currentSession,
                PendingPlaybackStatus = playbackIntents.GetDesiredStatus(currentSession?.SessionId),
                PrimaryText = currentPrimaryText,
                SecondaryText = currentSecondaryText,
                AccentText = string.Empty,
                TimelineReliability = currentTimelineReliability,
                EffectivePosition = currentEffectivePosition,
                LineDuration = currentLineDuration,
                IsWaitingForPlayback = currentSession == null
            };
            var islandState = taskbarSnapshot.ToIslandRenderState();
            if (tutorialActive)
            {
                islandState.PrimaryLyric = tutorialPrimaryText;
                islandState.SecondaryLyric = tutorialSecondaryText;
                islandState.PrimaryAccent = tutorialAccentText;
            }
            ModuleHost.Update(islandState);
            LyricDockController?.Present(taskbarSnapshot);
        }

        private Task PreviousRequested()
        {
            return ExecuteTrackChangeCommandAsync(mediaSessions.TrySkipPreviousAsync);
        }

        private Task PlayPauseRequested()
        {
            if (tutorialFlow.ControlClicked(IsTutorialTemporaryInteractionHeld()))
            {
                _ = ContinueTutorialAfterControlAsync(tutorialCancellation?.Token ?? CancellationToken.None);
            }

            var session = currentSession;
            if (session == null)
            {
                return Task.CompletedTask;
            }

            playbackIntents.Toggle(session.SessionId, session.PlaybackStatus);
            RenderCurrentModuleState();
            return SynchronizePlaybackIntentAsync(session.SessionId);
        }

        private async Task SynchronizePlaybackIntentAsync(string sessionId)
        {
            await playbackCommandGate.WaitAsync();
            try
            {
                while (true)
                {
                    var desired = playbackIntents.GetDesiredStatus(sessionId);
                    if (!desired.HasValue)
                    {
                        return;
                    }

                    var observed = mediaSessions.GetPlaybackStatus(sessionId);
                    if (observed == desired)
                    {
                        playbackIntents.Confirm(sessionId, observed.Value);
                        await mediaSessions.RefreshAsync();
                        AdoptRefreshedSession(sessionId);
                        RenderCurrentModuleState();
                        return;
                    }

                    var accepted = desired == MediaPlaybackStatus.Playing
                        ? await mediaSessions.TryPlayAsync(sessionId)
                        : await mediaSessions.TryPauseAsync(sessionId);
                    if (!accepted || !await WaitForPlaybackStatusAsync(sessionId, desired.Value))
                    {
                        playbackIntents.Cancel(sessionId);
                        await mediaSessions.RefreshAsync();
                        AdoptRefreshedSession(sessionId);
                        RenderCurrentModuleState();
                        return;
                    }

                    await mediaSessions.RefreshAsync();
                    AdoptRefreshedSession(sessionId);
                    var confirmed = mediaSessions.GetPlaybackStatus(sessionId);
                    if (confirmed.HasValue)
                    {
                        playbackIntents.Confirm(sessionId, confirmed.Value);
                    }
                    RenderCurrentModuleState();
                }
            }
            catch
            {
                playbackIntents.Cancel(sessionId);
                RenderCurrentModuleState();
            }
            finally
            {
                playbackCommandGate.Release();
            }
        }

        private async Task<bool> WaitForPlaybackStatusAsync(string sessionId, MediaPlaybackStatus desired)
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                if (!playbackIntents.GetDesiredStatus(sessionId).HasValue)
                {
                    return false;
                }

                if (mediaSessions.GetPlaybackStatus(sessionId) == desired)
                {
                    return true;
                }

                await Task.Delay(75);
            }

            return mediaSessions.GetPlaybackStatus(sessionId) == desired;
        }

        private void AdoptRefreshedSession(string sessionId)
        {
            if (!string.Equals(currentSession?.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var refreshed = mediaSessions.Sessions.FirstOrDefault(session =>
                string.Equals(session.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            if (refreshed != null)
            {
                currentSession = refreshed;
            }
        }

        private Task NextRequested()
        {
            return ExecuteTrackChangeCommandAsync(mediaSessions.TrySkipNextAsync);
        }

        private async Task ExecuteTrackChangeCommandAsync(Func<string, Task<bool>> command)
        {
            var session = currentSession;
            if (session == null)
            {
                return;
            }

            var originalTitle = session.Title ?? string.Empty;
            var originalArtist = session.Artist ?? string.Empty;
            var generation = ++trackChangeGeneration;
            try
            {
                if (!await command(session.SessionId))
                {
                    return;
                }

                for (var attempt = 0; attempt < 12; attempt++)
                {
                    await Task.Delay(attempt == 0 ? 80 : 120);
                    if (generation != trackChangeGeneration)
                    {
                        return;
                    }
                    await mediaSessions.RefreshAsync();
                    var refreshed = mediaSessions.Sessions.FirstOrDefault(candidate =>
                        string.Equals(candidate.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase));
                    if (refreshed != null &&
                        (!string.Equals(refreshed.Title, originalTitle, StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(refreshed.Artist, originalArtist, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (generation != trackChangeGeneration)
                        {
                            return;
                        }
                        currentSession = refreshed;
                        await RefreshAsync();
                        return;
                    }
                }

                await RefreshAsync();
            }
            catch
            {
                // The player may close or replace its SMTC session while changing tracks.
            }
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

        private void DisableTaskbarLyrics(LyricDockFailureReason reason)
        {
            if (!placementSettings.LyricDockEnabled)
            {
                return;
            }

            placementSettings.LyricDockEnabled = false;
            settingsStore.Save(placementSettings);
            ShowTaskbarLyricsFailure(reason);
        }

                private void ShowWidgetsHidingDegradedNotice()
        {
            // Security software with registry protection can block the TaskbarDa write that
            // auto-hides Widgets.  Tell the user once per enable cycle instead of failing silently.
            var message = "本机的安全软件（如火绒、联想电脑管家）阻止了任务栏小组件设置的自动修改，因此无法自动隐藏小组件。" +
                "任务栏歌词已在小组件旁正常显示。\n\n如需自动隐藏：请在安全软件中关闭对应的注册表/系统防护规则后重新开启歌词；" +
                "或手动在系统设置 > 个性化 > 任务栏中关闭小组件，歌词会自动使用腾出的空间。";
            var dialog = new InformationDialog(this, "小组件保持可见", message);
            dialog.ShowDialog();
        }

        private void ShowTaskbarLyricsFailure(LyricDockFailureReason reason)
        {
            var message = reason switch
            {
                LyricDockFailureReason.UnsupportedOS => "任务栏歌词仅支持 Windows 10 及以上版本，已保持关闭。",
                LyricDockFailureReason.WidgetsNotFound => "未能可靠找到所选屏幕的 Windows Widgets 按钮，已关闭任务栏歌词并恢复 Widgets。",
                LyricDockFailureReason.InsufficientSafeSpace => "所选任务栏没有至少 220 px 的连续安全空间，已关闭任务栏歌词。若任务栏开启了小组件，可在系统设置 > 个性化 > 任务栏中手动关闭以腾出空间后重试。",
                LyricDockFailureReason.RegistryOrRefreshFailed => "Windows Widgets 设置未能写入或验证生效，已关闭任务栏歌词并尝试恢复原状态。",
                LyricDockFailureReason.TaskbarNotFound => "未找到所选屏幕的任务栏，已关闭任务栏歌词并恢复 Widgets。",
                _ => "任务栏环境已改变且无法安全恢复，已关闭任务栏歌词并恢复 Widgets。"
            };
            var dialog = new InformationDialog(this, "任务栏歌词已关闭", message);
            dialog.ShowDialog();
        }

        private void OpenPlacementSettingsWindow(bool focusTaskbarLyrics = false)
        {
            if (settingsWindow != null)
            {
                SetSettingsWindowHoverSuppressed(true);
                ShowIsland();
                settingsWindow.Activate();
                if (focusTaskbarLyrics) settingsWindow.FocusTaskbarLyricsSettings();
                return;
            }

            settingsWindow = new PlacementSettingsWindow(
                screenCatalog.GetScreens(),
                placementSettings,
                ApplyPlacementSettings,
                mediaSessions.Sessions,
                InstalledPlayerCatalog.Detect(),
                BeginLayoutEditing,
                SaveLayoutEditing,
                CancelLayoutEditing,
                UpdateLyricsWidth,
                UpdateDividerSettings,
                RemoveDividers,
                SetModuleDragActive,
                SetSettingsWindowHoverSuppressed,
                GetLayoutDraftSnapshot,
                () => _ = StartTutorialAsync(),
                OnTutorialSettingsSectionChanged,
                TryExitTutorial);
            settingsWindow.Closed += async (sender, args) =>
            {
                settingsWindow = null;
                RestartNoPlaybackAutoRetractCountdown();
                SetSettingsWindowHoverSuppressed(false);
                await RefreshAsync();
            };
            SetSettingsWindowHoverSuppressed(true);
            ShowIsland();
            settingsWindow.Show();
            if (focusTaskbarLyrics) settingsWindow.FocusTaskbarLyricsSettings();
            OnTutorialSettingsOpened();
            BringTutorialSurfacesForward();
        }

        private void BeginLayoutEditing(IslandLayoutMode mode, bool resetToDefault)
        {
            committedModuleDrops.Reset();
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

        private IslandLayoutProfile GetLayoutDraftSnapshot(IslandLayoutMode mode)
        {
            return layoutEditing && layoutEditSession != null && mode == layoutEditingMode
                ? layoutEditSession.GetDraftSnapshot()
                : null;
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

        private void RemoveModuleAfterOutsideDrop(string instanceId)
        {
            if (!layoutEditing || layoutEditSession == null || string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            layoutEditSession.Remove(instanceId);
            ModuleHost.ClearInsertionPreview();
            ApplyInteractionState(IslandInteractionState.Editing, true);
            settingsWindow?.NotifyExternalSettingsChanged();
        }

        private void SetModuleDragActive(bool value)
        {
            if (moduleDragActive == value)
            {
                return;
            }

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

        private void RestartNoPlaybackAutoRetractCountdown()
        {
            if (currentSession != null)
            {
                noPlaybackSinceUtc = null;
                return;
            }

            startupHintTimer?.Stop();
            if (placementSettings.NoPlaybackAutoRetractSeconds <= 0)
            {
                noPlaybackSinceUtc = null;
                ShowIsland();
                return;
            }

            noPlaybackSinceUtc = DateTimeOffset.UtcNow;
            ShowIsland();
        }

        private void SetSettingsWindowHoverSuppressed(bool value)
        {
            if (settingsWindowHoverSuppressed == value)
            {
                return;
            }

            settingsWindowHoverSuppressed = value;
            ModuleHost.SetPlaybackInteractionEnabled(IsHoverTransparencySuppressed());
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
            committedModuleDrops.Reset();
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
            committedModuleDrops.Reset();
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
            if (!layoutEditing || layoutEditSession == null)
            {
                ClearModuleDragSizePreview(true);
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var payload = IslandLayoutDragPayload.FromData(e.Data);
            if (payload == null || !IsIslandDropPoint(e))
            {
                ModuleHost.ClearInsertionPreview();
                ClearModuleDragSizePreview(true);
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var acceptedEffect = payload?.NewType.HasValue == true
                ? DragDropEffects.Copy
                : DragDropEffects.Move;
            e.Effects = acceptedEffect;
            QueueModuleDragPreview(payload, e.GetPosition(ModuleHost).X);
            e.Handled = true;
        }

        private void ModuleHost_Drop(object sender, DragEventArgs e)
        {
            var payload = IslandLayoutDragPayload.FromData(e.Data);
            var validTarget = payload != null && IsIslandDropPoint(e);
            queuedModuleDragPayload = null;
            var destinationIndex = validTarget
                ? FindModuleInsertionIndex(e.GetPosition(ModuleHost).X, payload)
                : -1;
            var index = validTarget
                ? ModuleHost.GetCommittedInsertionIndex(payload, destinationIndex)
                : -1;
            ModuleHost.ClearInsertionPreview();
            var hadSizePreview = moduleDragPreviewWidth > 0;
            moduleDragPreviewWidth = 0;
            if (!layoutEditing || layoutEditSession == null)
            {
                if (hadSizePreview)
                {
                    ApplyMeasuredIslandSize(true);
                }
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (index >= 0 && payload != null)
            {
                if (!committedModuleDrops.TryCommit(payload.OperationId))
                {
                    if (hadSizePreview)
                    {
                        ApplyMeasuredIslandSize(true);
                    }
                    e.Handled = true;
                    return;
                }

                if (payload.NewType.HasValue)
                {
                    layoutEditSession.Add(payload.NewType.Value, index);
                }
                else if (!string.IsNullOrWhiteSpace(payload.ExistingInstanceId))
                {
                    layoutEditSession.Move(payload.ExistingInstanceId, index);
                }

                ApplyInteractionState(IslandInteractionState.Editing, payload.NewType.HasValue);
                settingsWindow?.NotifyExternalSettingsChanged();
                e.Effects = payload.NewType.HasValue ? DragDropEffects.Copy : DragDropEffects.Move;
            }
            else if (hadSizePreview)
            {
                ApplyMeasuredIslandSize(true);
                e.Effects = DragDropEffects.None;
            }
            else if (!validTarget)
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void ModuleHost_DragLeave(object sender, DragEventArgs e)
        {
            if (IsIslandDropPoint(e))
            {
                e.Handled = true;
                return;
            }

            var payload = IslandLayoutDragPayload.FromData(e.Data);
            if (!string.IsNullOrWhiteSpace(payload?.ExistingInstanceId))
            {
                ModuleHost.ShowRemovalPreview(payload.ExistingInstanceId);
                ApplyMeasuredIslandSize(true);
            }
            else
            {
                ModuleHost.ClearInsertionPreview();
                ClearModuleDragSizePreview(true);
            }
            queuedModuleDragPayload = null;
            e.Handled = true;
        }

        private int FindModuleInsertionIndex(double pointerX, IslandLayoutDragPayload payload)
        {
            return ModuleHost.FindInsertionIndex(pointerX, payload);
        }

        private void QueueModuleDragPreview(IslandLayoutDragPayload payload, double pointerX)
        {
            queuedModuleDragPayload = payload;
            queuedModuleDragPointerX = pointerX;
            if (moduleDragPreviewQueued)
            {
                return;
            }

            moduleDragPreviewQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                moduleDragPreviewQueued = false;
                var latestPayload = queuedModuleDragPayload;
                if (latestPayload == null || !layoutEditing || layoutEditSession == null)
                {
                    return;
                }

                var index = FindModuleInsertionIndex(queuedModuleDragPointerX, latestPayload);
                var previewWidth = ModuleHost.GetDragPreviewWidth(latestPayload);
                ModuleHost.ShowInsertionPreview(index, previewWidth, latestPayload);
                PreviewModuleDragSize(latestPayload.NewType.HasValue ? previewWidth : 0);
            }));
        }

        private bool IsIslandDropPoint(DragEventArgs e)
        {
            var point = e.GetPosition(IslandShape);
            return IslandShape.InputHitTest(point) != null;
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

        private async Task StartTutorialAsync()
        {
            await tutorialStartGate.WaitAsync();
            try
            {
                await StopTutorialAsync(false, false);

                tutorialCancellation = new CancellationTokenSource();
                tutorialFlow.Start();
                RegisterTutorialEscapeHotkey();
                tutorialHoverSuppressed = true;
                startupHintTimer?.Stop();
                HideHoverTransparency();
                tutorialLayoutOverride = CreateTutorialProfile(IslandModuleType.Lyrics);
                SetTutorialText("即将开始教学模式", "单击LyricHover继续");
                ApplyInteractionState(IslandInteractionState.Expanded, true);
                RenderCurrentModuleState();
                ShowIsland();

                tutorialMaskWindow = new TutorialMaskWindow();
                tutorialMaskWindow.Show();
                tutorialExitWindow = CreateTutorialActionWindow(
                    "退出教学模式", Color.FromRgb(75, 85, 99), true,
                    () => _ = StopTutorialAsync(true, true));
                tutorialExitWindow.Show();
                BringTutorialSurfacesForward();
                await Task.WhenAll(
                    tutorialMaskWindow.FadeInAsync(TimeSpan.FromMilliseconds(500)),
                    tutorialExitWindow.FadeInAsync(TimeSpan.FromMilliseconds(500)));
            }
            finally
            {
                tutorialStartGate.Release();
            }
        }

        private void OnTutorialSettingsOpened()
        {
            var purpose = tutorialFlow.SettingsOpened();
            if (purpose == TutorialSettingsOpenPurpose.FirstSettings)
            {
                _ = RunTutorialBasicsAsync(tutorialCancellation?.Token ?? CancellationToken.None);
            }
            else if (purpose == TutorialSettingsOpenPurpose.CustomModules)
            {
                SetTutorialText("请点击左侧“模块布局”", string.Empty);
            }
        }

        private void OnTutorialSettingsSectionChanged(string section)
        {
            if (string.Equals(section, "Layout", StringComparison.Ordinal) && tutorialFlow.LayoutPageSelected())
            {
                settingsWindow?.PulseLayoutEditSettingsHighlight();
                _ = RunTutorialCustomizationIntroAsync(tutorialCancellation?.Token ?? CancellationToken.None);
            }
        }

        private async Task RunTutorialBasicsAsync(CancellationToken cancellationToken)
        {
            try
            {
                SetTutorialText("真棒！", string.Empty);
                await DelayTutorialAsync(1100, cancellationToken);
                SetTutorialText("拖动LyricHover可左右移动", "在“设置-位置”里也可以调整位置");
                await FadeOutSettingsWindowAsync(TimeSpan.FromSeconds(1));
                await DelayTutorialAsync(2600, cancellationToken);

                SetTutorialText("接下来演示鼠标避让", "请把鼠标移动到岛上");
                await DelayTutorialAsync(1000, cancellationToken);
                tutorialHoverSuppressed = false;
                ModuleHost.SetPlaybackInteractionEnabled(IsHoverTransparencySuppressed());
                UpdateHoverProximity();
                tutorialHoverEnteredCompletion = new TaskCompletionSource<bool>();
                await WaitForTutorialHoverAsync(cancellationToken);
                await DelayTutorialAsync(900, cancellationToken);

                SetTutorialText("该功能可方便看到岛下内容", "无需频繁拖动LyricHover，助你高效工作");
                await DelayTutorialAsync(3200, cancellationToken);
                SetTutorialText("可以透过岛直接左键点击控制岛下内容", string.Empty, "（新功能！）");
                await DelayTutorialAsync(3000, cancellationToken);

                tutorialLayoutOverride = CreateTutorialProfile(IslandModuleType.Lyrics, IslandModuleType.Divider, IslandModuleType.PlaybackControls);
                ApplyInteractionState(IslandInteractionState.Expanded, true);
                await ModuleHost.AnimateModulesInAsync(
                    new[] { tutorialDividerModuleId, tutorialControlsModuleId },
                    260,
                    120,
                    cancellationToken);
                SetTutorialText("新版本增加了音乐控制功能", string.Empty);
                await DelayTutorialAsync(2200, cancellationToken);

                SetTutorialText("按下" + GetTemporaryInteractionGesture() + "可暂时关闭鼠标避让来点击控制按钮", "来试试看！");
                tutorialFlow.BeginControlClickPractice();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                tutorialHoverEnteredCompletion = null;
            }
        }

        private async Task ContinueTutorialAfterControlAsync(CancellationToken cancellationToken)
        {
            try
            {
                SetTutorialText("快捷键可在设置中修改", string.Empty);
                await DelayTutorialAsync(1800, cancellationToken);
                await ModuleHost.AnimateModulesOutAsync(
                    new[] { tutorialControlsModuleId, tutorialDividerModuleId },
                    260,
                    120,
                    cancellationToken);
                tutorialLayoutOverride = CreateTutorialProfile(IslandModuleType.Lyrics);
                ApplyInteractionState(IslandInteractionState.Expanded, true);

                placementSettings.IslandLayouts = placementSettings.IslandLayouts ?? IslandLayoutDefaults.Create();
                placementSettings.IslandLayouts.Mode = IslandLayoutMode.HorizontalBlocks;
                tutorialLayoutOverride = null;
                tutorialFlow.RequestCustomSettings();
                SetTutorialText("现在我们来体验新功能——自定义模块", "现在右键岛打开设置");
                ApplyInteractionState(IslandInteractionState.Expanded, true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RunTutorialCustomizationIntroAsync(CancellationToken cancellationToken)
        {
            try
            {
                SetTutorialText("您可以直接拖动“自定义模块”部分的内容到岛里", "进行自定义布局");
                await DelayTutorialAsync(3500, cancellationToken);
                SetTutorialText("所有模块可直接鼠标拖入岛添加、拖动排序、拖出岛删除", "同一模块可拖入多个");
                await DelayTutorialAsync(4600, cancellationToken);
                SetTutorialText("来拖动试试看吧", string.Empty);

                tutorialNextWindow?.Close();
                tutorialNextWindow = CreateTutorialActionWindow(
                    "下一步", Color.FromRgb(22, 119, 255), false,
                    () => _ = CompleteTutorialCustomizationAsync());
                tutorialNextWindow.Show();
                BringTutorialSurfacesForward();
                await tutorialNextWindow.PulseInAsync(TimeSpan.FromMilliseconds(820));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task CompleteTutorialCustomizationAsync()
        {
            if (!tutorialFlow.CompleteCustomization())
            {
                return;
            }

            tutorialNextWindow?.Close();
            tutorialNextWindow = null;
            settingsWindow?.ApplyPendingChangesForTutorial();
            tutorialLayoutOverride = null;
            await RunTutorialLayoutDemoAsync(tutorialCancellation?.Token ?? CancellationToken.None);
        }

        private async Task RunTutorialLayoutDemoAsync(CancellationToken cancellationToken)
        {
            try
            {
                var layouts = placementSettings.IslandLayouts ?? IslandLayoutDefaults.Create();
                tutorialLayoutOverride = layouts.Horizontal;
                ApplyInteractionState(IslandInteractionState.Expanded, true);
                SetTutorialText("现在我们来看看两种布局模式", string.Empty);
                await DelayTutorialAsync(2400, cancellationToken);
                SetTutorialText("水平积木", string.Empty);
                await DelayTutorialAsync(1700, cancellationToken);
                SetTutorialText("你设置的布局一字排开，信息一眼可见", string.Empty);
                await DelayTutorialAsync(3000, cancellationToken);
                SetTutorialText("刚刚你设置的模块布局已经保存在了水平积木", string.Empty);
                await DelayTutorialAsync(3000, cancellationToken);

                // Keep the user's current horizontal layout visible while explaining the
                // alternate hold-to-expand behavior; the tutorial should not switch modes.
                SetTutorialText("自动折叠模式", "按住 " + GetTemporaryInteractionGesture() + " 即时展开，松开后自动折叠");
                await DelayTutorialAsync(1800, cancellationToken);
                SetTutorialText("平时保持紧凑，只显示核心模块", string.Empty);
                await DelayTutorialAsync(2500, cancellationToken);
                SetTutorialText("按住 " + GetTemporaryInteractionGesture() + " 后显示你的完整模块布局", "与水平积木布局独立");
                await DelayTutorialAsync(3500, cancellationToken);
                SetTutorialText("🎉教学模式已结束！快去体验吧！！", string.Empty);
                await DelayTutorialAsync(2300, cancellationToken);
                tutorialFlow.Complete();
                await StopTutorialAsync(true, true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task StopTutorialAsync(bool fadeMask, bool refresh)
        {
            if (tutorialStopping)
            {
                return tutorialStopTask;
            }

            tutorialStopping = true;
            tutorialStopTask = StopTutorialCoreAsync(fadeMask, refresh);
            return tutorialStopTask;
        }

        private async Task StopTutorialCoreAsync(bool fadeMask, bool refresh)
        {
            try
            {
                var cancellation = tutorialCancellation;
                tutorialCancellation = null;
                cancellation?.Cancel();
                hotkeyService?.Unregister(TutorialEscapeHotkeyId);
                if (fadeMask)
                {
                    var fadeDuration = TimeSpan.FromSeconds(1);
                    await Task.WhenAll(
                        tutorialMaskWindow?.FadeOutAsync(fadeDuration) ?? Task.CompletedTask,
                        tutorialExitWindow?.FadeOutAsync(fadeDuration) ?? Task.CompletedTask,
                        tutorialNextWindow?.FadeOutAsync(fadeDuration) ?? Task.CompletedTask);
                }

                CloseTutorialWindows();
                tutorialFlow.Exit();
                tutorialLayoutOverride = null;
                tutorialPrimaryText = string.Empty;
                tutorialSecondaryText = string.Empty;
                tutorialAccentText = string.Empty;
                tutorialHoverSuppressed = false;
                tutorialHoverEnteredCompletion = null;
                ModuleHost.SetPlaybackInteractionEnabled(IsHoverTransparencySuppressed());
                ApplyInteractionState(interactionController.GetState(GetInteractionClock()), true);
                RenderCurrentModuleState();
                if (refresh && IsLoaded)
                {
                    await RefreshAsync();
                }
            }
            finally
            {
                tutorialStopping = false;
            }
        }

        private void CloseTutorialWindows()
        {
            tutorialNextWindow?.Close();
            tutorialExitWindow?.Close();
            tutorialMaskWindow?.Close();
            tutorialNextWindow = null;
            tutorialExitWindow = null;
            tutorialMaskWindow = null;
        }

        private TutorialActionWindow CreateTutorialActionWindow(string text, Color color, bool topRight, Action clicked)
        {
            var window = new TutorialActionWindow(text, color, clicked, () => TryExitTutorial(), !topRight);
            var screen = ResolveScreen();
            window.Left = screen.WorkLeft + screen.WorkWidth - window.Width - 24;
            window.Top = topRight ? screen.WorkTop + 24 : screen.WorkTop + (screen.WorkHeight - window.Height) / 2;
            return window;
        }

        private void BringTutorialSurfacesForward()
        {
            if (!tutorialFlow.IsActive)
            {
                return;
            }

            Topmost = false;
            Topmost = true;
            tutorialExitWindow?.BringForward();
            tutorialNextWindow?.BringForward();
        }

        private async Task FadeOutSettingsWindowAsync(TimeSpan duration)
        {
            var window = settingsWindow;
            if (window == null)
            {
                return;
            }

            var completion = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation(0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            animation.Completed += (sender, args) => completion.TrySetResult(true);
            window.BeginAnimation(OpacityProperty, animation);
            await completion.Task;
            if (ReferenceEquals(settingsWindow, window))
            {
                window.Close();
            }
        }

        private async Task WaitForTutorialHoverAsync(CancellationToken cancellationToken)
        {
            var hoverTask = tutorialHoverEnteredCompletion?.Task ?? Task.CompletedTask;
            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completed = await Task.WhenAny(hoverTask, cancellationTask);
            if (completed == cancellationTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static Task DelayTutorialAsync(int milliseconds, CancellationToken cancellationToken) => Task.Delay(milliseconds, cancellationToken);

        private void SetTutorialText(string primary, string secondary, string accent = "")
        {
            tutorialPrimaryText = primary ?? string.Empty;
            tutorialSecondaryText = secondary ?? string.Empty;
            tutorialAccentText = accent ?? string.Empty;
            currentLineDuration = TimeSpan.FromSeconds(5);
            RenderCurrentModuleState();
        }

        private bool IsTutorialTemporaryInteractionHeld()
        {
            return IsTemporaryInteractionHeld();
        }

        private bool TryExitTutorial()
        {
            if (!tutorialFlow.IsActive)
            {
                return false;
            }

            _ = StopTutorialAsync(true, true);
            return true;
        }

        private IslandLayoutProfile EnsureTutorialLyricsVisible(IslandLayoutProfile profile)
        {
            return tutorialFlow.IsActive ? EnsureProfileHasLyrics(profile) : profile;
        }

        private IslandLayoutProfile EnsureProfileHasLyrics(IslandLayoutProfile profile)
        {
            if (profile?.Modules?.Any(module => module != null && module.Type == IslandModuleType.Lyrics) == true)
            {
                return profile;
            }

            var clone = CloneProfile(profile);
            clone.Modules.Insert(0, CreateTutorialModule(IslandModuleType.Lyrics));
            return clone;
        }

        private IslandLayoutProfile CreateTutorialProfile(params IslandModuleType[] types)
        {
            return new IslandLayoutProfile { Modules = types.Select(CreateTutorialModule).ToList() };
        }

        private IslandModuleInstance CreateTutorialModule(IslandModuleType type)
        {
            return new IslandModuleInstance(type)
            {
                Id = type == IslandModuleType.Lyrics
                    ? tutorialLyricsModuleId
                    : type == IslandModuleType.Divider
                        ? tutorialDividerModuleId
                        : type == IslandModuleType.PlaybackControls
                            ? tutorialControlsModuleId
                            : Guid.NewGuid().ToString("N"),
                LyricsWidth = type == IslandModuleType.Lyrics ? 680 : IslandModuleInstance.DefaultLyricsWidth,
                DividerOpacity = 0.32,
                MarginBefore = type == IslandModuleType.Divider ? 6 : 4,
                MarginAfter = type == IslandModuleType.Divider ? 6 : 4
            };
        }

        private static IslandLayoutProfile CloneProfile(IslandLayoutProfile profile)
        {
            return new IslandLayoutProfile
            {
                Modules = (profile?.Modules ?? new System.Collections.Generic.List<IslandModuleInstance>())
                    .Where(module => module != null)
                    .Select(module => new IslandModuleInstance(module.Type)
                    {
                        Id = module.Id,
                        LyricsWidth = module.LyricsWidth,
                        DividerOpacity = module.DividerOpacity,
                        MarginBefore = module.MarginBefore,
                        MarginAfter = module.MarginAfter
                    }).ToList()
            };
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
            tutorialHoverEnteredCompletion?.TrySetResult(true);
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

            var shouldForwardClick = horizontalDragPending && !horizontalDragActive && ShouldForwardLeftClickThrough();
            var shouldHandleIslandClick = horizontalDragPending && !horizontalDragActive;
            var shouldContinueTutorial = shouldHandleIslandClick && tutorialFlow.Step == TutorialStep.AwaitingIslandClick;
            FinishHorizontalDrag();
            if (shouldContinueTutorial && tutorialFlow.ContinueFromIslandClick())
            {
                SetTutorialText("请右键LyricHover打开设置", "从菜单中选择“偏好设置”");
                e.Handled = true;
                return;
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
            tutorialHoverEnteredCompletion?.TrySetResult(true);
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
            if (e.Key == Key.Escape && TryExitTutorial())
            {
                e.Handled = true;
                return;
            }

            if (IsHoverTransparencySuppressed())
            {
                ModuleHost.SetPlaybackInteractionEnabled(true);
                HideHoverTransparency();
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


