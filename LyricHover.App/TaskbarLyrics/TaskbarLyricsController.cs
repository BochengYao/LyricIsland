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
        public bool IsEnabled => enabled;

        // An old crash may have left Widgets suppressed.  This must occur before the persisted setting is honored.
        public bool Start(bool requestedEnabled, string requestedScreenName)
        {
            if (!widgetLease.RestoreResidualLease()) return false;
            Configure(requestedEnabled, requestedScreenName);
            return !requestedEnabled || enabled;
        }

        public bool Configure(bool requestedEnabled, string requestedScreenName)
        {
            this.requestedEnabled = requestedEnabled;
            screenName = requestedScreenName ?? string.Empty;
            if (!requestedEnabled)
            {
                enabled = false;
                HideAndRestore();
                return true;
            }

            if (!environment.IsWindows11 ||
                !environment.TryGetPlacement(screenName, out var initialPlacement) ||
                initialPlacement == null || !initialPlacement.IsVisible || initialPlacement.IsFullscreenCovered ||
                initialPlacement.Width < MinimumWidth || initialPlacement.Height <= 0 || initialPlacement.DpiScale <= 0 ||
                !widgetLease.TryAcquire())
            {
                enabled = false;
                HideAndRestore();
                return false;
            }

            enabled = true;
            Present(snapshot);
            return enabled;
        }

        public void Present(LyricsPresentationSnapshot value)
        {
            snapshot = value ?? new LyricsPresentationSnapshot { IsWaitingForPlayback = true };
            RefreshPlacement();
        }

        public void RefreshPlacement()
        {
            if (!enabled) return;
            if (!environment.TryGetPlacement(screenName, out var placement) || placement == null || !placement.IsVisible || placement.IsFullscreenCovered)
            {
                surface.Hide();
                return;
            }

            var width = Math.Min(MaximumWidth, placement.Width);
            if (width < MinimumWidth || placement.Height <= 0 || placement.DpiScale <= 0)
            {
                // Fail closed: do not draw in an uncertain taskbar region.  Keep the lease until the
                // user explicitly disables the feature or the process exits, so an environment change can recover.
                surface.Hide();
                return;
            }

            surface.Place(placement, width);
            surface.Present(snapshot);
            if (!surface.IsVisible) surface.Show();
        }

        public void Dispose()
        {
            environment.Changed -= EnvironmentChanged;
            enabled = false;
            HideAndRestore();
        }

        private void EnvironmentChanged(object sender, EventArgs args)
        {
            if (!enabled && requestedEnabled) Configure(true, screenName);
            else RefreshPlacement();
        }
        private void HideAndRestore()
        {
            surface.Hide();
            widgetLease.TryRestore();
        }
    }
}
