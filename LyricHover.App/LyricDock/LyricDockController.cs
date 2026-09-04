using System;
using System.Threading;
using System.Threading.Tasks;

namespace LyricHover.App.LyricDock
{
    public interface ILyricDockSurface
    {
        event EventHandler SettingsRequested;
        event EventHandler RefreshRequested;
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
        private bool disposed;
        private int widgetsHidingGeneration;
        private TaskbarDaValueState? observedWidgetsState;
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
            surface.RefreshRequested += (sender, args) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SettingsRequested;
        public event EventHandler RefreshRequested;
        public event EventHandler<LyricDockFailureReason> FeatureDisabled;
        public event EventHandler WidgetsHidden;
        public event EventHandler WidgetsHidingNeedsSettingsConfirmation;
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
            var configured = false;
            try
            {
                // Startup restoration must stay silent: the degraded notice is only useful
                // when the user explicitly turns the feature on from settings.
                configured = Configure(requestedEnabled, requestedScreenName, requestedAlignment);
            }
            finally
            {
                restoringStartup = false;
            }
            if (configured && requestedEnabled) StartWidgetsHidingAttempt();
            return configured;
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
            enabled = true;
            Show(initialPlacement);
            ObserveWidgetsState();
            StartWidgetsHidingAttempt();
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
            disposed = true;
            Interlocked.Increment(ref widgetsHidingGeneration);
            enabled = false;
            HideAndRestore();
        }

        private void EnvironmentChanged(object sender, EventArgs args)
        {
            RefreshPlacement();
            if (!enabled || !ObserveWidgetsState(out var changedState) || changedState == TaskbarDaValueState.Disabled) return;

            // A manual Windows Settings change has made Widgets visible again.  Reuse the
            // normal silent-first flow: reassert the existing lease when possible, otherwise
            // ask before opening Settings again.
            widgetsHidingUnavailable = false;
            StartWidgetsHidingAttempt();
        }

        private void ObserveWidgetsState()
        {
            if (environment.TryReadTaskbarDa(out var current)) observedWidgetsState = current;
        }

        private bool ObserveWidgetsState(out TaskbarDaValueState changedState)
        {
            changedState = TaskbarDaValueState.Absent;
            if (!environment.TryReadTaskbarDa(out var current)) return false;
            var changed = observedWidgetsState.HasValue && observedWidgetsState.Value != current;
            observedWidgetsState = current;
            changedState = current;
            return changed;
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

        public void TryHideWidgetsThroughSettingsUi()
        {
            if (!enabled || disposed) return;
            var generation = Interlocked.Increment(ref widgetsHidingGeneration);
            Task.Run(() => widgetLease.TryAcquireThroughSettingsUi(screenName)).ContinueWith(task =>
            {
                if (disposed || generation != Volatile.Read(ref widgetsHidingGeneration) || !enabled) return;
                if (task.Status == TaskStatus.RanToCompletion && task.Result)
                {
                    ObserveWidgetsState();
                    widgetsHidingUnavailable = false;
                    WidgetsHidden?.Invoke(this, EventArgs.Empty);
                }
            }, TaskScheduler.Default);
        }

        private void StartWidgetsHidingAttempt()
        {
            if (widgetsHidingUnavailable || restoringStartup || disposed) return;

            var generation = Interlocked.Increment(ref widgetsHidingGeneration);
            // Explorer can take seconds to apply TaskbarDa.  The current placement is already
            // safe with Widgets visible, so its verification must not block the WPF Dispatcher.
            Task.Run(() => widgetLease.TryAcquire(screenName)).ContinueWith(task =>
            {
                if (disposed || generation != Volatile.Read(ref widgetsHidingGeneration) || !enabled)
                {
                    if (task.Status == TaskStatus.RanToCompletion && task.Result)
                    {
                        widgetLease.TryRestore();
                    }
                    return;
                }

                if (task.Status == TaskStatus.RanToCompletion && task.Result)
                {
                    ObserveWidgetsState();
                    WidgetsHidden?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // The background path did not have permission.  Let the host ask before
                // opening Windows Settings; never surface that page without consent.
                widgetsHidingUnavailable = true;
                WidgetsHidingNeedsSettingsConfirmation?.Invoke(this, EventArgs.Empty);
            }, TaskScheduler.Default);
        }
    }
}
