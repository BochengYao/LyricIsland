using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Services.Store;
using LyricHover.App.LayoutEditing;
using LyricHover.App.Media;
using LyricHover.App.TaskbarLyrics;
using LyricHover.Core;
using LyricHover.Core.Layout;
using LyricHover.Core.Media;

namespace LyricHover.App
{
    public partial class PlacementSettingsWindow : Window
    {
        private readonly IReadOnlyList<OverlayScreenArea> screens;
        private readonly IReadOnlyList<MediaSessionSnapshot> playerSessions;
        private readonly IReadOnlyList<InstalledPlayer> installedPlayers;
        private readonly Action<OverlayPlacementSettings> applySettings;
        private readonly Action<IslandLayoutMode, bool> beginLayoutEditing;
        private readonly Action saveLayoutEditing;
        private readonly Action cancelLayoutEditing;
        private readonly Action<IslandLayoutMode, double> updateLyricsWidth;
        private readonly Action<IslandLayoutMode, double, double> updateDividerSettings;
        private readonly Action<IslandLayoutMode> removeDividers;
        private readonly Action<bool> setModuleDragActive;
        private readonly Action<bool> setHoverTransparencySuppressed;
        private readonly Func<IslandLayoutMode, IslandLayoutProfile> getLayoutDraftSnapshot;
        private readonly Action startTutorial;
        private readonly Action<string> tutorialSectionChanged;
        private readonly Func<bool> tryExitTutorial;
        private int acceptedCacheLimitMegabytes;
        private OverlayPlacementSettings workingSettings;
        private SettingsDirtyStateTracker<OverlayPlacementSettings> dirtyStateTracker;
        private SettingsThemePreference selectedThemePreference = SettingsThemePreference.System;
        private bool initializingSettings = true;
        private bool suppressTaskbarLyricsConfirmation;
        private bool settingsDirty;
        private bool dirtyStateUpdateQueued;
        private bool layoutEditingActive;
        private bool suppressLayoutSelectionChanged;
        private IslandLayoutMode selectedLayoutMode = IslandLayoutMode.HorizontalBlocks;
        private bool systemThemeEventsSubscribed;
        private Point? moduleToolboxDragStartPoint;
        private ModuleToolboxItemDescriptor moduleToolboxDragOption;
        private bool moduleToolboxDragInProgress;
        private ModuleDragGhostWindow moduleDragGhost;
        private bool suppressPlayerSelectionChanged;
        private DispatcherTimer translationModeToastTimer;
        private Storyboard layoutModePreviewStoryboard;
        private int themeTransitionVersion;
        private readonly StoreProEntitlementService proEntitlementService;
        private readonly SupporterBadgeIdentityStore supporterBadgeIdentityStore;
        private SupporterBadgeIdentity supporterBadgeIdentity;
        private ProEntitlementKind proEntitlementKind;
        private DateTimeOffset? proEntitlementAcquiredAt;
        private bool proEntitlementRefreshInProgress;
        private SupporterBadgePreviewWindow supporterBadgePreviewWindow;

        private const string MicrosoftStoreProductId = "9NRXZP5HMXK2";
        private const string MicrosoftStoreProductUrl = "https://apps.microsoft.com/detail/9nrxzp5hmxk2";
        internal const string MicrosoftStoreProProductId = "lyric_island_pro";
        private const string WebsiteUrl = "https://lyric-island.top/";
        private const string FeedbackUrl = "https://lyric-island.top/incentives/";
        private const string SupporterBadgeButtonText = "查看我的支持者徽章";
        private const string PurchaseIconGeometry = "M4,7 L16,7 L17,18 L3,18 Z M7,7 L7,5 A3,3 0 0 1 13,5 L13,7";
        private const string BadgeIconGeometry = "M7.21,15 L2.66,7.14 A2,2 0 0 1 2.79,4.94 L4.4,2.8 A2,2 0 0 1 6,2 H18 A2,2 0 0 1 19.6,2.8 L21.2,4.94 A2,2 0 0 1 21.34,7.14 L16.79,15 M11,12 L5.12,2.2 M13,12 L18.88,2.2 M8,7 H16 M12,12 A5,5 0 1 1 12,22 A5,5 0 1 1 12,12 M12,18 V16 H11.5";

        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        public PlacementSettingsWindow(
            IReadOnlyList<OverlayScreenArea> screens,
            OverlayPlacementSettings currentSettings,
            Action<OverlayPlacementSettings> applySettings,
            IReadOnlyList<MediaSessionSnapshot> playerSessions = null,
            IReadOnlyList<InstalledPlayer> installedPlayers = null,
            Action<IslandLayoutMode, bool> beginLayoutEditing = null,
            Action saveLayoutEditing = null,
            Action cancelLayoutEditing = null,
            Action<IslandLayoutMode, double> updateLyricsWidth = null,
            Action<IslandLayoutMode, double, double> updateDividerSettings = null,
            Action<IslandLayoutMode> removeDividers = null,
            Action<bool> setModuleDragActive = null,
            Action<bool> setHoverTransparencySuppressed = null,
            Func<IslandLayoutMode, IslandLayoutProfile> getLayoutDraftSnapshot = null,
            Action startTutorial = null,
            Action<string> tutorialSectionChanged = null,
            Func<bool> tryExitTutorial = null)
        {
            InitializeComponent();
            var localApplicationDataRoot = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var productDataRoot = ProductDataDirectory.Prepare(localApplicationDataRoot);
            proEntitlementService = new StoreProEntitlementService(localApplicationDataRoot);
            supporterBadgeIdentityStore = new SupporterBadgeIdentityStore(
                System.IO.Path.Combine(productDataRoot, "supporter-badge-identity.json"));
            supporterBadgeIdentity = supporterBadgeIdentityStore.Load();
            UpdateSupporterIdentityControls();
            this.screens = screens ?? new List<OverlayScreenArea>().AsReadOnly();
            this.playerSessions = playerSessions ?? new List<MediaSessionSnapshot>().AsReadOnly();
            this.installedPlayers = installedPlayers ?? new List<InstalledPlayer>().AsReadOnly();
            this.applySettings = applySettings ?? throw new ArgumentNullException(nameof(applySettings));
            this.beginLayoutEditing = beginLayoutEditing;
            this.saveLayoutEditing = saveLayoutEditing;
            this.cancelLayoutEditing = cancelLayoutEditing;
            this.updateLyricsWidth = updateLyricsWidth;
            this.updateDividerSettings = updateDividerSettings;
            this.removeDividers = removeDividers;
            this.setModuleDragActive = setModuleDragActive;
            this.setHoverTransparencySuppressed = setHoverTransparencySuppressed;
            this.getLayoutDraftSnapshot = getLayoutDraftSnapshot;
            this.startTutorial = startTutorial;
            this.tutorialSectionChanged = tutorialSectionChanged;
            this.tryExitTutorial = tryExitTutorial;
            PreviewKeyDown += PlacementSettingsWindow_PreviewKeyDown;

            ScreenComboBox.ItemsSource = this.screens
                .Select((screen, index) => new ScreenOption(screen.Name, "显示器 " + (index + 1) + " (" + (int)screen.WorkWidth + " x " + (int)screen.WorkHeight + ")"))
                .ToList();
            LyricsSourceComboBox.ItemsSource = new[]
            {
                new LyricsSourceOption(LyricsSourcePreference.Automatic, "自动选择"),
                new LyricsSourceOption(LyricsSourcePreference.LrcLib, "LRCLIB"),
                new LyricsSourceOption(LyricsSourcePreference.QQMusic, "QQ 音乐"),
                new LyricsSourceOption(LyricsSourcePreference.KuGou, "酷狗"),
                new LyricsSourceOption(LyricsSourcePreference.NetEase, "网易云")
            };

            var settings = (currentSettings ?? new OverlayPlacementSettings()).DeepClone();
            settings.Normalize();
            workingSettings = settings;
            workingSettings.Normalize();
            acceptedCacheLimitMegabytes = settings.CacheLimitMegabytes;
            selectedThemePreference = settings.SettingsTheme;
            SetThemeRadioButton(selectedThemePreference);
            ApplySettingsTheme();
            LyricsSourceComboBox.SelectedValue = settings.LyricsSource;
            SingleLineRadioButton.IsChecked = !settings.UseMultiLineDisplay;
            MultiLineRadioButton.IsChecked = settings.UseMultiLineDisplay;
            ShowTranslationCheckBox.IsChecked = settings.ShowTranslation;
            TaskbarLyricsEnabledCheckBox.IsChecked = settings.TaskbarLyricsEnabled;
            TaskbarLyricsAlignmentComboBox.SelectedIndex = settings.TaskbarLyricsAlignment == TaskbarLyricsAlignment.Left ? 1 : 0;
            ScreenComboBox.SelectedValue = string.IsNullOrWhiteSpace(settings.ScreenName)
                ? this.screens.FirstOrDefault()?.Name
                : settings.ScreenName;
            OffsetSlider.Value = Math.Max(0, Math.Min(100, settings.OffsetRatio * 100));
            NoPlaybackAutoRetractSlider.Value = settings.NoPlaybackAutoRetractSeconds;
            ExpandedAutoCollapseSlider.Value = settings.ExpandedAutoCollapseSeconds;
            CacheLimitTextBox.Text = settings.CacheLimitMegabytes.ToString(CultureInfo.InvariantCulture);
            HoverAuraSizeSlider.Value = settings.HoverAuraSize;
            HoverDetectionRangeSlider.Value = settings.HoverDetectionRange;
            HoverAuraAspectRatioSlider.Value = settings.HoverAuraAspectRatio;
            PassThroughOnHoverCheckBox.IsChecked = settings.PassThroughOnHover;
            EarlierHotkeyTextBox.Text = settings.LyricOffsetHotkeys.Earlier;
            LaterHotkeyTextBox.Text = settings.LyricOffsetHotkeys.Later;
            ResetHotkeyTextBox.Text = settings.LyricOffsetHotkeys.Reset;
            TemporaryInteractionHotkeyTextBox.Text = settings.LyricOffsetHotkeys.TemporaryInteraction;
            UpdateExpandableInteractionHint();
            SetHoverSpectrumControls(settings.HoverSpectrumStops);
            InitializeLayoutEditingControls(settings);
            InitializeLayoutModePreviewAnimation();
            InitializePlayerSelection(settings);
            UpdateTranslationLineModeLock();
            UpdateSettingValueLabels();
            LyricsSectionButton.IsChecked = true;
            ShowSection("Lyrics");
            dirtyStateTracker = new SettingsDirtyStateTracker<OverlayPlacementSettings>(
                CaptureSettings(),
                CreateSettingsFingerprint);
            InitializeActionButtonBrushes();
            AttachSettingsChangeHandlers();
            initializingSettings = false;
            RefreshActionButtonVisuals(false);
            Loaded += (sender, args) =>
            {
                SubscribeToSystemThemeChanges();
                ApplySettingsTheme();
                UpdateSegmentSelectionPositions(false);
                CenterOnDesktop();
                setHoverTransparencySuppressed?.Invoke(true);
                UpdateLayoutModePreviewAnimation();
            };
            Closing += (sender, args) =>
            {
                translationModeToastTimer?.Stop();
                supporterBadgePreviewWindow?.Close();
                supporterBadgePreviewWindow = null;
                TranslationModeToast.BeginAnimation(OpacityProperty, null);
                TranslationModeToast.Visibility = Visibility.Collapsed;
                moduleDragGhost?.Close();
                moduleDragGhost = null;
                StopLayoutModePreviewAnimation();
                setHoverTransparencySuppressed?.Invoke(false);
                if (layoutEditingActive)
                {
                    CancelLayoutEditing();
                }
            };
            Closed += (sender, args) => UnsubscribeFromSystemThemeChanges();
        }

