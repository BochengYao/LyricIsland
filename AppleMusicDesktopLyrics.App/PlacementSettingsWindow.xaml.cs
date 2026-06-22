using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AppleMusicDesktopLyrics.Core;

namespace AppleMusicDesktopLyrics.App
{
    public partial class PlacementSettingsWindow : Window
    {
        private readonly IReadOnlyList<OverlayScreenArea> screens;
        private readonly Action<OverlayPlacementSettings> applySettings;
        private readonly int initialCacheLimitMegabytes;
        private SettingsThemePreference selectedThemePreference = SettingsThemePreference.System;

        public PlacementSettingsWindow(
            IReadOnlyList<OverlayScreenArea> screens,
            OverlayPlacementSettings currentSettings,
            Action<OverlayPlacementSettings> applySettings)
        {
            InitializeComponent();
            this.screens = screens ?? new List<OverlayScreenArea>().AsReadOnly();
            this.applySettings = applySettings ?? throw new ArgumentNullException(nameof(applySettings));

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

            var settings = currentSettings ?? new OverlayPlacementSettings();
            settings.Normalize();
            initialCacheLimitMegabytes = settings.CacheLimitMegabytes;
            selectedThemePreference = settings.SettingsTheme;
            SetThemeRadioButton(selectedThemePreference);
            ApplySettingsTheme();
            LyricsSourceComboBox.SelectedValue = settings.LyricsSource;
            SingleLineRadioButton.IsChecked = !settings.UseMultiLineDisplay;
            MultiLineRadioButton.IsChecked = settings.UseMultiLineDisplay;
            ShowTranslationCheckBox.IsChecked = settings.ShowTranslation;
            ScreenComboBox.SelectedValue = string.IsNullOrWhiteSpace(settings.ScreenName)
                ? this.screens.FirstOrDefault()?.Name
                : settings.ScreenName;
            OffsetSlider.Value = Math.Max(0, Math.Min(100, settings.OffsetRatio * 100));
            CacheLimitTextBox.Text = settings.CacheLimitMegabytes.ToString(CultureInfo.InvariantCulture);
            HoverAuraSizeSlider.Value = settings.HoverAuraSize;
            HoverDetectionRangeSlider.Value = settings.HoverDetectionRange;
            HoverAuraAspectRatioSlider.Value = settings.HoverAuraAspectRatio;
            PassThroughOnHoverCheckBox.IsChecked = settings.PassThroughOnHover;
            SetHoverSpectrumControls(settings.HoverSpectrumStops);
            UpdateTranslationLineModeLock();
            UpdateSettingValueLabels();
            LyricsSectionButton.IsChecked = true;
            ShowSection("Lyrics");
            Loaded += (sender, args) =>
            {
                ApplySettingsTheme();
                CenterOnDesktop();
            };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            applySettings(ReadSettings());
            DialogResult = true;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            applySettings(ReadSettings());
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private OverlayPlacementSettings ReadSettings()
        {
            return new OverlayPlacementSettings
            {
                ScreenName = ScreenComboBox.SelectedValue as string ?? screens.FirstOrDefault()?.Name ?? string.Empty,
                Edge = OverlayDockEdge.Top,
                OffsetRatio = OffsetSlider.Value / 100.0,
                CacheLimitMegabytes = ReadCacheLimitMegabytes(),
                HoverAuraSize = (int)Math.Round(HoverAuraSizeSlider.Value),
                HoverDetectionRange = (int)Math.Round(HoverDetectionRangeSlider.Value),
                HoverAuraAspectRatio = HoverAuraAspectRatioSlider.Value,
                HoverTransparencyPercent = (int)Math.Round(SpectrumCenterTransparencySlider.Value),
                HoverSpectrumStops = ReadHoverSpectrumStops(),
                PassThroughOnHover = PassThroughOnHoverCheckBox.IsChecked == true,
                SettingsTheme = selectedThemePreference,
                LyricsSource = ReadLyricsSource(),
                UseMultiLineDisplay = ReadUseMultiLineDisplay(),
                ShowTranslation = ShowTranslationCheckBox.IsChecked == true
            };
        }

        private int ReadCacheLimitMegabytes()
        {
            int value;
            if (!int.TryParse(CacheLimitTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                value = initialCacheLimitMegabytes;
            }

            return Math.Max(OverlayPlacementSettings.MinCacheLimitMegabytes, Math.Min(OverlayPlacementSettings.MaxCacheLimitMegabytes, value));
        }

        private void SettingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSettingValueLabels();
        }

        private void ShowTranslationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTranslationLineModeLock();
        }

