using System;
using System.Windows.Controls;
using System.Windows.Media;
using LyricHover.Core.Media;

namespace LyricHover.App.Modules
{
    public partial class ProgressModuleView : UserControl, IIslandModuleView
    {
        private TimeSpan? lastDuration;
        private long lastPositionSecond = long.MinValue;
        private double lastProgressValue = double.NaN;
        private FontFamily chineseFontFamily = new FontFamily("Microsoft YaHei UI");
        private FontFamily englishFontFamily = new FontFamily("Segoe UI");
        private double fontScale = 1;

        public ProgressModuleView()
        {
            InitializeComponent();
        }

        public void ApplyAppearance(OverlayPlacementSettings settings)
        {
            settings = settings ?? new OverlayPlacementSettings();
            chineseFontFamily = IslandAppearanceRendering.ResolveFont(settings.IslandChineseFontFamily, "Microsoft YaHei UI");
            englishFontFamily = IslandAppearanceRendering.ResolveFont(settings.IslandEnglishFontFamily, "Segoe UI");
            fontScale = settings.IslandLyricsFontSize / OverlayPlacementSettings.LegacyIslandLyricsFontSize;
            ApplyTextAppearance();
        }

        public void Update(IslandRenderState state)
        {
            state = state ?? new IslandRenderState();
            var textChanged = false;
            var duration = state.Session?.Duration ?? TimeSpan.Zero;
            if (!lastDuration.HasValue || lastDuration.Value != duration)
            {
                lastDuration = duration;
                ProgressBar.Maximum = Math.Max(1, duration.TotalSeconds);
                DurationText.Text = FormatTime(duration);
                textChanged = true;
            }

            var progressValue = Math.Max(0, Math.Min(ProgressBar.Maximum, state.EffectivePosition.TotalSeconds));
            if (lastProgressValue != progressValue)
            {
                lastProgressValue = progressValue;
                ProgressBar.Value = progressValue;
            }

            var normalizedPosition = state.EffectivePosition < TimeSpan.Zero
                ? TimeSpan.Zero
                : state.EffectivePosition;
            var positionSecond = (long)normalizedPosition.TotalSeconds;
            if (lastPositionSecond != positionSecond)
            {
                lastPositionSecond = positionSecond;
                PositionText.Text = FormatTime(normalizedPosition);
                textChanged = true;
            }
            if (textChanged) ApplyTextAppearance();
        }

        private void ApplyTextAppearance()
        {
            PositionText.FontSize = 9.5 * fontScale;
            DurationText.FontSize = 9.5 * fontScale;
            IslandAppearanceRendering.SetMixedText(PositionText, PositionText.Text, chineseFontFamily, englishFontFamily);
            IslandAppearanceRendering.SetMixedText(DurationText, DurationText.Text, chineseFontFamily, englishFontFamily);
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
            {
                time = TimeSpan.Zero;
            }

            return time.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }
    }
}
