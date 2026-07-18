using System;
using System.Windows.Controls;
using AppleMusicDesktopLyrics.Core.Media;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public partial class ProgressModuleView : UserControl, IIslandModuleView
    {
        public ProgressModuleView()
        {
            InitializeComponent();
        }

        public void Update(IslandRenderState state)
        {
            state = state ?? new IslandRenderState();
            var duration = state.Session?.Duration ?? TimeSpan.Zero;
            ProgressBar.Maximum = Math.Max(1, duration.TotalSeconds);
            ProgressBar.Value = Math.Max(0, Math.Min(ProgressBar.Maximum, state.EffectivePosition.TotalSeconds));
            PositionText.Text = (state.TimelineReliability == TimelineReliability.Estimated ? "≈" : "") +
                FormatTime(state.EffectivePosition);
            DurationText.Text = FormatTime(duration);
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
