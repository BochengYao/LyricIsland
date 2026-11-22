using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using LyricHover.App;
using LyricHover.App.LyricDock;
using LyricHover.App.Modules;
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
            suite.Run("island rendering uses island lines while dock translation is enabled", IslandRenderingUsesIndependentLyrics);
            suite.Run("visible dock restores z order on unchanged placement probes", VisibleDockRestoresZOrderOnUnchangedPlacement);
            suite.Run("native dock recovers above a raised window without taking focus", NativeDockRecoversZOrderWithoutTakingFocus);
            suite.Run("single island lyric centers vertically after removing the secondary row", SingleIslandLyricCentersVertically);
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

        static void SingleIslandLyricCentersVertically()
        {
            var view = new LyricsModuleView { AnimationsEnabled = false };
            var snapshotType = typeof(LyricsModuleView).Assembly.GetType(typeof(LyricsPresentationSnapshot).FullName);
            var snapshot = Activator.CreateInstance(snapshotType);
            var projection = snapshotType.GetMethod("ToIslandRenderState", new[] { typeof(string), typeof(string) });
            var update = typeof(LyricsModuleView).GetMethod("Update");
            foreach (var fontSize in new[] { 15d, 24d, 32d })
            {
                view.ApplyAppearance(new OverlayPlacementSettings { IslandLyricsFontSize = fontSize });
                foreach (var secondary in new[] { "", "下一句 / translation", "" })
                {
                    update.Invoke(view, new[] { projection.Invoke(snapshot, new object[] { "English gyjp / 中文歌词", secondary }) });
                    view.Measure(new System.Windows.Size(view.Width, view.Height));
                    view.Arrange(new System.Windows.Rect(0, 0, view.Width, view.Height));
                    view.UpdateLayout();
                    var primary = (System.Windows.FrameworkElement)view.FindName("PrimaryLyricClip");
                    var secondaryClip = (System.Windows.FrameworkElement)view.FindName("SecondaryLyricClip");
                    if (secondary.Length == 0)
                    {
                        var primaryTop = primary.TransformToAncestor(view).Transform(new System.Windows.Point()).Y;
                        Assert.True(Math.Abs(primaryTop + primary.ActualHeight / 2 - view.ActualHeight / 2) <= 1);
                        Assert.Equal(System.Windows.Visibility.Collapsed, secondaryClip.Visibility);
                    }
                    else
                    {
                        Assert.Equal(System.Windows.Visibility.Visible, secondaryClip.Visibility);
                        Assert.True(secondaryClip.ActualHeight > 0);
                    }
                }
            }
            // The incoming animation layer must also stop reserving an empty row.
            view.AnimationsEnabled = true;
            update.Invoke(view, new[] { projection.Invoke(snapshot, new object[] { "new line", "" }) });
            var incoming = (System.Windows.FrameworkElement)view.FindName("IncomingSecondaryLyricClip");
            Assert.Equal(System.Windows.Visibility.Collapsed, incoming.Visibility);
        }

        static void IslandRenderingUsesIndependentLyrics()
        {
            var lyrics = LyricsPackageParser.Parse("[00:01.00]hello\n[00:02.00]next\n" +
                LyricsPackageParser.TranslationSeparator + "\n[00:01.00]你好\n[00:02.00]下一句");
            var view = new LyricsModuleView { AnimationsEnabled = false };
            foreach (var islandMultiline in new[] { false, true })
            {
                foreach (var islandTranslation in new[] { false, true })
                {
                    foreach (var dockTranslation in new[] { false, true })
                    {
                        var islandLines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(1), TimeSpan.Zero,
                            islandMultiline, islandTranslation);
                        var dockLines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(1), TimeSpan.Zero,
                            true, dockTranslation);
                        var snapshot = new LyricsPresentationSnapshot
                        {
                            PrimaryText = dockLines[0].Text,
                            SecondaryText = dockLines.Count > 1 ? dockLines[1].Text : "",
                            EffectivePosition = TimeSpan.FromSeconds(1)
                        };
                        var primary = islandLines[0].Text;
                        var secondary = islandLines.Count > 1 ? islandLines[1].Text : "";
                        // Use the production snapshot as well as the production WPF view;
                        // this test project links a separate copy for controller fixtures.
                        var snapshotType = typeof(LyricsModuleView).Assembly.GetType(typeof(LyricsPresentationSnapshot).FullName);
                        var runtimeSnapshot = Activator.CreateInstance(snapshotType);
                        snapshotType.GetProperty("PrimaryText").SetValue(runtimeSnapshot, snapshot.PrimaryText);
                        snapshotType.GetProperty("SecondaryText").SetValue(runtimeSnapshot, snapshot.SecondaryText);
                        snapshotType.GetProperty("EffectivePosition").SetValue(runtimeSnapshot, snapshot.EffectivePosition);
                        var projection = snapshotType.GetMethod("ToIslandRenderState", new[] { typeof(string), typeof(string) });
                        Assert.True(projection != null);
                        var state = projection.Invoke(runtimeSnapshot, new object[] { primary, secondary });
                        typeof(LyricsModuleView).GetMethod("Update").Invoke(view, new[] { state });
                        var renderedSecondary = (TextBlock)view.FindName("SecondaryLyricText");
                        Assert.Equal(secondary, new System.Windows.Documents.TextRange(
                            renderedSecondary.ContentStart, renderedSecondary.ContentEnd).Text);
                        Assert.Equal(secondary.Length > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                            renderedSecondary.Visibility);
                        Assert.Equal(snapshot.SecondaryText, dockLines.Count > 1 ? dockLines[1].Text : "");
                        Assert.Equal(snapshot.EffectivePosition, (TimeSpan)state.GetType().GetProperty("EffectivePosition").GetValue(state));
                    }
                }
            }
            var main = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricHover.App", "MainWindow.xaml.cs"));
            Assert.True(main.Contains("taskbarSnapshot.ToIslandRenderState(currentPrimaryText, currentSecondaryText)"));
        }

        static void VisibleDockRestoresZOrderOnUnchangedPlacement()
        {
            var environment = new FakeLyricDockEnvironment { TaskbarDa = TaskbarDaValueState.Disabled };
            var surface = new FakeTaskbarSurface();
            var recoveryPath = Path.Combine(Path.GetTempPath(), "lyrichover-zorder-" + Guid.NewGuid() + ".txt");
            using var controller = new LyricDockController(environment, new WidgetVisibilityLease(environment, recoveryPath), surface);
            Assert.True(controller.Configure(true, "DISPLAY1", LyricDockAlignment.Center));
            var initialShows = surface.ShowCalls;
            Assert.True(surface.IsVisible);
            var probe = typeof(LyricDockController).GetMethod("ApplyPlacementProbe", BindingFlags.Instance | BindingFlags.NonPublic);
            probe.Invoke(controller, new object[] { true, environment.Placement, LyricDockFailureReason.None, false });
            Assert.Equal(initialShows + 1, surface.ShowCalls);
            Assert.Equal(1, surface.PlaceCalls);
            Assert.Equal(0, environment.TaskbarDaWriteCalls);
        }

        [DllImport("user32.dll", EntryPoint = "GetWindow")]
        static extern IntPtr DockTestGetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        static extern IntPtr DockTestGetForegroundWindow();
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        static extern bool DockTestSetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        static void NativeDockRecoversZOrderWithoutTakingFocus()
        {
            var dockType = typeof(LyricsModuleView).Assembly.GetType("LyricHover.App.LyricDock.LyricDockWindow");
            var dock = (System.Windows.Window)Activator.CreateInstance(dockType, new object[] { null });
            var cover = new System.Windows.Window
            {
                Width = 24, Height = 24, Left = -30000, Top = -30000,
                ShowActivated = false, ShowInTaskbar = false, Topmost = true, Focusable = false
            };
            try
            {
                dock.Left = -30000;
                dock.Top = -30000;
                dock.Width = 360;
                dock.Height = 40;
                var surfaceType = dockType.Assembly.GetType("LyricHover.App.LyricDock.ILyricDockSurface");
                var show = surfaceType.GetMethod("Show");
                var foreground = DockTestGetForegroundWindow();
                show.Invoke(dock, null);
                cover.Show();
                var dockHandle = new System.Windows.Interop.WindowInteropHelper(dock).Handle;
                var coverHandle = new System.Windows.Interop.WindowInteropHelper(cover).Handle;
                Assert.True(DockTestSetWindowPos(coverHandle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002));
                Assert.True(IsDockTestWindowAbove(coverHandle, dockHandle));
                Assert.True(dock.IsVisible); // HWND visibility does not detect occlusion.
                show.Invoke(dock, null);
                Assert.True(IsDockTestWindowAbove(dockHandle, coverHandle));
                Assert.Equal(foreground, DockTestGetForegroundWindow());
            }
            finally { cover.Close(); dock.Close(); }
        }

        static bool IsDockTestWindowAbove(IntPtr expectedAbove, IntPtr other)
        {
            for (var window = DockTestGetWindow(expectedAbove, 2); window != IntPtr.Zero; window = DockTestGetWindow(window, 2))
            {
                if (window == other) return true;
            }
            return false;
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
