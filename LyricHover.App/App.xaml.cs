using System.Windows;
using System.Windows.Threading;
using LyricHover.App.LyricDock;
using LyricHover.Core;

namespace LyricHover.App
{
    public partial class App : Application
    {
        private SingleInstanceGuard instanceGuard;
        private DispatcherTimer activationSignalTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (IsWidgetsSettingsHelper(e.Args))
            {
                // Keep a real STA dispatcher alive while Settings composes and commits its
                // XAML toggle.  The prior one-shot branch blocked startup and exited too
                // soon when it had to open the Settings page itself.
                base.OnStartup(e);
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    var settingsSuccess = WindowsLyricDockEnvironment.TryDisableWidgetsFromStaHelper();
                    Shutdown(settingsSuccess ? 0 : 1);
                }), DispatcherPriority.ApplicationIdle);
                return;
            }

            // The normal app remains unelevated.  This one-shot mode is launched only after
            // a current-user TaskbarDa write was denied and changes precisely that value.
            if (TryHandleElevatedTaskbarWrite(e.Args))
            {
                return;
            }

            instanceGuard = SingleInstanceGuard.TryAcquire("LyricsIsland.DesktopLyrics.SingleInstance");
            if (!instanceGuard.HasHandle)
            {
                instanceGuard.SignalExistingInstance();
                Shutdown();
                return;
            }

            base.OnStartup(e);
            activationSignalTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(400) };
            activationSignalTimer.Tick += (sender, args) =>
            {
                if (instanceGuard.ConsumeActivationSignal(System.TimeSpan.Zero) && MainWindow is MainWindow window)
                {
                    window.ShowWaitingForPlaybackHint();
                }
            };
            activationSignalTimer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            activationSignalTimer?.Stop();
            instanceGuard?.Dispose();
            base.OnExit(e);
        }

        private bool TryHandleElevatedTaskbarWrite(string[] args)
        {
            if (args == null || args.Length != 2 || args[0] != "--lyrichover-taskbar-da")
            {
                return false;
            }

            var success = args[1] == "disabled" &&
                WindowsLyricDockEnvironment.TryWriteTaskbarDaFromElevatedHelper(TaskbarDaValueState.Disabled);
            Shutdown(success ? 0 : 1);
            return true;
        }

        private static bool IsWidgetsSettingsHelper(string[] args) =>
            args?.Length == 1 && args[0] == "--lyrichover-widgets-settings-toggle";
    }
}
