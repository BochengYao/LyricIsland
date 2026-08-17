using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LyricHover.App.TaskbarLyrics
{
    // All shell access remains current-user and replaceable for tests.  No policy keys,
    // elevation, Explorer restart, or injected Explorer code is used.
    public sealed class WindowsTaskbarEnvironment : ITaskbarEnvironment, IDisposable
    {
        private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string WidgetsValueName = "TaskbarDa";
        private const uint WmSettingChange = 0x001A;
        private const uint SmtoAbortIfHung = 0x0002;
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);
        private readonly Forms.Timer changeTimer;
        private readonly Dictionary<string, WidgetAnchor> widgetAnchors = new Dictionary<string, WidgetAnchor>(StringComparer.OrdinalIgnoreCase);

        public WindowsTaskbarEnvironment()
        {
            changeTimer = new Forms.Timer { Interval = 1500 };
            changeTimer.Tick += (sender, args) => Changed?.Invoke(this, EventArgs.Empty);
            changeTimer.Start();
        }

        public bool IsWindows11 => Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
        public event EventHandler Changed;

        public bool TryGetPlacement(string screenName, TaskbarLyricsAlignment alignment, out TaskbarLyricsPlacement placement, out TaskbarLyricsFailureReason failureReason)
        {
            placement = null;
            failureReason = TaskbarLyricsFailureReason.None;
            if (!IsWindows11) { failureReason = TaskbarLyricsFailureReason.Windows11Required; return false; }
            if (!TryFindTaskbar(screenName, out var taskbar, out var screen)) { failureReason = TaskbarLyricsFailureReason.TaskbarNotFound; return false; }
            if (!IsWindowVisible(taskbar) || IsTaskbarAutoHidden() || IsForegroundFullscreen(screen, taskbar))
            {
                failureReason = TaskbarLyricsFailureReason.TaskbarAutoHiddenOrFullscreen;
                return false;
            }
            if (!GetWindowRect(taskbar, out var taskbarRect) || taskbarRect.Width <= taskbarRect.Height || taskbarRect.Height < 24 || taskbarRect.Height > 180)
            {
                failureReason = TaskbarLyricsFailureReason.TaskbarNotFound;
                return false;
            }

            var targetScreen = string.IsNullOrWhiteSpace(screenName) ? screen.DeviceName : screenName;
            if (!TryGetWidgetsAnchor(taskbar, targetScreen, out var widgets))
            {
                failureReason = TaskbarLyricsFailureReason.WidgetsNotFound;
                return false;
            }
            if (!TryGetOccupiedIntervals(taskbar, taskbarRect, widgets, out var occupied))
            {
                failureReason = TaskbarLyricsFailureReason.TaskbarChanged;
                return false;
            }
            if (!TrySelectSafeGap(taskbarRect, widgets.Bounds, occupied, alignment, out var gap))
            {
                failureReason = TaskbarLyricsFailureReason.InsufficientSafeSpace;
                return false;
            }

            placement = new TaskbarLyricsPlacement
            {
                Left = gap.Left,
                Top = taskbarRect.Top,
                Width = Math.Min(TaskbarLyricsController.MaximumWidth, gap.Width),
                Height = taskbarRect.Height,
                DpiScale = GetDpiScale(taskbar),
                IsLeftAligned = alignment == TaskbarLyricsAlignment.Left,
                IsVisible = true,
                IsFullscreenCovered = false,
                IsDarkTheme = IsTaskbarDark(),
                TaskbarBounds = taskbarRect.ToBounds(),
                WidgetsBounds = widgets.Bounds
            };
            return true;
        }

        public bool TryReadTaskbarDa(out TaskbarDaValueState state)
        {
            state = TaskbarDaValueState.Absent;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, false);
                if (key == null || key.GetValue(WidgetsValueName, null) == null) return true;
                state = Convert.ToInt32(key.GetValue(WidgetsValueName)) == 0 ? TaskbarDaValueState.Disabled : TaskbarDaValueState.Enabled;
                return true;
            }
            catch { return false; }
        }

        public bool TryWriteTaskbarDa(TaskbarDaValueState state)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(ExplorerAdvancedKey, true);
                if (key == null) return false;
                if (state == TaskbarDaValueState.Absent) key.DeleteValue(WidgetsValueName, false);
                else key.SetValue(WidgetsValueName, state == TaskbarDaValueState.Enabled ? 1 : 0, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public bool TryRefreshTaskbarAndVerify(TaskbarDaValueState expectedState)
        {
            try
            {
                var result = IntPtr.Zero;
                if (SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveShell", SmtoAbortIfHung, 1000, out result) == IntPtr.Zero) return false;
                var deadline = Stopwatch.StartNew();
                while (deadline.Elapsed < TimeSpan.FromSeconds(3))
                {
                    if (TryReadTaskbarDa(out var actual) && actual == expectedState && VerifyWidgetsVisualState(expectedState)) return true;
                    Thread.Sleep(100);
                }
                return false;
            }
            catch { return false; }
        }

        public void Dispose() { changeTimer.Stop(); changeTimer.Dispose(); }

        private bool VerifyWidgetsVisualState(TaskbarDaValueState expectedState)
        {
            // The disabling transition must be observed in UI Automation; a successful setting-change
            // broadcast alone is deliberately not accepted as a refresh success.
            if (expectedState != TaskbarDaValueState.Disabled) return true;
            // A residual lease can predate this process, so no prior Widgets rectangle is available.
            // In that restoration-only case the exact HKCU state was already read above; never pretend
            // to have observed a disappearance that this process could not anchor.
            if (widgetAnchors.Count == 0) return true;
            foreach (var anchor in widgetAnchors.Values.ToArray())
            {
                if (!IsWindow(anchor.TaskbarHandle) || TryFindWidgetsBounds(anchor.TaskbarHandle, out _)) return false;
            }
            return widgetAnchors.Count > 0;
        }

        private bool TryGetWidgetsAnchor(IntPtr taskbar, string screenName, out WidgetAnchor anchor)
        {
            if (TryFindWidgetsBounds(taskbar, out var bounds))
            {
                anchor = new WidgetAnchor(taskbar, bounds);
                widgetAnchors[screenName] = anchor;
                return true;
            }
            if (TryReadTaskbarDa(out var state) && state == TaskbarDaValueState.Disabled &&
                widgetAnchors.TryGetValue(screenName, out anchor) && anchor.TaskbarHandle == taskbar && IsWindow(taskbar)) return true;
            anchor = null;
            return false;
        }

        private static bool TryFindWidgetsBounds(IntPtr taskbar, out TaskbarBounds bounds)
        {
            bounds = null;
            try
            {
                var root = AutomationElement.FromHandle(taskbar);
                if (root == null) return false;
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
                {
                    var id = element.Current.AutomationId ?? string.Empty;
                    var name = element.Current.Name ?? string.Empty;
                    var className = element.Current.ClassName ?? string.Empty;
                    if (id.IndexOf("widget", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("widgets", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("小组件", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("ウィジェット", StringComparison.OrdinalIgnoreCase) < 0 &&
                        className.IndexOf("widget", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width < 12 || rect.Height < 12) continue;
                    bounds = new TaskbarBounds { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetOccupiedIntervals(IntPtr taskbar, NativeRect taskbarRect, WidgetAnchor widgets, out List<Interval> intervals)
        {
            intervals = new List<Interval>();
            try
            {
                var root = AutomationElement.FromHandle(taskbar);
                if (root == null) return false;
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
                {
                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width < 8 || rect.Height < 8 || rect.Width >= taskbarRect.Width - 2 || rect.Height > taskbarRect.Height + 4) continue;
                    if (rect.Left < taskbarRect.Left - 2 || rect.Right > taskbarRect.Right + 2 || rect.Bottom < taskbarRect.Top - 2 || rect.Top > taskbarRect.Bottom + 2) continue;
                    var controlType = element.Current.ControlType;
                    if (controlType != ControlType.Button && controlType != ControlType.Edit && controlType != ControlType.ListItem && controlType != ControlType.MenuItem && controlType != ControlType.TabItem) continue;
                    if (Intersects(rect.Left, rect.Right, widgets.Bounds.Left, widgets.Bounds.Right)) continue;
                    intervals.Add(new Interval(Math.Max(taskbarRect.Left, rect.Left), Math.Min(taskbarRect.Right, rect.Right)));
                }
                intervals.Add(new Interval(taskbarRect.Left, taskbarRect.Left + 2));
                intervals.Add(new Interval(taskbarRect.Right - 2, taskbarRect.Right));
                intervals = Merge(intervals);
                return true;
            }
            catch { return false; }
        }

        private static bool TrySelectSafeGap(NativeRect taskbar, TaskbarBounds widgets, List<Interval> occupied, TaskbarLyricsAlignment alignment, out Interval selected)
        {
            selected = default;
            var bounds = occupied.Select(item => new TaskbarBounds { Left = item.Left, Right = item.Right, Top = taskbar.Top, Bottom = taskbar.Bottom });
            if (!TaskbarSafeSlotCalculator.TrySelect(taskbar.ToBounds(), widgets, bounds, alignment, out var safeBounds)) return false;
            selected = new Interval(safeBounds.Left, safeBounds.Right);
            return true;
        }

        private static List<Interval> Merge(IEnumerable<Interval> intervals)
        {
            var merged = new List<Interval>();
            foreach (var current in intervals.OrderBy(item => item.Left))
            {
                if (merged.Count == 0 || current.Left > merged[merged.Count - 1].Right + 2) merged.Add(current);
                else merged[merged.Count - 1] = new Interval(merged[merged.Count - 1].Left, Math.Max(merged[merged.Count - 1].Right, current.Right));
            }
            return merged;
        }

        private static bool TryFindTaskbar(string screenName, out IntPtr taskbar, out Forms.Screen screen)
        {
            var handle = IntPtr.Zero;
            Forms.Screen foundScreen = null;
            var expected = string.IsNullOrWhiteSpace(screenName) ? Forms.Screen.PrimaryScreen.DeviceName : screenName;
            EnumWindows((candidate, parameter) =>
            {
                var className = GetClassName(candidate);
                if (className != "Shell_TrayWnd" && className != "Shell_SecondaryTrayWnd") return true;
                var candidateScreen = Forms.Screen.FromHandle(candidate);
                if (!string.Equals(candidateScreen.DeviceName, expected, StringComparison.OrdinalIgnoreCase)) return true;
                handle = candidate; foundScreen = candidateScreen; return false;
            }, IntPtr.Zero);
            taskbar = handle; screen = foundScreen;
            return taskbar != IntPtr.Zero && screen != null;
        }

        private static bool IsForegroundFullscreen(Forms.Screen screen, IntPtr taskbar)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == taskbar || Forms.Screen.FromHandle(foreground).DeviceName != screen.DeviceName || !GetWindowRect(foreground, out var rect)) return false;
            var bounds = screen.Bounds;
            return rect.Left <= bounds.Left + 2 && rect.Top <= bounds.Top + 2 && rect.Right >= bounds.Right - 2 && rect.Bottom >= bounds.Bottom - 2;
        }

        private static bool IsTaskbarAutoHidden() => (SHAppBarMessage(4, IntPtr.Zero).ToInt64() & 1) != 0;
        private static bool IsTaskbarDark()
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false); return Convert.ToInt32(key?.GetValue("SystemUsesLightTheme", 0)) == 0; }
            catch { return true; }
        }
        private static double GetDpiScale(IntPtr hwnd) => GetDpiForWindow(hwnd) / 96.0;
        private static bool Intersects(double left, double right, double otherLeft, double otherRight) => left < otherRight && right > otherLeft;
        private static string GetClassName(IntPtr hwnd) { var buffer = new System.Text.StringBuilder(256); return GetClassNameNative(hwnd, buffer, buffer.Capacity) == 0 ? string.Empty : buffer.ToString(); }

        private sealed class WidgetAnchor { public WidgetAnchor(IntPtr handle, TaskbarBounds bounds) { TaskbarHandle = handle; Bounds = bounds; } public IntPtr TaskbarHandle { get; } public TaskbarBounds Bounds { get; } }
        private readonly struct Interval { public Interval(double left, double right) { Left = left; Right = right; } public double Left { get; } public double Right { get; } public double Width => Right - Left; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; public int Width => Right - Left; public int Height => Bottom - Top; public TaskbarBounds ToBounds() => new TaskbarBounds { Left = Left, Top = Top, Right = Right, Bottom = Bottom }; }
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameNative(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("shell32.dll")] private static extern IntPtr SHAppBarMessage(uint message, IntPtr data);
    }
}
