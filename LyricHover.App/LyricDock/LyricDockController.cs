using System;

namespace LyricHover.App.LyricDock
{
    public interface ILyricDockSurface
    {
        event EventHandler SettingsRequested;
        bool IsVisible { get; }
        void Show();
        void Hide();
        void Present(LyricsPresentationSnapshot snapshot);
        void Place(LyricDockPlacement placement, double width);
    }

    public sealed class LyricDockController : IDisposable
    {
        public const double MinimumWidth = 220;
        public const double MaximumWidth = 360;
        private readonly ILyricDockEnvironment environment;
        private readonly WidgetVisibilityLease widgetLease;
        private readonly ILyricDockSurface surface;
        private bool enabled;
        private bool requestedEnabled;
        private bool widgetsHidingUnavailable;
        private bool restoringStartup;
        private string screenName;
        private LyricDockAlignment alignment;
        private LyricsPresentationSnapshot snapshot = new LyricsPresentationSnapshot { IsWaitingForPlayback = true };

        public LyricDockController(ILyricDockEnvironment environment, WidgetVisibilityLease widgetLease, ILyricDockSurface surface)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.widgetLease = widgetLease ?? throw new ArgumentNullException(nameof(widgetLease));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            environment.Changed += EnvironmentChanged;
            surface.SettingsRequested += (sender, args) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SettingsRequested;
        public event EventHandler<LyricDockFailureReason> FeatureDisabled;
        public event EventHandler WidgetsHidingDegraded;
        public bool IsEnabled => enabled;
        public LyricDockFailureReason LastFailureReason { get; private set; }

        // An old crash may have left Widgets suppressed.  This must occur before the persisted setting is honored.
        public bool Start(bool requestedEnabled, string requestedScreenName, LyricDockAlignment requestedAlignment)
        {
            screenName = requestedScreenName ?? string.Empty;
            alignment = requestedAlignment;
            if (!widgetLease.RestoreResidualLease(screenName))
            {
                Disable(LyricDockFailureReason.RegistryOrRefreshFailed, restoreWidgets: false);
                return false;
            }
            restoringStartup = true;
            try
            {
                // Startup restoration must stay silent: the degraded notice is only useful
                // when the user explicitly turns the feature on from settings.
                return Configure(requestedEnabled, requestedScreenName, requestedAlignment);
            }
            finally
            {
                restoringStartup = false;
            }
        }

        public bool Configure(bool requestedEnabled, string requestedScreenName, LyricDockAlignment requestedAlignment)
        {
            this.requestedEnabled = requestedEnabled;
            screenName = requestedScreenName ?? string.Empty;
            alignment = requestedAlignment;
            LastFailureReason = LyricDockFailureReason.None;
            if (!requestedEnabled)
            {
                enabled = false;
                // Re-arm the Widgets-hiding attempt for the next enable cycle.
                widgetsHidingUnavailable = false;
                HideAndRestore();
                return true;
            }

            if (!environment.IsSupported)
            {
                Disable(LyricDockFailureReason.UnsupportedOS);
                return false;
            }
            if (!environment.TryGetPlacement(screenName, alignment, out var initialPlacement, out var reason) || !CanUse(initialPlacement))
            {
                Disable(reason == LyricDockFailureReason.None ? LyricDockFailureReason.TaskbarNotFound : reason);
                return false;
            }
            if (!widgetsHidingUnavailable && !widgetLease.TryAcquire())
            {
                // Widgets hiding is an optional space enhancement, not a prerequisite.  Some
                // machines (notably Win11 25H2 with registry write protection) block TaskbarDa
                // writes entirely; keep the feature working with Widgets visible because the
                // placement logic already accounts for them as occupied space.  Stop retrying
                // for this session so refreshes don't repeat the slow acquisition timeout.
                widgetsHidingUnavailable = true;
                if (!restoringStartup) WidgetsHidingDegraded?.Invoke(this, EventArgs.Empty);            }

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
                if (reason == LyricDockFailureReason.TaskbarAutoHiddenOrFullscreen)
                {
                    surface.Hide();
                    return;
                }
                Disable(reason == LyricDockFailureReason.None ? LyricDockFailureReason.TaskbarChanged : reason);
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
        private bool CanUse(LyricDockPlacement placement) => placement != null && placement.IsVisible && !placement.IsFullscreenCovered && placement.Width >= MinimumWidth && placement.Height > 0 && placement.DpiScale > 0;
        private void Show(LyricDockPlacement placement)
        {
            surface.Place(placement, Math.Min(MaximumWidth, placement.Width));
            surface.Present(snapshot);
            if (!surface.IsVisible) surface.Show();
        }
        private void Disable(LyricDockFailureReason reason, bool restoreWidgets = true)
        {
            LastFailureReason = reason;
            enabled = false;
            requestedEnabled = false;
            if (restoreWidgets) HideAndRestore();
            else surface.Hide();
            FeatureDisabled?.Invoke(this, reason);
        }
        private void HideAndRestore()
        {
            surface.Hide();
            widgetLease.TryRestore();
        }
    }
}