        private void SectionButton_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;
            ShowSection(button?.Tag as string ?? "Lyrics");
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
            var rootBackground = BrushFromHex(dark ? "#111318" : "#F7F8FB");
            var cardBackground = BrushFromHex(dark ? "#181B22" : "#FFFFFFFF");
            var cardBorder = BrushFromHex(dark ? "#2A303A" : "#E6E9EF");
            var muted = BrushFromHex(dark ? "#9CA3AF" : "#667085");
            var primary = BrushFromHex(dark ? "#F3F4F6" : "#202124");
            var toggleBackground = BrushFromHex(dark ? "#242A34" : "#EEF1F6");

            UpdateThemeResources(dark);
            RootChrome.Background = rootBackground;
            RootChrome.BorderBrush = BrushFromHex(dark ? "#303642" : "#D4DAE3");
            SidebarCard.Background = cardBackground;
            SidebarCard.BorderBrush = cardBorder;
            ContentCard.Background = cardBackground;
            ContentCard.BorderBrush = cardBorder;
            ThemeToggleRoot.Background = toggleBackground;
            HeaderTitleText.Foreground = primary;
            HeaderSubtitleText.Foreground = muted;
            ApplyTextTheme(this, primary, muted);
        }

        private void UpdateThemeResources(bool dark)
        {
            SetBrushResource("SettingsControlBackgroundBrush", dark ? "#20252E" : "#F8FAFC");
            SetBrushResource("SettingsControlForegroundBrush", dark ? "#F3F4F6" : "#1F2937");
            SetBrushResource("SettingsControlMutedForegroundBrush", dark ? "#B4BDCA" : "#667085");
            SetBrushResource("SettingsControlBorderBrush", dark ? "#3A4250" : "#D8DEE8");
            SetBrushResource("SettingsControlHoverBackgroundBrush", dark ? "#28303B" : "#FFFFFF");
            SetBrushResource("SettingsControlPressedBackgroundBrush", dark ? "#303846" : "#EEF2F7");
            SetBrushResource("SettingsSelectedBackgroundBrush", dark ? "#2D3542" : "#FFFFFF");
            SetBrushResource("SettingsSelectedForegroundBrush", dark ? "#F8FAFC" : "#111827");
            SetBrushResource("SettingsSidebarHoverBackgroundBrush", dark ? "#202733" : "#F2F5FA");
            SetBrushResource("SettingsSidebarSelectedBackgroundBrush", dark ? "#20324D" : "#EAF2FF");
            SetBrushResource("SettingsSidebarSelectedForegroundBrush", dark ? "#8DBBFF" : "#0F5FD7");
            SetBrushResource("SettingsTrackBackgroundBrush", dark ? "#303846" : "#E6EAF0");
        }

        private void SetBrushResource(string key, string hex)
        {
            Resources[key] = BrushFromHex(hex);
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
                if (IsDescendantOf(HoverPreviewScene, textBlock))
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
                HoverSettingsPanel == null)
            {
                return;
            }

            LyricsSettingsPanel.Visibility = section == "Lyrics" ? Visibility.Visible : Visibility.Collapsed;
            PositionSettingsPanel.Visibility = section == "Position" ? Visibility.Visible : Visibility.Collapsed;
            CacheSettingsPanel.Visibility = section == "Cache" ? Visibility.Visible : Visibility.Collapsed;
            HoverSettingsPanel.Visibility = section == "Hover" ? Visibility.Visible : Visibility.Collapsed;
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
                SingleLineRadioButton.IsEnabled = false;
                MultiLineRadioButton.IsEnabled = false;
            }
            else
            {
                SingleLineRadioButton.IsEnabled = true;
                MultiLineRadioButton.IsEnabled = true;
            }
        }

        private void UpdateSettingValueLabels()
        {
            if (HoverAuraSizeValueText == null || SpectrumEdgeTransparencyValueText == null)
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
            SpectrumEdgePreviewStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumEdgeTransparencySlider.Value), 255, 255, 255);
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
            if (e.ButtonState == MouseButtonState.Pressed && CanStartWindowDrag(e.OriginalSource as DependencyObject))
            {
                DragMove();
            }
        }

        private static bool CanStartWindowDrag(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button || source is TextBox || source is ComboBox || source is Slider || source is CheckBox || source is RadioButton)
                {
                    return false;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return true;
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

    }
}
