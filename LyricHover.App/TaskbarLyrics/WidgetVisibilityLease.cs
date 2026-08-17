using System;
using System.IO;

namespace LyricHover.App.TaskbarLyrics
{
    public sealed class WidgetVisibilityLease : IDisposable
    {
        private readonly ITaskbarEnvironment environment;
        private readonly string recoveryPath;
        private bool acquired;
        private TaskbarDaValueState originalState;

        public WidgetVisibilityLease(ITaskbarEnvironment environment, string recoveryPath)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.recoveryPath = recoveryPath ?? throw new ArgumentNullException(nameof(recoveryPath));
        }

        public bool RestoreResidualLease()
        {
            if (!File.Exists(recoveryPath)) return true;
            var value = File.ReadAllText(recoveryPath);
            if (!Enum.TryParse(value, out TaskbarDaValueState original) ||
                !environment.TryWriteTaskbarDa(original) || !environment.TryRefreshTaskbar()) return false;
            File.Delete(recoveryPath);
            return true;
        }

        public bool TryAcquire()
        {
            if (acquired) return true;
            if (!environment.TryReadTaskbarDa(out originalState)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(recoveryPath));
            File.WriteAllText(recoveryPath, originalState.ToString());
            if (!environment.TryWriteTaskbarDa(TaskbarDaValueState.Disabled) || !environment.TryRefreshTaskbar())
            {
                TryRestore();
                return false;
            }
            acquired = true;
            return true;
        }

        public bool TryRestore()
        {
            if (!File.Exists(recoveryPath)) { acquired = false; return true; }
            var value = File.ReadAllText(recoveryPath);
            if (!Enum.TryParse(value, out TaskbarDaValueState restore) ||
                !environment.TryWriteTaskbarDa(restore) || !environment.TryRefreshTaskbar()) return false;
            File.Delete(recoveryPath);
            acquired = false;
            return true;
        }

        public void Dispose() { TryRestore(); }
    }
}
