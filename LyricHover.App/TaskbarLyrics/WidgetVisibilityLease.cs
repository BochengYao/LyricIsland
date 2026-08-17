using System;
using System.IO;
using LyricHover.Core;

namespace LyricHover.App.TaskbarLyrics
{
    public sealed class WidgetVisibilityLease : IDisposable
    {
        private readonly ITaskbarEnvironment environment;
        private readonly string recoveryPath;
        private bool acquired;

        public WidgetVisibilityLease(ITaskbarEnvironment environment, string recoveryPath)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.recoveryPath = recoveryPath ?? throw new ArgumentNullException(nameof(recoveryPath));
        }

        public bool RestoreResidualLease()
        {
            if (!TryReadRecovery(out var original, out var exists)) return false;
            if (!exists) return true;
            if (!environment.TryWriteTaskbarDa(original) || !environment.TryRefreshTaskbarAndVerify(original)) return false;
            return TryDeleteRecovery();
        }

        public bool TryAcquire()
        {
            if (acquired) return true;
            if (!environment.TryReadTaskbarDa(out var original) || !TryWriteRecovery(original)) return false;
            if (!environment.TryWriteTaskbarDa(TaskbarDaValueState.Disabled) || !environment.TryRefreshTaskbarAndVerify(TaskbarDaValueState.Disabled))
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
