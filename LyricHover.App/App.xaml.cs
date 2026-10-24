using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly CancellationTokenSource updateCheckCancellation = new CancellationTokenSource();
        private bool updateCheckStarted;

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
                    window.RevealRetractedIslandForRepeatedLaunch();
                }
            };
            activationSignalTimer.Start();
            Dispatcher.BeginInvoke(new Action(BeginUpdateCheck), DispatcherPriority.ApplicationIdle);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            updateCheckCancellation.Cancel();
            activationSignalTimer?.Stop();
            instanceGuard?.Dispose();
            base.OnExit(e);
        }

        private void BeginUpdateCheck()
        {
            if (updateCheckStarted || !(MainWindow is MainWindow owner))
            {
                return;
            }

            if (!owner.IsLoaded)
            {
                RoutedEventHandler loaded = null;
                loaded = (sender, args) =>
                {
                    owner.Loaded -= loaded;
                    BeginUpdateCheck();
                };
                owner.Loaded += loaded;
                return;
            }

            updateCheckStarted = true;
            _ = CheckForAvailableUpdateAsync(owner);
        }

        private async Task CheckForAvailableUpdateAsync(Window owner)
        {
            try
            {
                var currentVersion = typeof(App).Assembly.GetName().Version;
                var locale = UpdateLocale(UiLanguageService.EffectiveLanguage);
                var storeVersionTask = StoreAppUpdateAvailability.GetLatestAvailableVersionAsync();
                var releaseTask = new AppUpdateChecker().CheckAsync(
                    currentVersion,
                    locale,
                    updateCheckCancellation.Token);
                await Task.WhenAll(storeVersionTask, releaseTask);
                var storeVersion = storeVersionTask.Result;
                var release = releaseTask.Result;
                if (release != null &&
                    storeVersion != null &&
                    release.Version == storeVersion &&
                    !updateCheckCancellation.IsCancellationRequested &&
                    owner.IsLoaded)
                {
                    new UpdateAvailableWindow(owner, release, locale).ShowDialog();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (HttpRequestException)
            {
            }
            catch (Exception)
            {
                // Update discovery is best effort and must never prevent the lyrics island from starting.
            }
        }

        private static string UpdateLocale(AppLanguagePreference language)
        {
            switch (language)
            {
                case AppLanguagePreference.TraditionalChinese:
                    return "zh-Hant";
                case AppLanguagePreference.Japanese:
                    return "ja";
                case AppLanguagePreference.English:
                    return "en";
                default:
                    return "zh";
            }
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
