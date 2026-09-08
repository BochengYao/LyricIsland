using System;
using System.IO;
using LyricHover.Core;

namespace LyricHover.App.LyricDock
{
    public sealed class WidgetVisibilityLease : IDisposable
    {
        private readonly ILyricDockEnvironment environment;
        private readonly string recoveryPath;
        private bool acquired;

        public WidgetVisibilityLease(ILyricDockEnvironment environment, string recoveryPath)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.recoveryPath = recoveryPath ?? throw new ArgumentNullException(nameof(recoveryPath));
        }

        public bool RestoreResidualLease(string screenName)
        {
            if (!TryReadRecovery(out var original, out var exists)) return false;
            if (!exists) return true;
            var observationReady = environment.TryPrepareWidgetsRestore(screenName);
            if (!environment.TryWriteTaskbarDa(original)) return false;
            if (!observationReady || !environment.TryRefreshTaskbarAndVerify(original)) return false;
            return TryDeleteRecovery();
        }

        public bool TryAcquire(string screenName = null)
        {
            if (acquired)
            {
                // The user can re-enable Widgets from Windows Settings while the dock is
                // active.  A lease is not proof that the shell still honors it: reassert the
                // hidden state without replacing the original recovery value.
                if (!environment.TryReadTaskbarDa(out var current)) return false;
                if (current == TaskbarDaValueState.Disabled) return true;
                if (!environment.TryWriteTaskbarDa(TaskbarDaValueState.Disabled) ||
                    !environment.TryReadTaskbarDa(out var actual) ||
                    actual != TaskbarDaValueState.Disabled ||
                    !environment.TryRefreshTaskbarAndVerify(TaskbarDaValueState.Disabled)) return false;
                return true;
            }
            // A previous attempt or process may have changed Widgets and left the
            // original value on disk. Restore that evidence before starting a new
            // lease; never replace it with the currently suppressed value.
            if (HasRecovery() && !TryRestore()) return false;
            if (!environment.TryReadTaskbarDa(out var original)) return false;
            // Capture the taskbar before changing TaskbarDa.  New Windows builds can expose
            // Widgets through a different UIA subtree; the environment preserves a taskbar
            // anchor even in that case so a successful registry write can still be verified.
            if (!environment.TryPrepareWidgetsRestore(screenName)) return false;
            if (original == TaskbarDaValueState.Disabled)
            {
                // The user already hid Widgets. Do not wait for an Explorer/UIA refresh or
                // show the settings-consent dialog merely because the visual probe is late.
                acquired = true;
                return true;
            }
            if (!TryWriteRecovery(original)) return false;
            // Single write attempt: never delete the value as a substitute for writing 0 —
            // a machine that blocks writes cannot restore the original value afterwards.
            var wrote = environment.TryWriteTaskbarDa(TaskbarDaValueState.Disabled);
            // Verify based on the actual registry state (set-value and delete-value both hide
            // Widgets, but the registry ends up in a different state in each case).
            var actualState = TaskbarDaValueState.Disabled;
            if (!environment.TryReadTaskbarDa(out actualState)) actualState = TaskbarDaValueState.Disabled;
            if (!wrote || actualState == original)
            {
                // The registry write was blocked and nothing changed.  Widgets cannot be
                // hidden on this machine (observed on Win11 25H2 with registry write
                // protection); fail fast instead of burning the full verification timeout.
                // The registry already matches the original state, so only drop the recovery
                // file — restoring would fail for the same reason and strand the file.
                TryDeleteRecovery();
                return false;
            }
            // When the delete-fallback was used, actualState is Absent; forceHide tells the
            // verifier that widgets must be hidden regardless of the Absent state semantics.
            if (!environment.TryRefreshTaskbarAndVerify(actualState, forceHide: actualState == TaskbarDaValueState.Absent))
            {
                TryRestore();
                return false;
            }
            acquired = true;
            return true;
        }

        // This path is deliberately separate from TryAcquire: opening and automating the
        // Windows Settings page is only allowed after the user has approved it.
        public bool TryAcquireThroughSettingsUi(string screenName = null)
        {
            if (acquired)
            {
                // A user can re-enable Widgets after this lease was acquired.  Do not treat
                // the in-memory lease as proof that the system setting is still Off: this is
                // the user-approved fallback, so it must actually open Settings and toggle.
                if (!environment.TryReadTaskbarDa(out var current)) return false;
                if (current == TaskbarDaValueState.Disabled) return true;
                return environment.TryPrepareWidgetsRestore(screenName) &&
                    environment.TryDisableWidgetsThroughSettingsUi() &&
                    environment.TryReadTaskbarDa(out var updatedState) &&
                    updatedState == TaskbarDaValueState.Disabled &&
                    environment.TryRefreshTaskbarAndVerify(TaskbarDaValueState.Disabled);
            }
            if (HasRecovery() && !TryRestore()) return false;
            if (!environment.TryReadTaskbarDa(out var original)) return false;
            if (!environment.TryPrepareWidgetsRestore(screenName)) return false;
            if (original == TaskbarDaValueState.Disabled)
            {
                acquired = environment.TryRefreshTaskbarAndVerify(TaskbarDaValueState.Disabled);
                return acquired;
            }
            if (!TryWriteRecovery(original)) return false;
            if (!environment.TryDisableWidgetsThroughSettingsUi() ||
                !environment.TryReadTaskbarDa(out var actual) ||
                actual != TaskbarDaValueState.Disabled ||
                !environment.TryRefreshTaskbarAndVerify(TaskbarDaValueState.Disabled))
            {
                // The Settings helper may have changed the system even when its
                // final read or visual verification fails. Only verified restore
                // is allowed to remove the original-state evidence.
                TryRestore();
                return false;
            }
            acquired = true;
            return true;
        }

        public bool TryRestore()
        {
            if (!TryReadRecovery(out var restore, out var exists)) return false;
            if (!exists) { acquired = false; return true; }
            if (!environment.TryWriteTaskbarDa(restore) || !environment.TryRefreshTaskbarAndVerify(restore) || !TryDeleteRecovery()) return false;
            acquired = false;
            return true;
        }

        public void Dispose() { TryRestore(); }

        private bool HasRecovery()
        {
            try { return File.Exists(recoveryPath); }
            catch { return true; }
        }

        private bool TryReadRecovery(out TaskbarDaValueState state, out bool exists)
        {
            state = TaskbarDaValueState.Absent;
            exists = false;
            try
            {
                if (!File.Exists(recoveryPath)) return true;
                exists = true;
                return Enum.TryParse(File.ReadAllText(recoveryPath), out state);
            }
            catch { return false; }
        }

        private bool TryWriteRecovery(TaskbarDaValueState state)
        {
            try
            {
                var directory = Path.GetDirectoryName(recoveryPath);
                if (string.IsNullOrWhiteSpace(directory)) return false;
                Directory.CreateDirectory(directory);
                AtomicFileWriter.WriteAllText(recoveryPath, state.ToString());
                return true;
            }
            catch { return false; }
        }

        private bool TryDeleteRecovery()
        {
            try
            {
                if (File.Exists(recoveryPath)) File.Delete(recoveryPath);
                return true;
            }
            catch { return false; }
        }
    }
}


