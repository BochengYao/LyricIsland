using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppleMusicDesktopLyrics.Core;

namespace AppleMusicDesktopLyrics.App
{
    public partial class PlacementSettingsWindow : Window
    {
        private readonly IReadOnlyList<OverlayScreenArea> screens;
        private readonly Action<OverlayPlacementSettings> applySettings;
        private readonly int initialCacheLimitMegabytes;

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
            SetHoverSpectrumControls(settings.HoverSpectrumStops);
            UpdateTranslationLineModeLock();
            UpdateSettingValueLabels();
            LyricsSectionButton.IsChecked = true;
            ShowSection("Lyrics");
            Loaded += (sender, args) => CenterOnDesktop();
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