        public void NotifyExternalSettingsChanged()
        {
            QueueDirtyStateUpdate();
        }

        public void ApplyPendingChangesForTutorial()
        {
            ApplyCurrentSettings();
        }

        public void FocusTaskbarLyricsSettings()
        {
            ShowSection("Lyrics");
            LyricsSectionButton.IsChecked = true;
            TaskbarLyricsEnabledCheckBox.Focus();
        }

        private void PlacementSettingsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && tryExitTutorial?.Invoke() == true)
            {
                e.Handled = true;
            }
        }

        private void AttachSettingsChangeHandlers()
        {
            foreach (var selector in new Selector[]
            {
                LyricsSourceComboBox,
                ScreenComboBox,
                PlayerSelectionComboBox
                ,TaskbarLyricsAlignmentComboBox
            })
            {
                selector.SelectionChanged += SettingsSelector_SelectionChanged;
            }

            foreach (var range in new RangeBase[]
            {
                OffsetSlider,
                NoPlaybackAutoRetractSlider,
                ExpandedAutoCollapseSlider,
                HoverAuraSizeSlider,
                HoverDetectionRangeSlider,
                HoverAuraAspectRatioSlider,
                SpectrumMidPositionSlider,
                SpectrumCenterTransparencySlider,
                SpectrumMidTransparencySlider,
                SpectrumEdgeTransparencySlider,
                LyricsWidthSlider,
                DividerOpacitySlider,
                DividerSpacingSlider
            })
            {
                range.ValueChanged += SettingsRange_ValueChanged;
            }

            foreach (var textBox in new[]
            {
                CacheLimitTextBox,
                EarlierHotkeyTextBox,
                LaterHotkeyTextBox,
                ResetHotkeyTextBox,
                TemporaryInteractionHotkeyTextBox
            })
            {
                textBox.TextChanged += SettingsTextBox_TextChanged;
            }

            foreach (var toggle in new ToggleButton[]
            {
                SingleLineRadioButton,
                MultiLineRadioButton,
                ShowTranslationCheckBox,
                TaskbarLyricsEnabledCheckBox,
                PassThroughOnHoverCheckBox,
                LightThemeRadioButton,
                DarkThemeRadioButton,
                SystemThemeRadioButton
            })
            {
                toggle.Checked += SettingsToggle_Changed;
                toggle.Unchecked += SettingsToggle_Changed;
            }
        }

        private void SettingsSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            QueueDirtyStateUpdate();
        }

        private void SettingsRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            QueueDirtyStateUpdate();
        }

        private void SettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ReferenceEquals(sender, TemporaryInteractionHotkeyTextBox))
            {
                UpdateExpandableInteractionHint();
            }
            QueueDirtyStateUpdate();
        }

        private void UpdateExpandableInteractionHint()
        {
            if (ExpandablePreviewShortcutRun == null || TemporaryInteractionHotkeyTextBox == null)
            {
                return;
            }

            var gesture = string.IsNullOrWhiteSpace(TemporaryInteractionHotkeyTextBox.Text)
                ? "Ctrl"
                : TemporaryInteractionHotkeyTextBox.Text.Trim();
            ExpandablePreviewShortcutRun.Text = gesture;
        }

        private void SettingsToggle_Changed(object sender, RoutedEventArgs e)
        {
            QueueDirtyStateUpdate();
        }

        private void ComboBoxItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ComboBoxItem;
            var comboBox = item == null
                ? null
                : ItemsControl.ItemsControlFromItemContainer(item) as ComboBox;
            if (comboBox == null)
            {
                return;
            }

            var selected = comboBox.ItemContainerGenerator.ItemFromContainer(item);
            if (selected != DependencyProperty.UnsetValue)
            {
                comboBox.SelectedItem = selected;
            }
            comboBox.IsDropDownOpen = false;
            comboBox.Focus();
            e.Handled = true;
        }

        private void QueueDirtyStateUpdate()
        {
            if (initializingSettings || dirtyStateTracker == null || dirtyStateUpdateQueued)
            {
                return;
            }

            dirtyStateUpdateQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                dirtyStateUpdateQueued = false;
                var dirty = dirtyStateTracker.IsDirty(CaptureSettings());
                if (dirty == settingsDirty)
                {
                    return;
                }

                settingsDirty = dirty;
                RefreshActionButtonVisuals(true);
            }));
        }

        private void InitializeActionButtonBrushes()
        {
            var background = GetThemeColor("SettingsControlBackgroundBrush", Colors.Transparent);
            var border = GetThemeColor("SettingsControlBorderBrush", Colors.Gray);
            var foreground = GetThemeColor("SettingsControlForegroundBrush", Colors.White);
            ApplyButton.Background = new SolidColorBrush(background);
            ApplyButton.BorderBrush = new SolidColorBrush(border);
            ApplyButton.Foreground = new SolidColorBrush(foreground);
            SaveButton.Background = new SolidColorBrush(background);
            SaveButton.BorderBrush = new SolidColorBrush(border);
            SaveButton.Foreground = new SolidColorBrush(foreground);
        }

        private void RefreshActionButtonVisuals(bool animate)
        {
            if (ApplyButton == null || SaveButton == null)
            {
                return;
            }

            EnsureMutableActionButtonBrushes();
            var neutralBackground = GetThemeColor("SettingsControlBackgroundBrush", Colors.Transparent);
            var neutralBorder = GetThemeColor("SettingsControlBorderBrush", Colors.Gray);
            var neutralForeground = GetThemeColor("SettingsControlForegroundBrush", Colors.White);
            var accent = Color.FromRgb(22, 119, 255);

            AnimateBrush((SolidColorBrush)ApplyButton.Background, neutralBackground, animate);
            AnimateBrush((SolidColorBrush)ApplyButton.BorderBrush, settingsDirty ? accent : neutralBorder, animate);
            AnimateBrush((SolidColorBrush)ApplyButton.Foreground, neutralForeground, animate);
            AnimateBrush((SolidColorBrush)SaveButton.Background, settingsDirty ? accent : neutralBackground, animate);
            AnimateBrush((SolidColorBrush)SaveButton.BorderBrush, settingsDirty ? accent : neutralBorder, animate);
            AnimateBrush((SolidColorBrush)SaveButton.Foreground, settingsDirty ? Colors.White : neutralForeground, animate);
        }

        private void EnsureMutableActionButtonBrushes()
        {
            EnsureMutableBrush(ApplyButton, Control.BackgroundProperty);
            EnsureMutableBrush(ApplyButton, Control.BorderBrushProperty);
            EnsureMutableBrush(ApplyButton, Control.ForegroundProperty);
            EnsureMutableBrush(SaveButton, Control.BackgroundProperty);
            EnsureMutableBrush(SaveButton, Control.BorderBrushProperty);
            EnsureMutableBrush(SaveButton, Control.ForegroundProperty);
        }

        private static void EnsureMutableBrush(Control control, DependencyProperty property)
        {
            var brush = control.GetValue(property) as SolidColorBrush;
            if (brush == null || brush.IsFrozen)
            {
                control.SetValue(property, new SolidColorBrush(brush?.Color ?? Colors.Transparent));
            }
        }

        private Color GetThemeColor(string resourceKey, Color fallback)
        {
            return (TryFindResource(resourceKey) as SolidColorBrush)?.Color ?? fallback;
        }

        private static void AnimateBrush(SolidColorBrush brush, Color target, bool animate)
        {
            var current = brush.Color;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = current;
            if (!animate)
            {
                brush.Color = target;
                return;
            }

            brush.BeginAnimation(
                SolidColorBrush.ColorProperty,
                new ColorAnimation(current, target, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                },
                HandoffBehavior.SnapshotAndReplace);
        }

        private void SubscribeToSystemThemeChanges()
        {
            if (systemThemeEventsSubscribed)
            {
                return;
            }

            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            systemThemeEventsSubscribed = true;
        }

        private void UnsubscribeFromSystemThemeChanges()
        {
            if (!systemThemeEventsSubscribed)
            {
                return;
            }

            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            systemThemeEventsSubscribed = false;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (selectedThemePreference == SettingsThemePreference.System)
            {
                Dispatcher.BeginInvoke(new Action(ApplySettingsTheme));
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (settingsDirty)
            {
                ApplyCurrentSettings();
            }

            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (settingsDirty)
            {
                ApplyCurrentSettings();
            }
        }

        private void CenterIslandButton_Click(object sender, RoutedEventArgs e)
        {
            OffsetSlider.Value = 50;
        }

        private void ResetHoverDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            HoverAuraSizeSlider.Value = OverlayPlacementSettings.DefaultHoverAuraSize;
            HoverDetectionRangeSlider.Value = OverlayPlacementSettings.DefaultHoverDetectionRange;
            HoverAuraAspectRatioSlider.Value = OverlayPlacementSettings.DefaultHoverAuraAspectRatio;
            SetHoverSpectrumControls(OverlayPlacementSettings.CreateDefaultHoverSpectrumStops());
            PassThroughOnHoverCheckBox.IsChecked = OverlayPlacementSettings.DefaultPassThroughOnHover;
            UpdateSettingValueLabels();
            QueueDirtyStateUpdate();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyCurrentSettings()
        {
            var settings = CaptureSettings();
            SaveLayoutEditingIfActive();
            applySettings(settings);
            workingSettings = settings.DeepClone();
            acceptedCacheLimitMegabytes = settings.CacheLimitMegabytes;
            dirtyStateTracker.Accept(settings);
            settingsDirty = false;
            RefreshActionButtonVisuals(true);

            if (LayoutSettingsPanel.Visibility == Visibility.Visible && beginLayoutEditing != null)
            {
                layoutEditingActive = true;
                beginLayoutEditing(ReadEditedLayoutMode(), false);
            }
        }

        private OverlayPlacementSettings CaptureSettings()
        {
            var settings = workingSettings?.DeepClone() ?? new OverlayPlacementSettings();
            settings.IslandLayouts = settings.IslandLayouts ?? IslandLayoutDefaults.Create();
            settings.ScreenName = ScreenComboBox.SelectedValue as string ?? screens.FirstOrDefault()?.Name ?? string.Empty;
            settings.Edge = OverlayDockEdge.Top;
            settings.OffsetRatio = OffsetSlider.Value / 100.0;
            settings.CacheLimitMegabytes = ReadCacheLimitMegabytes();
            settings.NoPlaybackAutoRetractSeconds = (int)Math.Round(NoPlaybackAutoRetractSlider.Value);
            settings.ExpandedAutoCollapseSeconds = (int)Math.Round(ExpandedAutoCollapseSlider.Value);
            settings.HoverAuraSize = (int)Math.Round(HoverAuraSizeSlider.Value);
            settings.HoverDetectionRange = (int)Math.Round(HoverDetectionRangeSlider.Value);
            settings.HoverAuraAspectRatio = HoverAuraAspectRatioSlider.Value;
            settings.HoverTransparencyPercent = (int)Math.Round(SpectrumCenterTransparencySlider.Value);
            settings.HoverSpectrumStops = ReadHoverSpectrumStops();
            settings.PassThroughOnHover = PassThroughOnHoverCheckBox.IsChecked == true;
            settings.SettingsTheme = selectedThemePreference;
            settings.LyricsSource = ReadLyricsSource();
            settings.LockedSourceAppUserModelId = PlayerSelectionComboBox.SelectedValue as string ?? string.Empty;
            settings.UseMultiLineDisplay = ReadUseMultiLineDisplay();
            settings.ShowTranslation = ShowTranslationCheckBox.IsChecked == true;
            settings.TaskbarLyricsEnabled = TaskbarLyricsEnabledCheckBox.IsChecked == true;
            settings.TaskbarLyricsAlignment = TaskbarLyricsAlignmentComboBox.SelectedIndex == 1 ? TaskbarLyricsAlignment.Left : TaskbarLyricsAlignment.Center;
            settings.LyricOffsetHotkeys = new HotkeySettings
            {
                Earlier = EarlierHotkeyTextBox.Text,
                Later = LaterHotkeyTextBox.Text,
                Reset = ResetHotkeyTextBox.Text,
                TemporaryInteraction = TemporaryInteractionHotkeyTextBox.Text
            };
            settings.IslandLayouts.Mode = ReadEditedLayoutMode();
            ApplyLyricsWidth(settings, LyricsWidthSlider.Value);
            ApplyDividerSettings(settings, ReadEditedLayoutMode(), DividerOpacitySlider.Value, DividerSpacingSlider.Value);
            if (layoutEditingActive && getLayoutDraftSnapshot != null)
            {
                var draft = getLayoutDraftSnapshot(ReadEditedLayoutMode());
                if (draft != null)
                {
                    SetLayoutProfile(settings, ReadEditedLayoutMode(), draft);
                }
            }

            settings.Normalize();
            return settings;
        }

        private static string CreateSettingsFingerprint(OverlayPlacementSettings settings)
        {
            return JsonSerializer.Serialize(settings ?? new OverlayPlacementSettings());
        }

        private static void SetLayoutProfile(
            OverlayPlacementSettings settings,
            IslandLayoutMode mode,
            IslandLayoutProfile profile)
        {
            if (settings?.IslandLayouts == null || profile == null)
            {
                return;
            }

            if (mode == IslandLayoutMode.HorizontalBlocks)
            {
                settings.IslandLayouts.Horizontal = profile;
            }
            else
            {
                settings.IslandLayouts.CompactExpanded = profile;
            }
        }

        private void InitializeLayoutEditingControls(OverlayPlacementSettings settings)
        {
            ModuleToolbox.ItemsSource = ModuleToolboxCatalog.All;
            suppressLayoutSelectionChanged = true;
            selectedLayoutMode = settings.IslandLayouts.Mode;
            LyricsWidthSlider.Value = GetLyricsWidth(settings);
            LoadDividerControls(settings.IslandLayouts.Mode);
            suppressLayoutSelectionChanged = false;
            UpdateLayoutModePreviewSelection();
        }

        private void InitializePlayerSelection(OverlayPlacementSettings settings)
        {
            var playerOptions = new Dictionary<string, PlayerSelectionOption>(StringComparer.OrdinalIgnoreCase)
            {
                [string.Empty] = new PlayerSelectionOption(string.Empty, "自动选择", true)
            };

            foreach (var installed in installedPlayers.OrderBy(player => player.DisplayName))
            {
                playerOptions[installed.SelectionKey] = new PlayerSelectionOption(
                    installed.SelectionKey,
                    installed.DisplayName,
                    true);
            }

            foreach (var session in playerSessions
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.SourceAppUserModelId)))
            {
                var profile = PlayerProfileCatalog.Resolve(session.SourceAppUserModelId);
                var selectionKey = profile.Kind == PlayerKind.Generic
                    ? session.SourceAppUserModelId
                    : PlayerProfileCatalog.GetSelectionKey(profile.Kind);
                var displayName = string.IsNullOrWhiteSpace(session.PlayerDisplayName)
                    ? profile.DisplayName
                    : session.PlayerDisplayName;
                playerOptions[selectionKey] = new PlayerSelectionOption(selectionKey, displayName, true);
            }

            var selectedValue = NormalizePlayerSelection(settings.LockedSourceAppUserModelId);
            if (!playerOptions.ContainsKey(selectedValue) && !string.IsNullOrWhiteSpace(selectedValue))
            {
                PlayerProfile selectedProfile;
                var displayName = PlayerProfileCatalog.TryResolveSelectionKey(selectedValue, out selectedProfile)
                    ? selectedProfile.DisplayName
                    : selectedValue;
                playerOptions[selectedValue] = new PlayerSelectionOption(selectedValue, displayName, false);
            }

            var options = playerOptions.Values
                .OrderBy(option => string.IsNullOrEmpty(option.Value) ? 0 : 1)
                .ThenByDescending(option => option.IsDetected)
                .ThenBy(option => option.DisplayName)
                .ToList();
            suppressPlayerSelectionChanged = true;
            PlayerSelectionComboBox.ItemsSource = options;
            PlayerSelectionComboBox.SelectedValue = options.Any(option =>
                string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase))
                ? selectedValue
                : string.Empty;
            suppressPlayerSelectionChanged = false;
            UpdatePlayerSelectionHint();
        }

        private void PlayerSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!suppressPlayerSelectionChanged)
            {
                if (workingSettings != null)
                {
                    workingSettings.LockedSourceAppUserModelId = PlayerSelectionComboBox.SelectedValue as string
                        ?? string.Empty;
                }

                QueueDirtyStateUpdate();
            }

            UpdatePlayerSelectionHint();
        }

        private void UpdatePlayerSelectionHint()
        {
            if (PlayerSelectionHintText == null || PlayerSelectionComboBox == null)
            {
                return;
            }

            var option = PlayerSelectionComboBox.SelectedItem as PlayerSelectionOption;
            string selectionHint;
            if (option == null || string.IsNullOrEmpty(option.Value))
            {
                selectionHint = "自动选择会跟随最近活跃的播放器";
            }
            else if (!option.IsDetected)
            {
                selectionHint = "未检测到，启动播放器后生效";
            }
            else
            {
                selectionHint = "优先选择 " + option.DisplayName;
            }

            PlayerSelectionHintText.Text = selectionHint + Environment.NewLine +
                "注：网易云音乐由于接口限制无法实时同步歌曲进度（播放器内拖动进度条无法同步）";
        }

        private static string NormalizePlayerSelection(string value)
        {
            value = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("player:", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            var profile = PlayerProfileCatalog.Resolve(value);
            return profile.Kind == PlayerKind.Generic
                ? value
                : PlayerProfileCatalog.GetSelectionKey(profile.Kind);
        }

        private IslandLayoutMode ReadEditedLayoutMode()
        {
            return selectedLayoutMode;
        }

        private static double GetLyricsWidth(OverlayPlacementSettings settings)
        {
            var profile = settings?.IslandLayouts?.Horizontal;
            var lyrics = profile?.Modules?.FirstOrDefault(module => module.Type == IslandModuleType.Lyrics);
            return IslandModuleInstance.NormalizeLyricsWidth(lyrics?.LyricsWidth ?? IslandModuleInstance.DefaultLyricsWidth);
        }

        private static void ApplyLyricsWidth(OverlayPlacementSettings settings, double width)
        {
            if (settings?.IslandLayouts == null)
            {
                return;
            }

            var normalized = IslandModuleInstance.NormalizeLyricsWidth(width);
            ApplyLyricsWidth(settings.IslandLayouts.Horizontal, normalized);
            ApplyLyricsWidth(settings.IslandLayouts.CompactCollapsed, normalized);
            ApplyLyricsWidth(settings.IslandLayouts.CompactExpanded, normalized);
        }

        private static void ApplyLyricsWidth(IslandLayoutProfile profile, double width)
        {
            if (profile?.Modules == null)
            {
                return;
            }

            foreach (var module in profile.Modules.Where(module => module.Type == IslandModuleType.Lyrics))
            {
                module.LyricsWidth = width;
            }
        }

        private static IslandLayoutProfile GetLayoutProfile(OverlayPlacementSettings settings, IslandLayoutMode mode)
        {
            if (settings?.IslandLayouts == null)
            {
                return null;
            }

            return mode == IslandLayoutMode.HorizontalBlocks
                ? settings.IslandLayouts.Horizontal
                : settings.IslandLayouts.CompactExpanded;
        }

        private static void GetDividerSettings(OverlayPlacementSettings settings, IslandLayoutMode mode, out double opacity, out double spacing)
        {
            var divider = GetLayoutProfile(settings, mode)?.Modules?
                .FirstOrDefault(module => module.Type == IslandModuleType.Divider);
            opacity = divider?.DividerOpacity ?? 0.22;
            spacing = divider == null ? 4 : (divider.MarginBefore + divider.MarginAfter) / 2.0;
        }

        private static void ApplyDividerSettings(OverlayPlacementSettings settings, IslandLayoutMode mode, double opacity, double spacing)
        {
            var profile = GetLayoutProfile(settings, mode);
            if (profile?.Modules == null)
            {
                return;
            }

            var normalizedOpacity = Math.Max(0, Math.Min(1, opacity));
            var normalizedSpacing = Math.Max(0, Math.Min(64, spacing));
            foreach (var divider in profile.Modules.Where(module => module.Type == IslandModuleType.Divider))
            {
                divider.DividerOpacity = normalizedOpacity;
                divider.MarginBefore = normalizedSpacing;
                divider.MarginAfter = normalizedSpacing;
            }
        }

        private static void RemoveDividers(OverlayPlacementSettings settings, IslandLayoutMode mode)
        {
            var modules = GetLayoutProfile(settings, mode)?.Modules;
            modules?.RemoveAll(module => module.Type == IslandModuleType.Divider);
        }

        private void LoadDividerControls(IslandLayoutMode mode)
        {
            double opacity;
            double spacing;
            GetDividerSettings(workingSettings, mode, out opacity, out spacing);
            DividerOpacitySlider.Value = opacity;
            DividerSpacingSlider.Value = spacing;
        }

        private void HorizontalLayoutPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            SelectLayoutMode(IslandLayoutMode.HorizontalBlocks);
        }

        private void ExpandableLayoutPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            SelectLayoutMode(IslandLayoutMode.Expandable);
        }

        private void SelectLayoutMode(IslandLayoutMode mode)
        {
            var changed = selectedLayoutMode != mode;
            selectedLayoutMode = mode;
            if (workingSettings?.IslandLayouts != null)
            {
                workingSettings.IslandLayouts.Mode = mode;
            }

            UpdateLayoutModePreviewSelection();
            if (!changed)
            {
                return;
            }

            if (suppressLayoutSelectionChanged || beginLayoutEditing == null)
            {
                QueueDirtyStateUpdate();
                return;
            }

            suppressLayoutSelectionChanged = true;
            LyricsWidthSlider.Value = GetLyricsWidth(workingSettings);
            LoadDividerControls(mode);
            suppressLayoutSelectionChanged = false;
            layoutEditingActive = true;
            beginLayoutEditing(mode, false);
            QueueDirtyStateUpdate();
        }

        private void ResetLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            workingSettings.IslandLayouts = IslandLayoutDefaults.Create();
            workingSettings.IslandLayouts.Mode = ReadEditedLayoutMode();
            LyricsWidthSlider.Value = GetLyricsWidth(workingSettings);
            LoadDividerControls(ReadEditedLayoutMode());
            beginLayoutEditing?.Invoke(ReadEditedLayoutMode(), true);
            layoutEditingActive = beginLayoutEditing != null;
            QueueDirtyStateUpdate();
        }

        private void SaveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            SaveLayoutEditingIfActive();
        }

        private void CancelLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            CancelLayoutEditing();
        }

        private void RemoveSelectedModule_Click(object sender, RoutedEventArgs e)
        {
            var mode = ReadEditedLayoutMode();
            RemoveDividers(workingSettings, mode);
            removeDividers?.Invoke(mode);
            QueueDirtyStateUpdate();
        }

        private void ModuleToolbox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (moduleToolboxDragInProgress)
            {
                e.Handled = true;
                return;
            }

            moduleToolboxDragStartPoint = e.GetPosition(ModuleToolbox);
            moduleToolboxDragOption = FindModuleToolboxOption(e.OriginalSource as DependencyObject);
            ModuleToolbox.CaptureMouse();
            e.Handled = true;
        }

        private void ModuleToolbox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (moduleToolboxDragInProgress)
            {
                e.Handled = true;
                return;
            }

            moduleToolboxDragStartPoint = null;
            moduleToolboxDragOption = null;
            if (Mouse.Captured == ModuleToolbox)
            {
                ModuleToolbox.ReleaseMouseCapture();
            }
        }

        private void ModuleToolbox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (moduleToolboxDragInProgress)
            {
                e.Handled = true;
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                moduleToolboxDragStartPoint = null;
                moduleToolboxDragOption = null;
                if (Mouse.Captured == ModuleToolbox)
                {
                    ModuleToolbox.ReleaseMouseCapture();
                }
                return;
            }

            if (!moduleToolboxDragStartPoint.HasValue)
            {
                return;
            }

            var currentPoint = e.GetPosition(ModuleToolbox);
            if (Math.Abs(currentPoint.X - moduleToolboxDragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - moduleToolboxDragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var option = moduleToolboxDragOption ?? FindModuleToolboxOption(e.OriginalSource as DependencyObject);
            if (option != null)
            {
                var payload = new IslandLayoutDragPayload { NewType = option.Value };
                moduleToolboxDragInProgress = true;
                moduleToolboxDragStartPoint = null;
                moduleToolboxDragOption = null;
                if (Mouse.Captured == ModuleToolbox)
                {
                    ModuleToolbox.ReleaseMouseCapture();
                }
                setModuleDragActive?.Invoke(true);
                ShowModuleDragGhost(option);
                try
                {
                    DragDrop.DoDragDrop(ModuleToolbox, IslandLayoutDragPayload.CreateDataObject(payload), DragDropEffects.Copy);
                }
                finally
                {
                    moduleDragGhost?.Close();
                    moduleDragGhost = null;
                    setModuleDragActive?.Invoke(false);
                    moduleToolboxDragInProgress = false;
                }
                e.Handled = true;
                return;
            }

        }

        private void ModuleDrag_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            UpdateModuleDragGhostPosition();
            Mouse.SetCursor(LayoutDragCursors.ClosedHand);
            e.UseDefaultCursors = false;
            e.Handled = true;
        }

        private void ShowModuleDragGhost(ModuleToolboxItemDescriptor option)
        {
            moduleDragGhost?.Close();
            moduleDragGhost = new ModuleDragGhostWindow(option, Resources);
            moduleDragGhost.Show();
            UpdateModuleDragGhostPosition();
        }

        private void UpdateModuleDragGhostPosition()
        {
            moduleDragGhost?.UpdatePosition();
        }

        private ModuleToolboxItemDescriptor FindModuleToolboxOption(DependencyObject source)
        {
            var container = ItemsControl.ContainerFromElement(ModuleToolbox, source) as FrameworkElement;
            var option = container?.DataContext as ModuleToolboxItemDescriptor;
            while (option == null && source != null)
            {
                var element = source as FrameworkElement;
                option = element?.DataContext as ModuleToolboxItemDescriptor;
                source = VisualTreeHelper.GetParent(source);
            }

            return option;
        }

        private void SaveLayoutEditingIfActive()
        {
            if (!layoutEditingActive)
            {
                return;
            }

            saveLayoutEditing?.Invoke();
            layoutEditingActive = false;
        }

        private void CancelLayoutEditing()
        {
            cancelLayoutEditing?.Invoke();
            layoutEditingActive = false;
        }

        private int ReadCacheLimitMegabytes()
        {
            int value;
            if (!int.TryParse(CacheLimitTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                value = acceptedCacheLimitMegabytes;
            }

            return Math.Max(OverlayPlacementSettings.MinCacheLimitMegabytes, Math.Min(OverlayPlacementSettings.MaxCacheLimitMegabytes, value));
        }

        private void SettingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ReferenceEquals(sender, LyricsWidthSlider))
            {
                ApplyLyricsWidth(workingSettings, LyricsWidthSlider.Value);
                updateLyricsWidth?.Invoke(ReadEditedLayoutMode(), LyricsWidthSlider.Value);
            }
            else if (!suppressLayoutSelectionChanged &&
                     (ReferenceEquals(sender, DividerOpacitySlider) || ReferenceEquals(sender, DividerSpacingSlider)))
            {
                var mode = ReadEditedLayoutMode();
                ApplyDividerSettings(workingSettings, mode, DividerOpacitySlider.Value, DividerSpacingSlider.Value);
                updateDividerSettings?.Invoke(mode, DividerOpacitySlider.Value, DividerSpacingSlider.Value);
            }
            UpdateSettingValueLabels();
        }

        private void TaskbarLyricsEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (initializingSettings || suppressTaskbarLyricsConfirmation || workingSettings?.TaskbarLyricsEnabled == true)
            {
                return;
            }

            var answer = MessageBox.Show(
                "任务栏歌词会临时隐藏当前用户的 Windows Widgets（小组件），这是全局任务栏设置。关闭任务栏歌词、正常退出或下次启动恢复时会还原原状态。是否继续？",
                "确认开启任务栏歌词",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer == MessageBoxResult.Yes)
            {
                return;
            }

            suppressTaskbarLyricsConfirmation = true;
            TaskbarLyricsEnabledCheckBox.IsChecked = false;
            suppressTaskbarLyricsConfirmation = false;
        }

        private void ShowTranslationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTranslationLineModeLock();
        }

        private void SingleLineRadioButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ShowTranslationCheckBox.IsChecked != true)
            {
                return;
            }

            MultiLineRadioButton.IsChecked = true;
            ShowTranslationModeToast();
            e.Handled = true;
        }

        private void LineModeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateLineModeSelection(!initializingSettings);
        }

        private void UpdateSegmentSelectionPositions(bool animated)
        {
            UpdateLineModeSelection(animated);
            UpdateThemeSelection(animated);
        }

        private void UpdateLineModeSelection(bool animated)
        {
            if (LineModeSelectionTransform == null || MultiLineRadioButton == null)
            {
                return;
            }

            AnimateSegmentSelection(
                LineModeSelectionTransform,
                MultiLineRadioButton.IsChecked == true ? 92 : 0,
                animated);
        }

        private void UpdateThemeSelection(bool animated)
        {
            if (ThemeSelectionTransform == null || DarkThemeRadioButton == null || SystemThemeRadioButton == null)
            {
                return;
            }

            var target = DarkThemeRadioButton.IsChecked == true
                ? 42
                : SystemThemeRadioButton.IsChecked == true
                    ? 84
                    : 0;
            AnimateSegmentSelection(ThemeSelectionTransform, target, animated);
        }

        private void AnimateSegmentSelection(TranslateTransform transform, double target, bool animated)
        {
            var current = transform.X;
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = target;
            if (!animated || !IsLoaded || Math.Abs(current - target) < 0.5)
            {
                return;
            }

            transform.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
        }

        private void ShowTranslationModeToast()
        {
            TranslationModeToast.BeginAnimation(OpacityProperty, null);
            TranslationModeToast.Visibility = Visibility.Visible;
            TranslationModeToast.Opacity = 0;
            TranslationModeToast.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            if (translationModeToastTimer == null)
            {
                translationModeToastTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(2100)
                };
                translationModeToastTimer.Tick += (sender, args) =>
                {
                    translationModeToastTimer.Stop();
                    FadeOutTranslationModeToast();
                };
            }

            translationModeToastTimer.Stop();
            translationModeToastTimer.Start();
        }

        private void FadeOutTranslationModeToast()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320))
            {
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.Completed += (sender, args) =>
            {
                TranslationModeToast.BeginAnimation(OpacityProperty, null);
                TranslationModeToast.Opacity = 0;
                TranslationModeToast.Visibility = Visibility.Collapsed;
            };
            TranslationModeToast.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void HotkeyTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            (sender as TextBox)?.SelectAll();
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null)
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;
            if (key == Key.LeftCtrl || key == Key.RightCtrl)
            {
                modifiers |= ModifierKeys.Control;
            }
            else if (key == Key.LeftAlt || key == Key.RightAlt)
            {
                modifiers |= ModifierKeys.Alt;
            }
            else if (key == Key.LeftShift || key == Key.RightShift)
            {
                modifiers |= ModifierKeys.Shift;
            }
            else if (key == Key.LWin || key == Key.RWin)
            {
                modifiers |= ModifierKeys.Windows;
            }

            var parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            if (!IsModifierKey(key))
            {
                parts.Add(FormatHotkeyKey(key));
            }

            if (parts.Count > 0)
            {
                textBox.Text = string.Join("+", parts);
                textBox.CaretIndex = textBox.Text.Length;
            }

            e.Handled = true;
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin;
        }

        private static string FormatHotkeyKey(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture);
            }

            return key.ToString();
        }

        private void SectionButton_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;
            var section = button?.Tag as string ?? "Lyrics";
            ShowSection(section);
            tutorialSectionChanged?.Invoke(section);
        }

        private void SupportStoreReviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-windows-store://review/?ProductId=" + MicrosoftStoreProductId,
                    UseShellExecute = true
                });
                SetSupportStatus("已打开 Microsoft Store，可在同一页面评分并撰写评价。", false);
            }
            catch (Exception)
            {
                SetSupportStatus("暂时无法打开 Microsoft Store，请稍后重试。", true);
            }
        }

        private void SupportShareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("LyricHover LYRIC HOVER · Windows 桌面歌词伴侣\n" + MicrosoftStoreProductUrl);
                SetSupportStatus("Microsoft Store 应用链接已复制，可以分享给朋友了。", false);
            }
            catch (Exception)
            {
                SetSupportStatus("暂时无法写入剪贴板，请稍后重试。", true);
            }
        }

        private void SupportFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => OpenExternalUrl(FeedbackUrl)));
        }

        private async void SupportProPurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (proEntitlementKind != ProEntitlementKind.None)
            {
                if (supporterBadgeIdentity == null)
                {
                    FocusSupporterIdentityInput();
                    return;
                }

                OpenSupporterBadgePreview();
                return;
            }

            SupportProPurchaseButton.IsEnabled = false;
            SetSupportStatus("正在连接 Microsoft Store…", false);

            try
            {
                var storeContext = StoreContext.GetDefault();
                var initializeWithWindow = (IInitializeWithWindow)(object)storeContext;
                initializeWithWindow.Initialize(new WindowInteropHelper(this).Handle);

                var products = await storeContext.GetAssociatedStoreProductsAsync(new[] { "Durable" });
                if (products.ExtendedError != null)
                {
                    SetSupportStatus("暂时无法读取 Pro 商品，请确认应用已从 Microsoft Store 安装。", true);
                    return;
                }

                var proProduct = products.Products.Values.FirstOrDefault(product =>
                    string.Equals(product.InAppOfferToken, MicrosoftStoreProProductId, StringComparison.OrdinalIgnoreCase));
                if (proProduct == null)
                {
                    SetSupportStatus("Pro 商品尚未在 Microsoft Store 中可用，请稍后重试。", true);
                    return;
                }

                if (proProduct.IsInUserCollection)
                {
                    await RefreshProEntitlementAsync();
                    if (proEntitlementKind == ProEntitlementKind.StorePro)
                    {
                        SetSupportStatus("当前 Microsoft 账号已经拥有 LYRIC HOVER Pro。", false);
                    }
                    else
                    {
                        SetSupportStatus("Microsoft Store 已返回购买记录，但暂时无法验证 Pro 权益。", true);
                    }
                    return;
                }

                var purchase = await proProduct.RequestPurchaseAsync();
                switch (purchase.Status)
                {
                    case StorePurchaseStatus.Succeeded:
                        await RefreshProEntitlementAsync();
                        if (proEntitlementKind == ProEntitlementKind.StorePro)
                        {
                            SetSupportStatus("购买成功，感谢您支持 LYRIC HOVER！", false);
                        }
                        else
                        {
                            SetSupportStatus("购买已完成，但暂时无法验证 Pro 权益，请稍后重新打开此页面。", true);
                        }
                        break;
                    case StorePurchaseStatus.AlreadyPurchased:
                        await RefreshProEntitlementAsync();
                        if (proEntitlementKind == ProEntitlementKind.StorePro)
                        {
                            SetSupportStatus("当前 Microsoft 账号已经拥有 LYRIC HOVER Pro。", false);
                        }
                        else
                        {
                            SetSupportStatus("Microsoft Store 已返回购买记录，但暂时无法验证 Pro 权益。", true);
                        }
                        break;
                    case StorePurchaseStatus.NotPurchased:
                        SetSupportStatus("购买已取消。", false);
                        break;
                    case StorePurchaseStatus.NetworkError:
                        SetSupportStatus("网络连接异常，暂时无法完成购买。", true);
                        break;
                    case StorePurchaseStatus.ServerError:
                    default:
                        SetSupportStatus("Microsoft Store 暂时无法完成购买，请稍后重试。", true);
                        break;
                }
            }
            catch (Exception)
            {
                SetSupportStatus("暂时无法打开 Pro 购买窗口，请使用 Microsoft Store 安装版重试。", true);
            }
            finally
            {
                SupportProPurchaseButton.IsEnabled = !proEntitlementRefreshInProgress;
            }
        }

        private async Task RefreshProEntitlementAsync()
        {
            if (proEntitlementRefreshInProgress)
            {
                return;
            }

            proEntitlementRefreshInProgress = true;
            SupportProPurchaseButton.IsEnabled = false;
            SetSupportStatus("正在验证 Pro 状态…", false);
            try
            {
                var result = await proEntitlementService.RefreshAsync();
                proEntitlementAcquiredAt = result.AcquiredAtUtc;
                ApplyProEntitlementState(result.Kind);
                if (!result.StoreQuerySucceeded && !result.UsedCache)
                {
                    SetSupportStatus("暂时无法验证 Pro 状态，请检查网络和 Microsoft Store 登录状态。", true);
                }
                else if (result.UsedCache)
                {
                    SetSupportStatus("当前为上次成功验证的 Pro 状态，将在联网后自动更新。", false);
                }
                else
                {
                    ClearSupportStatus();
                }
            }
            finally
            {
                proEntitlementRefreshInProgress = false;
                SupportProPurchaseButton.IsEnabled = true;
            }
        }

        private void ApplyProEntitlementState(ProEntitlementKind kind)
        {
            proEntitlementKind = kind;
            var presentation = ProEntitlementPresentation.For(kind);
            SupportProTitleText.Text = presentation.Title;
            SupportProDescriptionText.Text = presentation.Description;
            SupportProButtonText.Text = presentation.ButtonText;
            SupportProButtonIcon.Data = Geometry.Parse(
                presentation.UseBadgeIcon ? BadgeIconGeometry : PurchaseIconGeometry);
            UpdateSupporterIdentityControls();
        }

        private void OpenSupporterBadgePreview()
        {
            if (!TryCommitSupporterBadgeIdentity())
            {
                return;
            }

            supporterBadgePreviewWindow = new SupporterBadgePreviewWindow(
                new SupporterBadgeOptions
                {
                    Identity = supporterBadgeIdentity,
                    AutoRotate = true,
                    InitialSide = SupporterBadgeInitialSide.Front,
                    Size = SupporterBadgeSize.Large
                })
            {
                Owner = this
            };

            try
            {
                supporterBadgePreviewWindow.ShowDialog();
            }
            finally
            {
                supporterBadgePreviewWindow = null;
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new Action(() => SupportProPurchaseButton.Focus()));
            }
        }

        private bool TryCommitSupporterBadgeIdentity()
        {
            if (supporterBadgeIdentity != null)
            {
                return true;
            }

            if (proEntitlementKind == ProEntitlementKind.None ||
                !proEntitlementAcquiredAt.HasValue)
            {
                SetSupportStatus("请先完成 Microsoft Store Pro 权益验证，再提交徽章署名。", true);
                return false;
            }

            var displayName = SupporterNicknameTextBox.Text;
            try
            {
                displayName = SupporterBadgeIdentityStore.SanitizeDisplayName(displayName);
                if (displayName.Length < SupporterBadgeIdentityStore.MinimumDisplayNameLength)
                {
                    SetSupportStatus("徽章署名至少需要 2 个字符。", true);
                    return false;
                }

                var confirmation = MessageBox.Show(
                    "署名将与 Microsoft Store 获取日期一起刻印在支持者徽章背面。提交后不可修改，是否确认？",
                    "确认并永久刻印",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (confirmation != MessageBoxResult.Yes)
                {
                    return false;
                }

                supporterBadgeIdentity = supporterBadgeIdentityStore.Commit(
                    displayName,
                    proEntitlementAcquiredAt.Value);
                UpdateSupporterIdentityControls();
                SetSupportStatus("徽章署名已永久刻印。", false);
                return true;
            }
            catch (InvalidOperationException)
            {
                supporterBadgeIdentity = supporterBadgeIdentityStore.Load();
                UpdateSupporterIdentityControls();
                return supporterBadgeIdentity != null;
            }
            catch (ArgumentException)
            {
                SetSupportStatus("署名只支持中英文、数字、空格、连字符和下划线，长度为 2–18 个字符。", true);
                return false;
            }
            catch (System.IO.IOException)
            {
                SetSupportStatus("暂时无法保存徽章署名，请稍后重试。", true);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                SetSupportStatus("暂时无法保存徽章署名，请稍后重试。", true);
                return false;
            }
        }

        private void UpdateSupporterIdentityControls()
        {
            if (SupporterNicknameTextBox == null ||
                SupporterIdentityInputHost == null ||
                SupportProPurchaseButton == null)
            {
                return;
            }

            var committed = supporterBadgeIdentity != null;
            var needsIdentity = !committed && proEntitlementKind != ProEntitlementKind.None;
            SupporterNicknameTextBox.Text = committed
                ? supporterBadgeIdentity.DisplayName
                : SupporterNicknameTextBox.Text;
            SupporterNicknameTextBox.IsReadOnly = committed;
            SupporterIdentityInputHost.Visibility = needsIdentity
                ? Visibility.Visible
                : Visibility.Collapsed;
            SupportProPurchaseButton.Visibility = needsIdentity
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateSupporterIdentityPlaceholder();

            if (needsIdentity)
            {
                FocusSupporterIdentityInput();
            }
        }

        private void FocusSupporterIdentityInput()
        {
            if (SupporterNicknameTextBox == null ||
                SupporterNicknameTextBox.Visibility != Visibility.Visible)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    SupporterNicknameTextBox.Focus();
                    SupporterNicknameTextBox.SelectAll();
                }));
        }

        private void SupporterNicknameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            TryCommitSupporterBadgeIdentity();
        }

        private void SupporterNicknameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSupporterIdentityPlaceholder();
        }

        private void UpdateSupporterIdentityPlaceholder()
        {
            if (SupporterNicknamePlaceholderText == null ||
                SupporterNicknameTextBox == null)
            {
                return;
            }

            SupporterNicknamePlaceholderText.Visibility = string.IsNullOrWhiteSpace(SupporterNicknameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ClearSupportStatus()
        {
            SupportStatusText.BeginAnimation(OpacityProperty, null);
            SupportStatusText.Text = string.Empty;
            SupportStatusText.Opacity = 1;
        }

        private void SetSupportStatus(string message, bool isError)
        {
            if (SupportStatusText == null)
            {
                return;
            }

            SupportStatusText.BeginAnimation(OpacityProperty, null);
            SupportStatusText.Opacity = 1;
            SupportStatusText.Text = message;
            SupportStatusText.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(190, 58, 58))
                : (Brush)FindResource("SettingsControlMutedForegroundBrush");

            var fadeAnimation = new DoubleAnimation
            {
                BeginTime = TimeSpan.FromSeconds(3),
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(280),
                FillBehavior = FillBehavior.HoldEnd
            };
            SupportStatusText.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        private void ThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;
            if (button == null || button.Tag == null)
            {
                return;
            }

            SettingsThemePreference preference;
            if (Enum.TryParse(button.Tag.ToString(), out preference))
            {
                selectedThemePreference = preference;
                ApplySettingsTheme();
                UpdateThemeSelection(!initializingSettings);
            }
        }

        private void SetThemeRadioButton(SettingsThemePreference preference)
        {
            if (LightThemeRadioButton == null)
            {
                return;
            }

            LightThemeRadioButton.IsChecked = preference == SettingsThemePreference.Light;
            DarkThemeRadioButton.IsChecked = preference == SettingsThemePreference.Dark;
            SystemThemeRadioButton.IsChecked = preference == SettingsThemePreference.System;
        }

        private void ApplySettingsTheme()
        {
            if (RootChrome == null)
            {
                return;
            }

            var dark = ResolveDarkSettingsTheme(selectedThemePreference);
            UpdateThemeResources(dark);
            RootChrome.BorderBrush = Brushes.Transparent;
            SidebarCard.BorderBrush = Brushes.Transparent;
            ContentCard.Background = Brushes.Transparent;
            ContentCard.BorderBrush = Brushes.Transparent;
            if (dirtyStateTracker != null)
            {
                RefreshActionButtonVisuals(false);
            }
        }

        private void UpdateThemeResources(bool dark)
        {
            var animate = IsLoaded && !initializingSettings;
            var transitionVersion = ++themeTransitionVersion;
            SetBrushResource("SettingsRootBackgroundBrush", dark ? "#121318" : "#F4F3FA", animate, transitionVersion);
            SetBrushResource("SettingsSidebarBackgroundBrush", dark ? "#202126" : "#FBFBFD", animate, transitionVersion);
            SetBrushResource("SettingsThemeToggleBackgroundBrush", dark ? "#2B2C32" : "#ECECF2", animate, transitionVersion);
            SetBrushResource("SettingsControlBackgroundBrush", dark ? "#20252E" : "#F8FAFC", animate, transitionVersion);
            SetBrushResource("SettingsControlForegroundBrush", dark ? "#F3F4F6" : "#1F2937", animate, transitionVersion);
            SetBrushResource("SettingsControlMutedForegroundBrush", dark ? "#B4BDCA" : "#667085", animate, transitionVersion);
            SetBrushResource("SettingsControlBorderBrush", dark ? "#3A4250" : "#D8DEE8", animate, transitionVersion);
            SetBrushResource("SettingsControlHoverBackgroundBrush", dark ? "#28303B" : "#FFFFFF", animate, transitionVersion);
            SetBrushResource("SettingsControlPressedBackgroundBrush", dark ? "#303846" : "#EEF2F7", animate, transitionVersion);
            SetBrushResource("SettingsSelectedBackgroundBrush", dark ? "#2D3542" : "#FFFFFF", animate, transitionVersion);
            SetBrushResource("SettingsSelectedForegroundBrush", dark ? "#F8FAFC" : "#111827", animate, transitionVersion);
            SetBrushResource("SettingsSidebarHoverBackgroundBrush", dark ? "#2A2B31" : "#F0F0F4", animate, transitionVersion);
            SetBrushResource("SettingsSidebarSelectedBackgroundBrush", dark ? "#34353C" : "#E5E5EA", animate, transitionVersion);
            SetBrushResource("SettingsSidebarSelectedForegroundBrush", dark ? "#4B4C54" : "#D2D2D8", animate, transitionVersion);
            SetBrushResource("SettingsTrackBackgroundBrush", dark ? "#303846" : "#E6EAF0", animate, transitionVersion);
            SetBrushResource("SettingsToastBackgroundBrush", dark ? "#F020252E" : "#FFFFFFFF", animate, transitionVersion);
            SetBrushResource("SettingsToastBorderBrush", dark ? "#664F9CFF" : "#C9D7EC", animate, transitionVersion);
            SetBrushResource("SettingsToastForegroundBrush", dark ? "#FFFFFFFF" : "#243044", animate, transitionVersion);
            SetBrushResource("SettingsDragGhostBackgroundBrush", dark ? "#F020252E" : "#F2FFFFFF", animate, transitionVersion);
            SetBrushResource("SettingsDragGhostBorderBrush", dark ? "#884F9CFF" : "#7A8E9CAF", animate, transitionVersion);
            SetBrushResource("SettingsDragGhostForegroundBrush", dark ? "#FFF3F4F6" : "#FF1F2937", animate, transitionVersion);
            UpdateSupportProBadge(dark);
        }

        private void UpdateSupportProBadge(bool dark)
        {
            if (SupportProEmblem == null)
            {
                return;
            }

            var asset = dark
                ? "Assets/pro-support-badge-dark.png"
                : "Assets/pro-support-badge.png";
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri($"pack://application:,,,/{asset}", UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            SupportProEmblem.Source = image;
        }

        private void SetBrushResource(string key, string hex, bool animate, int transitionVersion)
        {
            var target = (Color)ColorConverter.ConvertFromString(hex);
            var brush = Resources[key] as SolidColorBrush;
            if (brush == null || brush.IsFrozen)
            {
                Resources[key] = new SolidColorBrush(target);
                return;
            }

            var current = brush.Color;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = current;
            if (!animate || current == target)
            {
                brush.Color = target;
                return;
            }

            var animation = new ColorAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            animation.Completed += (sender, args) =>
            {
                if (transitionVersion != themeTransitionVersion)
                {
                    return;
                }

                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.Color = target;
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static bool ResolveDarkSettingsTheme(SettingsThemePreference preference)
        {
            if (preference == SettingsThemePreference.Dark)
            {
                return true;
            }

            if (preference == SettingsThemePreference.Light)
            {
                return false;
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private void ApplyTextTheme(DependencyObject root, Brush primary, Brush muted)
        {
            foreach (var textBlock in FindVisualChildren<TextBlock>(root))
            {
                if (IsDescendantOf(HoverPreviewScene, textBlock) ||
                    IsDescendantOf(TranslationModeToast, textBlock))
                {
                    continue;
                }

                textBlock.Foreground = textBlock.FontSize <= 12.5 ? muted : primary;
            }

        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T)
                {
                    yield return (T)child;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject ancestor, DependencyObject child)
        {
            if (ancestor == null || child == null)
            {
                return false;
            }

            var current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void ShowSection(string section)
        {
            if (LyricsSettingsPanel == null ||
                PositionSettingsPanel == null ||
                CacheSettingsPanel == null ||
                HoverSettingsPanel == null ||
                HotkeySettingsPanel == null ||
                LayoutSettingsPanel == null ||
                SupportSettingsPanel == null ||
                AboutSettingsPanel == null)
            {
                return;
            }

            LyricsSettingsPanel.Visibility = section == "Lyrics" ? Visibility.Visible : Visibility.Collapsed;
            PositionSettingsPanel.Visibility = section == "Position" ? Visibility.Visible : Visibility.Collapsed;
            CacheSettingsPanel.Visibility = section == "Cache" ? Visibility.Visible : Visibility.Collapsed;
            HoverSettingsPanel.Visibility = section == "Hover" ? Visibility.Visible : Visibility.Collapsed;
            HotkeySettingsPanel.Visibility = section == "Hotkeys" ? Visibility.Visible : Visibility.Collapsed;
            LayoutSettingsPanel.Visibility = section == "Layout" ? Visibility.Visible : Visibility.Collapsed;
            SupportSettingsPanel.Visibility = section == "Support" ? Visibility.Visible : Visibility.Collapsed;
            AboutSettingsPanel.Visibility = section == "About" ? Visibility.Visible : Visibility.Collapsed;
            if (section == "Support")
            {
                _ = RefreshProEntitlementAsync();
            }
            UpdateLayoutModePreviewAnimation();
            if (section == "Layout" && beginLayoutEditing != null && !layoutEditingActive)
            {
                layoutEditingActive = true;
                beginLayoutEditing(ReadEditedLayoutMode(), false);
            }
        }

        public void PulseLayoutEditSettingsHighlight()
        {
            if (LayoutEditTutorialHighlight == null)
            {
                return;
            }

            LayoutEditTutorialHighlight.BeginAnimation(OpacityProperty, null);
            LayoutEditTutorialHighlight.Opacity = 0;
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(920),
                FillBehavior = FillBehavior.Stop
            };
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(
                0.72,
                KeyTime.FromPercent(0.28),
                new KeySpline(0.16, 1, 0.3, 1)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.72, KeyTime.FromPercent(0.48)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(
                0,
                KeyTime.FromPercent(1),
                new KeySpline(0.4, 0, 0.2, 1)));
            animation.Completed += (sender, args) => LayoutEditTutorialHighlight.Opacity = 0;
            LayoutEditTutorialHighlight.BeginAnimation(OpacityProperty, animation);
        }

        private void OpenGitHubAboutRow_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(OpenGitHub));
        }

        private void OpenWebsiteAboutRow_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => OpenExternalUrl(WebsiteUrl)));
        }

        private static void OpenGitHub()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/BochengYao/LyricIsland",
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
                // External navigation is best-effort; keep the settings window uninterrupted.
            }
        }

        private static void OpenExternalUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
                // External navigation is best-effort; keep the settings window uninterrupted.
            }
        }

        private void RestartTutorialAboutRow_Click(object sender, RoutedEventArgs e)
        {
            var callback = startTutorial;
            Close();
            Dispatcher.BeginInvoke(new Action(() => callback?.Invoke()));
        }

        private void InitializeLayoutModePreviewAnimation()
        {
            layoutModePreviewStoryboard = new Storyboard
            {
                Duration = TimeSpan.FromSeconds(3.2),
                RepeatBehavior = RepeatBehavior.Forever
            };

            AddPreviewAnimation(
                "ExpandablePreviewIsland",
                FrameworkElement.WidthProperty,
                0, 116,
                0.65, 116,
                1.08, 250,
                2.3, 250,
                2.78, 116,
                3.2, 116);
            AddPreviewAnimation(
                "ExpandablePreviewExtraModules",
                UIElement.OpacityProperty,
                0, 0,
                0.88, 0,
                1.18, 1,
                2.3, 1,
                2.62, 0,
                3.2, 0);
            UpdateLayoutModePreviewSelection();
        }

        private void AddPreviewAnimation(
            string targetName,
            DependencyProperty property,
            params double[] timeAndValuePairs)
        {
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(3.2)
            };
            for (var index = 0; index + 1 < timeAndValuePairs.Length; index += 2)
            {
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(
                    timeAndValuePairs[index + 1],
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(timeAndValuePairs[index]))));
            }

            Storyboard.SetTargetName(animation, targetName);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            layoutModePreviewStoryboard.Children.Add(animation);
        }

        private void UpdateLayoutModePreviewAnimation()
        {
            if (layoutModePreviewStoryboard == null || !IsLoaded || LayoutSettingsPanel == null)
            {
                return;
            }

            if (LayoutSettingsPanel.Visibility == Visibility.Visible)
            {
                layoutModePreviewStoryboard.Begin(this, true);
            }
            else
            {
                StopLayoutModePreviewAnimation();
            }
        }

        private void StopLayoutModePreviewAnimation()
        {
            if (layoutModePreviewStoryboard != null && IsLoaded)
            {
                layoutModePreviewStoryboard.Stop(this);
            }
        }

        private void UpdateLayoutModePreviewSelection()
        {
            if (HorizontalLayoutPreviewCard == null || ExpandableLayoutPreviewCard == null)
            {
                return;
            }

            var horizontalSelected = ReadEditedLayoutMode() == IslandLayoutMode.HorizontalBlocks;
            SetLayoutPreviewCardSelection(HorizontalLayoutPreviewCard, horizontalSelected);
            SetLayoutPreviewCardSelection(ExpandableLayoutPreviewCard, !horizontalSelected);
        }

        private void SetLayoutPreviewCardSelection(Border card, bool selected)
        {
            card.BorderThickness = new Thickness(selected ? 1.5 : 1);
            if (selected)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(22, 119, 255));
            }
            else
            {
                card.SetResourceReference(Border.BorderBrushProperty, "SettingsControlBorderBrush");
            }
        }

        private LyricsSourcePreference ReadLyricsSource()
        {
            return LyricsSourceComboBox.SelectedValue is LyricsSourcePreference
                ? (LyricsSourcePreference)LyricsSourceComboBox.SelectedValue
                : LyricsSourcePreference.Automatic;
        }

        private bool ReadUseMultiLineDisplay()
        {
            if (ShowTranslationCheckBox.IsChecked == true)
            {
                return true;
            }

            return MultiLineRadioButton.IsChecked == true;
        }

        private void UpdateTranslationLineModeLock()
        {
            if (SingleLineRadioButton == null || MultiLineRadioButton == null || ShowTranslationCheckBox == null)
            {
                return;
            }

            if (ShowTranslationCheckBox.IsChecked == true)
            {
                MultiLineRadioButton.IsChecked = true;
            }

            SingleLineRadioButton.IsEnabled = true;
            MultiLineRadioButton.IsEnabled = true;
        }

        private void UpdateSettingValueLabels()
        {
            if (HoverAuraSizeValueText == null || SpectrumEdgeTransparencyValueText == null || LyricsWidthValueText == null ||
                DividerOpacityValueText == null || DividerSpacingValueText == null ||
                NoPlaybackAutoRetractValueText == null || ExpandedAutoCollapseValueText == null)
            {
                return;
            }

            HoverAuraSizeValueText.Text = ((int)Math.Round(HoverAuraSizeSlider.Value)).ToString(CultureInfo.InvariantCulture) + " px";
            HoverDetectionRangeValueText.Text = ((int)Math.Round(HoverDetectionRangeSlider.Value)).ToString(CultureInfo.InvariantCulture) + " px";
            HoverAuraAspectRatioValueText.Text = HoverAuraAspectRatioSlider.Value.ToString("0.00", CultureInfo.InvariantCulture) + ":1";
            SpectrumMidPositionValueText.Text = ((int)Math.Round(SpectrumMidPositionSlider.Value)).ToString(CultureInfo.InvariantCulture) + "%";
            SpectrumMidPositionSliderValueText.Text = ((int)Math.Round(SpectrumMidPositionSlider.Value)).ToString(CultureInfo.InvariantCulture) + "%";
            SpectrumCenterTransparencyValueText.Text = ((int)Math.Round(SpectrumCenterTransparencySlider.Value)).ToString(CultureInfo.InvariantCulture) + "%";
            SpectrumMidTransparencyValueText.Text = ((int)Math.Round(SpectrumMidTransparencySlider.Value)).ToString(CultureInfo.InvariantCulture) + "%";
            SpectrumEdgeTransparencyValueText.Text = ((int)Math.Round(SpectrumEdgeTransparencySlider.Value)).ToString(CultureInfo.InvariantCulture) + "%";
            LyricsWidthValueText.Text = ((int)Math.Round(LyricsWidthSlider.Value)).ToString(CultureInfo.InvariantCulture) + " px";
            DividerOpacityValueText.Text = ((int)Math.Round(DividerOpacitySlider.Value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
            DividerSpacingValueText.Text = ((int)Math.Round(DividerSpacingSlider.Value)).ToString(CultureInfo.InvariantCulture) + " px";
            var noPlaybackSeconds = (int)Math.Round(NoPlaybackAutoRetractSlider.Value);
            NoPlaybackAutoRetractValueText.Text = noPlaybackSeconds == 0
                ? "永不"
                : noPlaybackSeconds.ToString(CultureInfo.InvariantCulture) + " 秒";
            ExpandedAutoCollapseValueText.Text = ((int)Math.Round(ExpandedAutoCollapseSlider.Value)).ToString(CultureInfo.InvariantCulture) + " 秒";
            UpdateSpectrumPreview();
            UpdateHoverShapePreview();
        }

        private void SetHoverSpectrumControls(IReadOnlyList<HoverSpectrumStop> stops)
        {
            var normalized = (stops == null || stops.Count < 3)
                ? OverlayPlacementSettings.CreateDefaultHoverSpectrumStops()
                : stops.OrderBy(stop => stop.PositionPercent).Take(3).ToList();

            SpectrumCenterTransparencySlider.Value = normalized[0].TransparencyPercent;
            SpectrumMidPositionSlider.Value = normalized[1].PositionPercent;
            SpectrumMidTransparencySlider.Value = normalized[1].TransparencyPercent;
            SpectrumEdgeTransparencySlider.Value = normalized[2].TransparencyPercent;
        }

        private List<HoverSpectrumStop> ReadHoverSpectrumStops()
        {
            return new List<HoverSpectrumStop>
            {
                new HoverSpectrumStop
                {
                    PositionPercent = 0,
                    TransparencyPercent = (int)Math.Round(SpectrumCenterTransparencySlider.Value)
                },
                new HoverSpectrumStop
                {
                    PositionPercent = (int)Math.Round(SpectrumMidPositionSlider.Value),
                    TransparencyPercent = (int)Math.Round(SpectrumMidTransparencySlider.Value)
                },
                new HoverSpectrumStop
                {
                    PositionPercent = 100,
                    TransparencyPercent = (int)Math.Round(SpectrumEdgeTransparencySlider.Value)
                }
            };
        }

        private void UpdateSpectrumPreview()
        {
            if (SpectrumCenterPreviewStop == null)
            {
                return;
            }

            SpectrumCenterPreviewStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumCenterTransparencySlider.Value), 22, 119, 255);
            SpectrumMidPreviewStop.Offset = Math.Max(0.05, Math.Min(0.95, SpectrumMidPositionSlider.Value / 100.0));
            SpectrumMidPreviewStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumMidTransparencySlider.Value), 22, 119, 255);
            SpectrumEdgePreviewStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumEdgeTransparencySlider.Value), 22, 119, 255);
            if (HoverPreviewCenterStop != null)
            {
                HoverPreviewCenterStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumCenterTransparencySlider.Value), 255, 255, 255);
                HoverPreviewMidStop.Offset = Math.Max(0.05, Math.Min(0.95, SpectrumMidPositionSlider.Value / 100.0));
                HoverPreviewMidStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumMidTransparencySlider.Value), 255, 255, 255);
                HoverPreviewEdgeStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumEdgeTransparencySlider.Value), 255, 255, 255);
            }
        }

        private void UpdateHoverShapePreview()
        {
            if (HoverShapePreviewEllipse == null || HoverAuraAspectRatioSlider == null)
            {
                return;
            }

            var ratio = Math.Max(OverlayPlacementSettings.MinHoverAuraAspectRatio, Math.Min(OverlayPlacementSettings.MaxHoverAuraAspectRatio, HoverAuraAspectRatioSlider.Value));
            var sizeScale = Math.Max(0.7, Math.Min(1.55, HoverAuraSizeSlider.Value / 96.0));
            const double maxWidth = 188;
            const double maxHeight = 68;
            if (ratio >= maxWidth / maxHeight)
            {
                HoverShapePreviewEllipse.Width = maxWidth;
                HoverShapePreviewEllipse.Height = maxWidth / ratio;
            }
            else
            {
                HoverShapePreviewEllipse.Height = maxHeight;
                HoverShapePreviewEllipse.Width = maxHeight * ratio;
            }

            HoverShapePreviewEllipse.Width *= sizeScale;
            HoverShapePreviewEllipse.Height *= sizeScale;

            if (HoverPreviewAura != null)
            {
                HoverPreviewAura.RadiusX = HoverShapePreviewEllipse.Width / 2;
                HoverPreviewAura.RadiusY = HoverShapePreviewEllipse.Height / 2;
            }
        }

        private static byte GetPreviewAlpha(double transparencyPercent)
        {
            var normalized = Math.Max(0, Math.Min(100, transparencyPercent));
            return (byte)Math.Round(255 * (100 - normalized) / 100.0);
        }

        private void CenterOnDesktop()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
            Top = workArea.Top + (workArea.Height - ActualHeight) / 2;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (!IsHotkeyTextBoxSource(source) && !IsInteractiveControlSource(source))
            {
                Keyboard.ClearFocus();
            }

            if (e.ButtonState == MouseButtonState.Pressed && CanStartWindowDrag(source))
            {
                DragMove();
            }
        }

        private bool IsHotkeyTextBoxSource(DependencyObject source)
        {
            while (source != null)
            {
                var textBox = source as TextBox;
                if (ReferenceEquals(textBox, EarlierHotkeyTextBox) ||
                    ReferenceEquals(textBox, LaterHotkeyTextBox) ||
                    ReferenceEquals(textBox, ResetHotkeyTextBox) ||
                    ReferenceEquals(textBox, TemporaryInteractionHotkeyTextBox))
                {
                    return true;
                }

                source = GetVisualOrLogicalParent(source);
            }

            return false;
        }

        private bool CanStartWindowDrag(DependencyObject source)
        {
            if (IsInteractiveControlSource(source))
            {
                return false;
            }

            while (source != null)
            {
                if (ReferenceEquals(source, ModuleToolbox))
                {
                    return false;
                }

                source = GetVisualOrLogicalParent(source);
            }

            return true;
        }

        private static bool IsInteractiveControlSource(DependencyObject source)
        {
            while (source != null)
            {
                if (source is ButtonBase || source is Selector || source is ComboBoxItem ||
                    source is RangeBase || source is TextBoxBase ||
                    source is FrameworkElement element && Equals(element.Tag, "TextLink"))
                {
                    return true;
                }

                source = GetVisualOrLogicalParent(source);
            }

            return false;
        }

        private static DependencyObject GetVisualOrLogicalParent(DependencyObject source)
        {
            return source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        private sealed class ScreenOption
        {
            public ScreenOption(string name, string displayName)
            {
                Name = name;
                DisplayName = displayName;
            }

            public string Name { get; }

            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class LyricsSourceOption
        {
            public LyricsSourceOption(LyricsSourcePreference value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public LyricsSourcePreference Value { get; }

            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class PlayerSelectionOption
        {
            public PlayerSelectionOption(string value, string displayName, bool isDetected)
            {
                Value = value ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                IsDetected = isDetected;
            }

            public string Value { get; }

            public string DisplayName { get; }

            public bool IsDetected { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

    }
}
