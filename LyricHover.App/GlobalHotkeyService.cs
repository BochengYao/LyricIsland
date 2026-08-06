using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace LyricHover.App
{
    public sealed class GlobalHotkeyService : IDisposable
    {
        private const int WmHotkey = 0x0312;
        private readonly IntPtr hwnd;
        private readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkeyService(IntPtr hwnd)
        {
            this.hwnd = hwnd;
        }

        public bool Register(int id, uint modifiers, uint key, Action action)
        {
            if (!RegisterHotKey(hwnd, id, modifiers, key))
            {
                return false;
            }

            actions[id] = action;
            return true;
        }

        public bool HandleMessage(int message, IntPtr wParam)
        {
            Action action;
            if (message != WmHotkey || !actions.TryGetValue(wParam.ToInt32(), out action))
            {
                return false;
            }

            action();
            return true;
        }

        public void Unregister(int id)
        {
            if (!actions.Remove(id))
            {
                return;
            }

            UnregisterHotKey(hwnd, id);
        }

        public void Dispose()
        {
            foreach (var id in actions.Keys.ToArray())
            {
                UnregisterHotKey(hwnd, id);
            }

            actions.Clear();
        }
    }
}
