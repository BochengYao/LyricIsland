using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using LyricHover.App;
using LyricHover.App.LyricDock;
using LyricHover.Core;

namespace LyricHover.Tests
{
    partial class Program
    {
        static void RunRenameTranslationTests(TestSuite suite)
        {
            suite.Run("translation switches release single line and persist independently", TranslationSwitchesReleaseSingleLine);
            suite.Run("desktop hosts are not fullscreen content during rename", DesktopHostsAreNotFullscreenContent);
            suite.Run("Widgets refresh only notifies taskbars and bounds retries", WidgetsRefreshOnlyNotifiesTaskbars);
            suite.Run("transient shell probes preserve the dock lease and recover", TransientShellProbesPreserveDockLease);
        }

        static void TranslationSwitchesReleaseSingleLine()
        {
            var settings = new OverlayPlacementSettings();
            OverlayPlacementSettings committed = null;
            var window = new PlacementSettingsWindow(
                new List<OverlayScreenArea>(), settings, draft => committed = draft.DeepClone());
            try
            {
                foreach (var prefix in new[] { "", "Dock" })
                {
                    var translation = (CheckBox)window.FindName(prefix + "ShowTranslationCheckBox");
                    var single = (RadioButton)window.FindName(prefix + "SingleLineRadioButton");
                    translation.IsChecked = false;
                    single.IsChecked = true;
                    window.ApplyPendingChangesForTutorial();
                    var capture = typeof(PlacementSettingsWindow).GetMethod("CaptureSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                    var snapshot = (OverlayPlacementSettings)capture.Invoke(window, new object[] { true });
                    Assert.False(prefix == "" ? snapshot.ShowTranslation : snapshot.LyricDockShowTranslation);
                    Assert.False(prefix == "" ? snapshot.UseMultiLineDisplay : snapshot.LyricDockUseMultiLineDisplay);
                    Assert.False(translation.IsChecked == true);
                    Assert.True(single.IsChecked == true);
                    // Selecting single line directly must also release translation, for
                    // mouse, keyboard and automation paths alike.
                    translation.IsChecked = true;
                    Assert.False(single.IsChecked == true);
                    single.IsChecked = true;
                    window.ApplyPendingChangesForTutorial();
                    var restarted = JsonSerializer.Deserialize<OverlayPlacementSettings>(JsonSerializer.Serialize(committed));
                    restarted.Normalize();
                    Assert.False(prefix == "" ? restarted.ShowTranslation : restarted.LyricDockShowTranslation);
                    Assert.False(prefix == "" ? restarted.UseMultiLineDisplay : restarted.LyricDockUseMultiLineDisplay);
                    var lyrics = LyricsPackageParser.Parse("[00:01.00]hello\n[00:02.00]next\n" +
                        LyricsPackageParser.TranslationSeparator + "\n[00:01.00]你好");
                    var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(1), TimeSpan.Zero,
                        prefix == "" ? restarted.UseMultiLineDisplay : restarted.LyricDockUseMultiLineDisplay,
                        prefix == "" ? restarted.ShowTranslation : restarted.LyricDockShowTranslation);
                    Assert.Equal(1, lines.Count);
                    Assert.Equal("hello", lines[0].Text);
                }
            }
            finally { window.Close(); }
        }

        static void DesktopHostsAreNotFullscreenContent()
        {
            Assert.True(ForegroundFullscreenDetector.IsDesktopWindowClass("WorkerW"));
            Assert.True(ForegroundFullscreenDetector.IsDesktopWindowClass("Progman"));
            Assert.False(ForegroundFullscreenDetector.IsDesktopWindowClass("CabinetWClass"));
            Assert.False(ForegroundFullscreenDetector.IsDesktopWindowClass("Chrome_WidgetWin_1"));
            Assert.False(ForegroundFullscreenDetector.IsDesktopWindowClass(null));
        }

        static void WidgetsRefreshOnlyNotifiesTaskbars()
        {
            var source = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricHover.App", "LyricDock", "WindowsLyricDockEnvironment.cs"));
            Assert.False(source.Contains("HwndBroadcast"));
            Assert.False(source.Contains("new IntPtr(0xffff)"));
            Assert.True(source.Contains("SendMessageTimeout(taskbar, WmSettingChange"));
            Assert.True(source.Contains("!notificationOk && !notificationRetried"));
        }

        static void TransientShellProbesPreserveDockLease()
        {
            var environment = new FakeLyricDockEnvironment { TaskbarDa = TaskbarDaValueState.Disabled };
            var surface = new FakeTaskbarSurface();
            var recoveryPath = Path.Combine(Path.GetTempPath(), "lyrichover-rename-" + Guid.NewGuid() + ".txt");
            using var controller = new LyricDockController(environment, new WidgetVisibilityLease(environment, recoveryPath), surface);
            var disabled = false;
            controller.FeatureDisabled += (sender, reason) => disabled = true;
            Assert.True(controller.Configure(true, "DISPLAY1", LyricDockAlignment.Center));
            var probe = typeof(LyricDockController).GetMethod("ApplyPlacementProbe", BindingFlags.Instance | BindingFlags.NonPublic);
            for (var index = 0; index < 3; index++)
            {
                probe.Invoke(controller, new object[] { false, null, LyricDockFailureReason.TaskbarChanged, false });
                Assert.True(controller.IsEnabled);
                Assert.False(surface.IsVisible);
            }
            probe.Invoke(controller, new object[] { true, environment.Placement, LyricDockFailureReason.None, false });
            Assert.True(surface.IsVisible);
            Assert.False(disabled);
            Assert.Equal(0, environment.TaskbarDaWriteCalls);
            Assert.Equal(0, environment.RefreshCalls);
        }
    }
}
