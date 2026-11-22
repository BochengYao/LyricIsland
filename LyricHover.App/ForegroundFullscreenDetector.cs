using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace LyricHover.App
{
    public static class ForegroundFullscreenDetector
    {
        public static bool IsForegroundWindowFullscreen(string screenName, IntPtr excludedWindow)
        {
            try
            {
                var foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero ||
                    foreground == excludedWindow ||
                    foreground == GetShellWindow() ||
                    IsDesktopWindow(foreground) ||
                    !GetWindowRect(foreground, out var rect))
                {
                    return false;
                }

                var targetScreen = ResolveScreen(screenName);
                var foregroundScreen = Forms.Screen.FromHandle(foreground);
                if (targetScreen == null || foregroundScreen == null ||
                    !string.Equals(foregroundScreen.DeviceName, targetScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var bounds = targetScreen.Bounds;
                return CoversScreen(
                    rect.Left,
                    rect.Top,
                    rect.Right,
                    rect.Bottom,
                    bounds.Left,
                    bounds.Top,
                    bounds.Right,
                    bounds.Bottom);
            }
            catch
            {
                // Environment probing must never make the island disappear when the
                // foreground window or monitor cannot be inspected reliably.
                return false;
            }
        }

        public static bool CoversScreen(
            int windowLeft,
            int windowTop,
            int windowRight,
            int windowBottom,
            int screenLeft,
            int screenTop,
            int screenRight,
            int screenBottom)
        {
            return windowLeft <= screenLeft &&
                windowTop <= screenTop &&
                windowRight >= screenRight &&
                windowBottom >= screenBottom;
        }

        private static bool IsDesktopWindow(IntPtr window)
        {
            var className = new StringBuilder(256);
            GetClassName(window, className, className.Capacity);
            return IsDesktopWindowClass(className.ToString());
        }

        public static bool IsDesktopWindowClass(string className)
        {
            // Explorer can foreground WorkerW while editing a desktop item's name.
            // It covers the monitor but is still the desktop, not fullscreen content.
            return string.Equals(className, "Progman", StringComparison.Ordinal) ||
                string.Equals(className, "WorkerW", StringComparison.Ordinal);
        }

        private static Forms.Screen ResolveScreen(string screenName)
        {
            if (!string.IsNullOrWhiteSpace(screenName))
            {
                var configured = Forms.Screen.AllScreens.FirstOrDefault(screen =>
                    string.Equals(screen.DeviceName, screenName, StringComparison.OrdinalIgnoreCase));
                if (configured != null)
                {
                    return configured;
                }
            }

            return Forms.Screen.PrimaryScreen;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
