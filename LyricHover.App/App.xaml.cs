using System.Windows;
using System.Windows.Threading;
using LyricHover.Core;

namespace LyricHover.App
{
    public partial class App : Application
    {
        private SingleInstanceGuard instanceGuard;
        private DispatcherTimer activationSignalTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
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
    }
}
