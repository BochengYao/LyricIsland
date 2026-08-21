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

        public bool TryAcquire()
        {
            if (acquired) return true;
            if (!environment.TryReadTaskbarDa(out var original)) return false;
            if (original == TaskbarDaValueState.Disabled)
            {
                // Widgets are already hidden by the user; nothing to change or restore.
                acquired = environment.TryRefreshTaskbarAndVerify(TaskbarDaValueState.Disabled);
                return acquired;
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

        public bool TryRestore()
        {
            if (!TryReadRecovery(out var restore, out var exists)) return false;
            if (!exists) { acquired = false; return true; }
            if (!environment.TryWriteTaskbarDa(restore) || !environment.TryRefreshTaskbarAndVerify(restore) || !TryDeleteRecovery()) return false;
            acquired = false;
            return true;
        }

        public void Dispose() { TryRestore(); }

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


