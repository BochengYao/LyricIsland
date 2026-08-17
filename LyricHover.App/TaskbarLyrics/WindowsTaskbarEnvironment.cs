using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LyricHover.App.TaskbarLyrics
{
    // Keeps all shell and registry interaction behind a replaceable boundary.  It never
    // uses policies, elevation, Explorer restarts, or a machine-wide registry hive.
    public sealed class WindowsTaskbarEnvironment : ITaskbarEnvironment, IDisposable
    {
        private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string WidgetsValueName = "TaskbarDa";
        private const uint WmSettingChange = 0x001A;
        private const uint SmtoAbortIfHung = 0x0002;
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);
        private readonly Forms.Timer changeTimer;

        public WindowsTaskbarEnvironment()
        {
            changeTimer = new Forms.Timer { Interval = 1500 };
            changeTimer.Tick += (sender, args) => Changed?.Invoke(this, EventArgs.Empty);
            changeTimer.Start();
        }

        public bool IsWindows11 => Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
        public event EventHandler Changed;

        public bool TryGetPlacement(string screenName, out TaskbarLyricsPlacement placement)
        {
            placement = null;
            if (!IsWindows11 || !TryFindTaskbar(screenName, out var taskbar, out var screen)) return false;
            if (!IsWindowVisible(taskbar) || IsTaskbarAutoHidden() || IsForegroundFullscreen(screen, taskbar)) return false;
            if (!GetWindowRect(taskbar, out var rect)) return false;

            var height = rect.Bottom - rect.Top;
            var width = rect.Right - rect.Left;
            // The initial release intentionally supports the normal horizontal taskbar only.
            if (height < 24 || height > 180 || width < 600 || width <= height) return false;

            // Reserve the left Start/search/pinned region and the right tray region.  The controller
            // refuses this placement when the resulting slot cannot safely fit its 220 px minimum.
            const int leftReserved = 520;
            const int rightReserved = 420;
            var safeWidth = width - leftReserved - rightReserved;
            if (safeWidth < TaskbarLyricsController.MinimumWidth) return false;

            placement = new TaskbarLyricsPlacement
            {
                Left = rect.Left + leftReserved,
                Top = rect.Top,
                Width = Math.Min(TaskbarLyricsController.MaximumWidth, safeWidth),
                Height = height,
                DpiScale = GetDpiScale(taskbar),
                IsLeftAligned = false,
                IsVisible = true,
                IsFullscreenCovered = false
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
                var value = Convert.ToInt32(key.GetValue(WidgetsValueName));
                state = value == 0 ? TaskbarDaValueState.Disabled : TaskbarDaValueState.Enabled;
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

        public bool TryRefreshTaskbar()
        {
            try
            {
                var result = IntPtr.Zero;
                return SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveShell", SmtoAbortIfHung, 1000, out result) != IntPtr.Zero;
            }
            catch { return false; }
        }

        public void Dispose() { changeTimer.Stop(); changeTimer.Dispose(); }

        private static bool TryFindTaskbar(string screenName, out IntPtr taskbar, out Forms.Screen screen)
        {
            var foundTaskbar = IntPtr.Zero;
            Forms.Screen foundScreen = null;
            var expected = string.IsNullOrWhiteSpace(screenName) ? Forms.Screen.PrimaryScreen.DeviceName : screenName;
            EnumWindows((handle, parameter) =>
            {
                var className = GetClassName(handle);
                if (className != "Shell_TrayWnd" && className != "Shell_SecondaryTrayWnd") return true;
                var candidate = Forms.Screen.FromHandle(handle);
                if (!string.Equals(candidate.DeviceName, expected, StringComparison.OrdinalIgnoreCase)) return true;
                foundTaskbar = handle;
                foundScreen = candidate;
                return false;
            }, IntPtr.Zero);
            taskbar = foundTaskbar;
            screen = foundScreen;
            return taskbar != IntPtr.Zero && screen != null;
        }

        private static bool IsForegroundFullscreen(Forms.Screen screen, IntPtr taskbar)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == taskbar || Forms.Screen.FromHandle(foreground).DeviceName != screen.DeviceName) return false;
            if (!GetWindowRect(foreground, out var rect)) return false;
            var bounds = screen.Bounds;
            return rect.Left <= bounds.Left + 2 && rect.Top <= bounds.Top + 2 && rect.Right >= bounds.Right - 2 && rect.Bottom >= bounds.Bottom - 2;
        }

        private static bool IsTaskbarAutoHidden() => (SHAppBarMessage(4, IntPtr.Zero).ToInt64() & 1) != 0;
        private static double GetDpiScale(IntPtr hwnd) => GetDpiForWindow(hwnd) / 96.0;
        private static string GetClassName(IntPtr hwnd)
        {
            var buffer = new System.Text.StringBuilder(256);
            return GetClassNameNative(hwnd, buffer, buffer.Capacity) == 0 ? string.Empty : buffer.ToString();
        }

        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameNative(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("shell32.dll")] private static extern IntPtr SHAppBarMessage(uint message, IntPtr data);
    }
}
