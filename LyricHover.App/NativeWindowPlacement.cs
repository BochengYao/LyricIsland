using System;
using System.Linq;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace LyricHover.App
{
    internal static class NativeWindowPlacement
    {
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoZOrder = 0x0004;

        public static bool CenterOnScreen(IntPtr handle, string screenName)
        {
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var windowRect))
            {
                return false;
            }

            var screen = Forms.Screen.AllScreens.FirstOrDefault(candidate =>
                string.Equals(candidate.DeviceName, screenName, StringComparison.OrdinalIgnoreCase))
                ?? Forms.Screen.PrimaryScreen
                ?? Forms.Screen.AllScreens.FirstOrDefault();
            if (screen == null)
            {
                return false;
            }

            var area = screen.WorkingArea;
            var bounds = NativeWindowPlacementMath.CenterInWorkingArea(
                area.Left,
                area.Top,
                area.Width,
                area.Height,
                windowRect.Right - windowRect.Left,
                windowRect.Bottom - windowRect.Top);
            return SetWindowPos(
                handle,
                IntPtr.Zero,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                SwpNoActivate | SwpNoZOrder);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
