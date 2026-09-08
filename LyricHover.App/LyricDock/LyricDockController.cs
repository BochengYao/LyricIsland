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
        private bool startupRecoveryPending;
        private bool startupRecoveryQueued;
        private bool disposed;
        private int widgetsHidingGeneration;
        private int startupRecoveryGeneration;
        private readonly SynchronizationContext uiContext;
        private readonly object widgetsOperationSync = new object();
        private Task widgetsOperation = Task.CompletedTask;
        private TaskbarDaValueState? observedWidgetsState;
        private string screenName;
        private LyricDockAlignment alignment;
        private LyricsPresentationSnapshot snapshot = new LyricsPresentationSnapshot { IsWaitingForPlayback = true };

        public LyricDockController(ILyricDockEnvironment environment, WidgetVisibilityLease widgetLease, ILyricDockSurface surface)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.widgetLease = widgetLease ?? throw new ArgumentNullException(nameof(widgetLease));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            uiContext = SynchronizationContext.Current;
            environment.Changed += EnvironmentChanged;
            surface.SettingsRequested += (sender, args) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            surface.RefreshRequested += (sender, args) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SettingsRequested;
        public event EventHandler RefreshRequested;
        public event EventHandler<LyricDockFailureReason> FeatureDisabled;
        public event EventHandler WidgetsHidden;
        public event EventHandler WidgetsHidingNeedsSettingsConfirmation;
        public event EventHandler RuntimeRecovered;
        public event EventHandler StartupRecoveryPending;
        public bool IsEnabled => enabled;
        public LyricDockFailureReason LastFailureReason { get; private set; }

        // An old crash may have left Widgets suppressed.  This must occur before the persisted setting is honored.
        public bool Start(bool requestedEnabled, string requestedScreenName, LyricDockAlignment requestedAlignment)
        {
            this.requestedEnabled = requestedEnabled;
            screenName = requestedScreenName ?? string.Empty;
            alignment = requestedAlignment;
            if (!widgetLease.RestoreResidualLease(screenName))
            {
                startupRecoveryPending = true;
                Interlocked.Increment(ref startupRecoveryGeneration);
                var pendingReason = LyricDockFailureReason.RegistryOrRefreshFailed;
                if (requestedEnabled && TryShowWhileStartupRecoveryIsPending(out pendingReason))
                {
                    LastFailureReason = LyricDockFailureReason.RegistryOrRefreshFailed;
                    StartupRecoveryPending?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                Disable(pendingReason == LyricDockFailureReason.None
                    ? LyricDockFailureReason.RegistryOrRefreshFailed
                    : pendingReason, restoreWidgets: false);
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
            if (!requestedEnabled)
            {
                enabled = false;
                Interlocked.Increment(ref widgetsHidingGeneration);
                Interlocked.Increment(ref startupRecoveryGeneration);
                startupRecoveryQueued = false;
                // Re-arm the Widgets-hiding attempt for the next enable cycle.
                widgetsHidingUnavailable = false;
                HideAndQueueRestore();
                return true;
            }

            if (startupRecoveryPending)
            {
                QueueStartupRecovery();
                if (TryShowWhileStartupRecoveryIsPending(out var pendingReason))
                {
                    StartupRecoveryPending?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                // The unresolved lease remains untouched.  Auto-hide, fullscreen and
                // insufficient-space states keep their normal hidden behavior, while a
                // later environment change or settings apply can safely probe again.
                LastFailureReason = pendingReason == LyricDockFailureReason.None
                    ? LyricDockFailureReason.TaskbarNotFound
                    : pendingReason;
                enabled = false;
                surface.Hide();
                return false;
            }

            LastFailureReason = LyricDockFailureReason.None;

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
            if (disposed) return;
            environment.Changed -= EnvironmentChanged;
            disposed = true;
            Interlocked.Increment(ref widgetsHidingGeneration);
            enabled = false;
            requestedEnabled = false;
            HideAndQueueRestore();
        }

        private void EnvironmentChanged(object sender, EventArgs args)
        {
            if (startupRecoveryPending)
            {
                // A pending residual restore owns the old lease, but it must not freeze
                // the visual safety probe. Environment changes can make a previously
                // unsafe gap usable again; probe and show it without acquiring anything.
                var pendingReason = LyricDockFailureReason.RegistryOrRefreshFailed;
                if (requestedEnabled && TryShowWhileStartupRecoveryIsPending(out pendingReason))
                {
                    return;
                }

                LastFailureReason = pendingReason == LyricDockFailureReason.None
                    ? LyricDockFailureReason.TaskbarNotFound
                    : pendingReason;
                enabled = false;
                surface.Hide();
                return;
            }

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
        private bool TryShowWhileStartupRecoveryIsPending(out LyricDockFailureReason reason)
        {
            reason = LyricDockFailureReason.None;
            if (!environment.IsSupported)
            {
                reason = LyricDockFailureReason.UnsupportedOS;
                return false;
            }

            if (!environment.TryGetPlacement(screenName, alignment, out var placement, out reason) || !CanUse(placement))
            {
                if (reason == LyricDockFailureReason.None) reason = LyricDockFailureReason.TaskbarNotFound;
                return false;
            }

            // Recovery owns the existing lease.  Until it succeeds this safe surface may
            // remain visible, but it must neither acquire nor overwrite another lease.
            enabled = true;
            Show(placement);
            return true;
        }
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
            Interlocked.Increment(ref widgetsHidingGeneration);
            if (restoreWidgets) HideAndQueueRestore();
            else surface.Hide();
            FeatureDisabled?.Invoke(this, reason);
        }
        private void HideAndQueueRestore()
        {
            surface.Hide();
            QueueWidgetsOperation(() => widgetLease.TryRestore());
        }

        public void TryHideWidgetsThroughSettingsUi()
        {
            if (!enabled || startupRecoveryPending || disposed) return;
            var generation = Interlocked.Increment(ref widgetsHidingGeneration);
            QueueWidgetsOperation(() => widgetLease.TryAcquireThroughSettingsUi(screenName)).ContinueWith(task =>
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
            if (startupRecoveryPending || widgetsHidingUnavailable || restoringStartup || disposed) return;

            var generation = Interlocked.Increment(ref widgetsHidingGeneration);
            // Explorer can take seconds to apply TaskbarDa.  The current placement is already
            // safe with Widgets visible, so its verification must not block the WPF Dispatcher.
            QueueWidgetsOperation(() => widgetLease.TryAcquire(screenName)).ContinueWith(task =>
            {
                if (disposed || generation != Volatile.Read(ref widgetsHidingGeneration) || !enabled)
                {
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

        private void QueueStartupRecovery()
        {
            if (startupRecoveryQueued || disposed || !requestedEnabled) return;

            startupRecoveryQueued = true;
            var generation = Volatile.Read(ref startupRecoveryGeneration);
            QueueWidgetsOperation(() => widgetLease.RestoreResidualLease(screenName)).ContinueWith(task =>
            {
                PostToUi(() =>
                {
                    if (generation != Volatile.Read(ref startupRecoveryGeneration) || disposed)
                    {
                        return;
                    }

                    startupRecoveryQueued = false;
                    if (task.Status != TaskStatus.RanToCompletion || !task.Result)
                    {
                        // Keep the recovery record and a safe already-visible surface.
                        // A later settings apply can retry, but cannot acquire or replace
                        // the unresolved lease in the meantime.
                        LastFailureReason = LyricDockFailureReason.RegistryOrRefreshFailed;
                        StartupRecoveryPending?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    startupRecoveryPending = false;
                    if (!requestedEnabled || !Configure(true, screenName, alignment))
                    {
                        return;
                    }

                    RuntimeRecovered?.Invoke(this, EventArgs.Empty);
                });
            }, TaskScheduler.Default);
        }

        private void PostToUi(Action action)
        {
            if (uiContext != null)
            {
                uiContext.Post(ignored => action(), null);
                return;
            }

            action();
        }

        internal Task WaitForWidgetsOperationsAsync()
        {
            lock (widgetsOperationSync) return widgetsOperation;
        }

        private Task<bool> QueueWidgetsOperation(Func<bool> operation)
        {
            lock (widgetsOperationSync)
            {
                var result = widgetsOperation.ContinueWith(
                    ignored => operation(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                widgetsOperation = result.ContinueWith(ignored => { }, TaskScheduler.Default);
                return result;
            }
        }
    }
}
