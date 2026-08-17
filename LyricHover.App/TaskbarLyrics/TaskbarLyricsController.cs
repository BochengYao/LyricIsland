using System;

namespace LyricHover.App.TaskbarLyrics
{
    public interface ITaskbarLyricsSurface
    {
        event EventHandler SettingsRequested;
        bool IsVisible { get; }
        void Show();
        void Hide();
        void Present(LyricsPresentationSnapshot snapshot);
        void Place(TaskbarLyricsPlacement placement, double width);
    }

    public sealed class TaskbarLyricsController : IDisposable
    {
        public const double MinimumWidth = 220;
        public const double MaximumWidth = 360;
        private readonly ITaskbarEnvironment environment;
        private readonly WidgetVisibilityLease widgetLease;
        private readonly ITaskbarLyricsSurface surface;
        private bool enabled;
        private bool requestedEnabled;
        private string screenName;
        private TaskbarLyricsAlignment alignment;
        private LyricsPresentationSnapshot snapshot = new LyricsPresentationSnapshot { IsWaitingForPlayback = true };

        public TaskbarLyricsController(ITaskbarEnvironment environment, WidgetVisibilityLease widgetLease, ITaskbarLyricsSurface surface)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.widgetLease = widgetLease ?? throw new ArgumentNullException(nameof(widgetLease));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            environment.Changed += EnvironmentChanged;
            surface.SettingsRequested += (sender, args) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SettingsRequested;
        public event EventHandler<TaskbarLyricsFailureReason> FeatureDisabled;
        public bool IsEnabled => enabled;
        public TaskbarLyricsFailureReason LastFailureReason { get; private set; }

        // An old crash may have left Widgets suppressed.  This must occur before the persisted setting is honored.
        public bool Start(bool requestedEnabled, string requestedScreenName, TaskbarLyricsAlignment requestedAlignment)
        {
            if (!widgetLease.RestoreResidualLease())
            {
                LastFailureReason = TaskbarLyricsFailureReason.RegistryOrRefreshFailed;
                return false;
            }
            return Configure(requestedEnabled, requestedScreenName, requestedAlignment);
        }

        public bool Configure(bool requestedEnabled, string requestedScreenName, TaskbarLyricsAlignment requestedAlignment)
        {
            this.requestedEnabled = requestedEnabled;
            screenName = requestedScreenName ?? string.Empty;
            alignment = requestedAlignment;
            LastFailureReason = TaskbarLyricsFailureReason.None;
            if (!requestedEnabled)
            {
                enabled = false;
                HideAndRestore();
                return true;
            }

            if (!environment.IsWindows11)
            {
                Disable(TaskbarLyricsFailureReason.Windows11Required);
                return false;
            }
            if (!environment.TryGetPlacement(screenName, alignment, out var initialPlacement, out var reason) || !CanUse(initialPlacement))
            {
                Disable(reason == TaskbarLyricsFailureReason.None ? TaskbarLyricsFailureReason.TaskbarNotFound : reason);
                return false;
            }
            if (!widgetLease.TryAcquire())
            {
                Disable(TaskbarLyricsFailureReason.RegistryOrRefreshFailed);
                return false;
            }

            enabled = true;
            Show(initialPlacement);
            return true;
        }

        public void Present(LyricsPresentationSnapshot value)
        {
            snapshot = value ?? new LyricsPresentationSnapshot { IsWaitingForPlayback = true };
            if (snapshot.IsWaitingForPlayback && string.IsNullOrWhiteSpace(snapshot.PrimaryText)) snapshot.PrimaryText = "等待播放";
            RefreshPlacement();
        }

        public void RefreshPlacement()
        {
            if (!enabled) return;
            if (!environment.TryGetPlacement(screenName, alignment, out var placement, out var reason) || !CanUse(placement))
            {
                if (reason == TaskbarLyricsFailureReason.TaskbarAutoHiddenOrFullscreen)
                {
                    surface.Hide();
                    return;
                }
                Disable(reason == TaskbarLyricsFailureReason.None ? TaskbarLyricsFailureReason.TaskbarChanged : reason);
                return;
            }
            Show(placement);
        }

        public void Dispose()
        {
            environment.Changed -= EnvironmentChanged;
            enabled = false;
            HideAndRestore();
        }

        private void EnvironmentChanged(object sender, EventArgs args)
        {
            RefreshPlacement();
        }
        private bool CanUse(TaskbarLyricsPlacement placement) => placement != null && placement.IsVisible && !placement.IsFullscreenCovered && placement.Width >= MinimumWidth && placement.Height > 0 && placement.DpiScale > 0;
        private void Show(TaskbarLyricsPlacement placement)
        {
            surface.Place(placement, Math.Min(MaximumWidth, placement.Width));
            surface.Present(snapshot);
            if (!surface.IsVisible) surface.Show();
        }
        private void Disable(TaskbarLyricsFailureReason reason)
        {
            LastFailureReason = reason;
            enabled = false;
            requestedEnabled = false;
            HideAndRestore();
            FeatureDisabled?.Invoke(this, reason);
        }
        private void HideAndRestore()
        {
            surface.Hide();
            widgetLease.TryRestore();
        }
    }
}
