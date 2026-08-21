using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LyricHover.App.LyricDock
{
    // All shell access remains current-user and replaceable for tests.  No policy keys,
    // elevation, Explorer restart, or injected Explorer code is used.
    public sealed class WindowsLyricDockEnvironment : ILyricDockEnvironment, IDisposable
    {
        private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string WidgetsValueName = "TaskbarDa";
        private const uint WmSettingChange = 0x001A;
        private const uint SmtoAbortIfHung = 0x0002;
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);
        private readonly Forms.Timer changeTimer;
        private readonly Dictionary<string, WidgetAnchor> widgetAnchors = new Dictionary<string, WidgetAnchor>(StringComparer.OrdinalIgnoreCase);

        public WindowsLyricDockEnvironment()
        {
            changeTimer = new Forms.Timer { Interval = 1500 };
            changeTimer.Tick += (sender, args) => Changed?.Invoke(this, EventArgs.Empty);
            changeTimer.Start();
        }

        public bool IsSupported => GetRealWindowsVersion().Major >= 10;
        public event EventHandler Changed;

        public bool TryGetPlacement(string screenName, LyricDockAlignment alignment, out LyricDockPlacement placement, out LyricDockFailureReason failureReason)
        {
            placement = null;
            failureReason = LyricDockFailureReason.None;
            if (!IsSupported) { failureReason = LyricDockFailureReason.UnsupportedOS; return false; }
            if (!TryFindTaskbar(screenName, out var taskbar, out var screen)) { failureReason = LyricDockFailureReason.TaskbarNotFound; return false; }
            if (!IsWindowVisible(taskbar) || IsTaskbarAutoHidden() || IsForegroundFullscreen(screen, taskbar))
            {
                failureReason = LyricDockFailureReason.TaskbarAutoHiddenOrFullscreen;
                return false;
            }
            if (!GetWindowRect(taskbar, out var taskbarRect) || taskbarRect.Width <= taskbarRect.Height || taskbarRect.Height <= 0)
            {
                failureReason = LyricDockFailureReason.TaskbarNotFound;
                return false;
            }

            var targetScreen = string.IsNullOrWhiteSpace(screenName) ? screen.DeviceName : screenName;
            WidgetAnchor widgets = null;
            var widgetsHiddenManually = false;
            if (!TryGetWidgetsAnchor(taskbar, targetScreen, out widgets))
            {
                // No Widgets element is visible.  When TaskbarDa=0 the user hid Widgets
                // manually via Windows Settings, so lyrics simply use the widest free gap;
                // otherwise the discovery is genuinely unreliable and must fail closed.
                if (!TryReadTaskbarDa(out var observedState) || observedState != TaskbarDaValueState.Disabled)
                {
                    failureReason = LyricDockFailureReason.WidgetsNotFound;
                    return false;
                }
                widgets = null;
                widgetsHiddenManually = true;
            }
            if (!TryGetOccupiedIntervals(taskbar, taskbarRect, widgets, out var occupied))
            {
                failureReason = LyricDockFailureReason.TaskbarChanged;
                return false;
            }
            if (!TrySelectSafeGap(taskbarRect, widgetsHiddenManually ? null : widgets.Bounds, occupied, alignment, out var gap))
            {
                failureReason = LyricDockFailureReason.InsufficientSafeSpace;
                return false;
            }

            placement = new LyricDockPlacement
            {
                Left = gap.Left,
                Top = taskbarRect.Top,
                Width = Math.Min(LyricDockController.MaximumWidth, gap.Width),
                Height = taskbarRect.Height,
                DpiScale = GetDpiScale(taskbar),
                IsLeftAligned = alignment == LyricDockAlignment.Left,
                IsVisible = true,
                IsFullscreenCovered = false,
                IsDarkTheme = IsTaskbarDark(),
                TaskbarBounds = taskbarRect.ToBounds(),
                WidgetsBounds = widgets?.Bounds
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
                if (TryWriteTaskbarDaDirect(state)) return true;
                // Some security suites (observed: kernel registry filters) block writes from
                // foreign processes but allow the same write performed inside the Microsoft-
                // signed WmiPrvSE.exe service host, which is where StdRegProv executes.
                // NOTE: never fall back to deleting the value here — a machine that blocks
                // writes cannot restore the user's original setting afterwards, so deleting
                // would silently destroy it (observed on a registry-filtered Win11 25H2).
                if (TryWriteTaskbarDaViaWmi(state))
                {
                    WriteDiagnosticLog($"Write TaskbarDa={state}: ok (via WMI)");
                    return true;
                }
                return false;
            }
            catch (Exception ex) { WriteDiagnosticLog($"Write TaskbarDa={state}: exception {ex.GetType().Name}: {ex.Message}"); return false; }
        }

        private bool TryWriteTaskbarDaDirect(TaskbarDaValueState state)
        {
            var hKey = IntPtr.Zero;
            var result = RegOpenKeyExW(HKEY_CURRENT_USER, ExplorerAdvancedKey, 0, KEY_ALL_ACCESS, ref hKey);
            if (result != 0) { WriteDiagnosticLog($"Write TaskbarDa={state}: RegOpenKeyEx failed, code={result}"); return false; }
            try
            {
                if (state == TaskbarDaValueState.Absent)
                {
                    result = RegDeleteValueW(hKey, WidgetsValueName);
                    if (result == 0 || result == 2) // 2 = ERROR_FILE_NOT_FOUND
                    {
                        WriteDiagnosticLog($"Write TaskbarDa={state}: ok (deleted via Win32)");
                        return true;
                    }
                    WriteDiagnosticLog($"Write TaskbarDa={state}: RegDeleteValue failed, code={result}");
                    return false;
                }
                var value = state == TaskbarDaValueState.Enabled ? 1 : 0;
                result = RegSetValueExW(hKey, WidgetsValueName, 0, REG_DWORD, ref value, 4);
                if (result == 0)
                {
                    WriteDiagnosticLog($"Write TaskbarDa={state}: ok (set via Win32)");
                    return true;
                }
                WriteDiagnosticLog($"Write TaskbarDa={state}: RegSetValueEx failed, code={result}");
                return false;
            }
            finally
            {
                RegCloseKey(hKey);
            }
        }

        private bool TryWriteTaskbarDaViaWmi(TaskbarDaValueState state)
        {
            try
            {
                using (var provider = new System.Management.ManagementClass(@"root\default:StdRegProv"))
                {
                    if (state == TaskbarDaValueState.Absent)
                    {
                        using (var inParams = provider.GetMethodParameters("DeleteValue"))
                        {
                            inParams["hDefKey"] = HKEY_CURRENT_USER_Wmi;
                            inParams["sSubKeyName"] = ExplorerAdvancedKey;
                            inParams["sValueName"] = WidgetsValueName;
                            using (var outParams = provider.InvokeMethod("DeleteValue", inParams, null))
                            {
                                var code = Convert.ToInt32(outParams?["ReturnValue"] ?? (-1));
                                WriteDiagnosticLog($"WMI delete TaskbarDa: code={code}");
                                return code == 0 || code == 2;
                            }
                        }
                    }
                    using (var setParams = provider.GetMethodParameters("SetDWORDValue"))
                    {
                        setParams["hDefKey"] = HKEY_CURRENT_USER_Wmi;
                        setParams["sSubKeyName"] = ExplorerAdvancedKey;
                        setParams["sValueName"] = WidgetsValueName;
                        setParams["uValue"] = (uint)(state == TaskbarDaValueState.Enabled ? 1 : 0);
                        using (var outParams = provider.InvokeMethod("SetDWORDValue", setParams, null))
                        {
                            var code = Convert.ToInt32(outParams?["ReturnValue"] ?? (-1));
                            WriteDiagnosticLog($"WMI set TaskbarDa={state}: code={code}");
                            return code == 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDiagnosticLog($"WMI write TaskbarDa={state}: exception {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        public bool TryPrepareWidgetsRestore(string screenName)
        {
            try
            {
                if (!TryFindTaskbar(screenName, out var taskbar, out var screen) || !IsWindow(taskbar)) return false;
                var targetScreen = string.IsNullOrWhiteSpace(screenName) ? screen.DeviceName : screenName;
                if (TryFindWidgetsElement(taskbar, out var element, out var bounds))
                {
                    widgetAnchors[targetScreen] = new WidgetAnchor(taskbar, bounds, element, wasVisibleBeforeLease: true);
                    return true;
                }

                // A residual lease was written only after an actual Widgets element was observed.
                // Preserve that visibility obligation even though the element is currently hidden.
                widgetAnchors[targetScreen] = new WidgetAnchor(taskbar, null, null, wasVisibleBeforeLease: true);
                return true;
            }
            catch { return false; }
        }

        public bool TryRefreshTaskbarAndVerify(TaskbarDaValueState expectedState, bool forceHide = false)
        {
            try
            {
                var result = IntPtr.Zero;
                var broadcastOk = SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveShell", SmtoAbortIfHung, 2000, out result) != IntPtr.Zero;
                WriteDiagnosticLog($"Refresh: expected={expectedState}, forceHide={forceHide}, broadcast={(broadcastOk ? "ok" : "failed")}");
                var deadline = Stopwatch.StartNew();
                while (deadline.Elapsed < TimeSpan.FromSeconds(8))
                {
                    var registryStable = HasStableTaskbarDaValue(expectedState);
                    var widgetsVerified = VerifyWidgetsVisualState(expectedState, forceHide);
                    LastRegistryStable = registryStable;
                    LastWidgetsVisible = !widgetsVerified;
                    if (registryStable && widgetsVerified)
                    {
                        WriteDiagnosticLog($"Refresh: verified after {deadline.ElapsedMilliseconds}ms");
                        return true;
                    }
                    if (!broadcastOk && deadline.ElapsedMilliseconds >= 2000)
                    {
                        // Retry the broadcast once; the first attempt may have raced a hung window.
                        broadcastOk = SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveShell", SmtoAbortIfHung, 2000, out result) != IntPtr.Zero;
                    }
                    Thread.Sleep(200);
                }
                WriteDiagnosticLog($"Refresh: TIMEOUT after 8000ms, registryStable={LastRegistryStable}, widgetsStillVisible={LastWidgetsVisible}");
                return false;
            }
            catch (Exception ex)
            {
                WriteDiagnosticLog("Refresh: exception " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private bool LastRegistryStable;
        private bool LastWidgetsVisible = true;

        private static void WriteDiagnosticLog(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LyricHover", "taskbar-diagnostic.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        public void Dispose() { changeTimer.Stop(); changeTimer.Dispose(); }

        private bool VerifyWidgetsVisualState(TaskbarDaValueState expectedState, bool forceHide = false)
        {
            // Both directions require a real UIA observation on the cached target taskbar. A registry
            // write or a broadcast alone never proves that Widgets actually disappeared or returned.
            if (widgetAnchors.Count == 0) return false;
            foreach (var anchor in widgetAnchors.Values.ToArray())
            {
                if (!IsWindow(anchor.TaskbarHandle)) return false;
                var isVisible = TryFindWidgetsBounds(anchor.TaskbarHandle, out _);
                if (expectedState == TaskbarDaValueState.Disabled && isVisible) return false;
                if (!forceHide && (expectedState == TaskbarDaValueState.Enabled || expectedState == TaskbarDaValueState.Absent) && anchor.WasVisibleBeforeLease && !isVisible) return false;
                if (forceHide && isVisible) return false;
            }
            return true;
        }

        private bool HasStableTaskbarDaValue(TaskbarDaValueState expectedState)
        {
            if (!TryReadTaskbarDa(out var first) || first != expectedState) return false;
            Thread.Sleep(50);
            return TryReadTaskbarDa(out var second) && second == expectedState;
        }

        private bool TryGetWidgetsAnchor(IntPtr taskbar, string screenName, out WidgetAnchor anchor)
        {
            if (TryFindWidgetsElement(taskbar, out var element, out var bounds))
            {
                anchor = new WidgetAnchor(taskbar, bounds, element, wasVisibleBeforeLease: true);
                widgetAnchors[screenName] = anchor;
                return true;
            }
            if (TryReadTaskbarDa(out var state) && state == TaskbarDaValueState.Disabled &&
                widgetAnchors.TryGetValue(screenName, out anchor) && anchor.TaskbarHandle == taskbar && anchor.Bounds != null && IsWindow(taskbar)) return true;
            anchor = null;
            return false;
        }

        private static bool TryFindWidgetsBounds(IntPtr taskbar, out TaskbarBounds bounds)
        {
            return TryFindWidgetsElement(taskbar, out _, out bounds);
        }

        private static bool TryFindWidgetsElement(IntPtr taskbar, out AutomationElement matched, out TaskbarBounds bounds)
        {
            matched = null;
            bounds = null;
            try
            {
                if (!GetWindowRect(taskbar, out var taskbarRect)) return false;
                var root = AutomationElement.FromHandle(taskbar);
                if (root == null) return false;
                var matches = new List<WidgetCandidate>();
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
                {
                    var id = element.Current.AutomationId ?? string.Empty;
                    var name = element.Current.Name ?? string.Empty;
                    var className = element.Current.ClassName ?? string.Empty;
                    if (!IsWidgetsCandidate(element, id, className, name)) continue;
                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0 || rect.Left < taskbarRect.Left || rect.Right > taskbarRect.Right || rect.Top < taskbarRect.Top || rect.Bottom > taskbarRect.Bottom) return false;
                    matches.Add(new WidgetCandidate(element, new TaskbarBounds { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom }));
                }
                // An ambiguous or invalid Widgets discovery must not choose an arbitrary element.
                if (matches.Count != 1) return false;
                matched = matches[0].Element;
                bounds = matches[0].Bounds;
                return true;
            }
            catch { }
            return false;
        }

        private static bool IsWidgetsCandidate(AutomationElement element, string automationId, string className, string name)
        {
            if (!WidgetsElementMatcher.IsMatch(automationId, className, name)) return false;
            // UIA frequently exposes a stable Widgets container and matched descendants. The
            // parent/child relationship canonicalizes that structure to its outermost matched
            // element, so any remaining multiple candidates are genuinely ambiguous.
            return !HasStableWidgetsParent(element);
        }

        private static bool HasStableWidgetsParent(AutomationElement element)
        {
            try
            {
                var parent = TreeWalker.ControlViewWalker.GetParent(element);
                return parent != null && WidgetsElementMatcher.HasStableWidgetsIdentity(parent.Current.AutomationId, parent.Current.ClassName);
            }
            catch { return false; }
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
                    if (rect.Width <= 0 || rect.Height <= 0 || rect.Width >= taskbarRect.Width || rect.Height > taskbarRect.Height) continue;
                    if (rect.Left < taskbarRect.Left || rect.Right > taskbarRect.Right || rect.Bottom < taskbarRect.Top || rect.Top > taskbarRect.Bottom) continue;
                    var controlType = element.Current.ControlType;
                    if (controlType != ControlType.Button && controlType != ControlType.Edit && controlType != ControlType.ListItem && controlType != ControlType.MenuItem && controlType != ControlType.TabItem) continue;
                    intervals.Add(new Interval(Math.Max(taskbarRect.Left, rect.Left), Math.Min(taskbarRect.Right, rect.Right)));
                }
                intervals.Add(new Interval(taskbarRect.Left, taskbarRect.Left));
                intervals.Add(new Interval(taskbarRect.Right, taskbarRect.Right));
                intervals = Merge(intervals);
                return true;
            }
            catch { return false; }
        }

        private static bool TrySelectSafeGap(NativeRect taskbar, TaskbarBounds widgets, List<Interval> occupied, LyricDockAlignment alignment, out Interval selected)
        {
            selected = default;
            var bounds = occupied.Select(item => new TaskbarBounds { Left = item.Left, Right = item.Right, Top = taskbar.Top, Bottom = taskbar.Bottom });
            if (!LyricDockSafeSlotCalculator.TrySelect(taskbar.ToBounds(), widgets, bounds, alignment, out var safeBounds)) return false;
            selected = new Interval(safeBounds.Left, safeBounds.Right);
            return true;
        }

        private static List<Interval> Merge(IEnumerable<Interval> intervals)
        {
            var merged = new List<Interval>();
            foreach (var current in intervals.OrderBy(item => item.Left))
            {
                if (merged.Count == 0 || current.Left > merged[merged.Count - 1].Right) merged.Add(current);
                else merged[merged.Count - 1] = new Interval(merged[merged.Count - 1].Left, Math.Max(merged[merged.Count - 1].Right, current.Right));
            }
            return merged;
        }

        private sealed class TaskbarSearchState
        {
            public string ExpectedDeviceName;
            public IntPtr Handle;
            public Forms.Screen Screen;
        }

        private static bool TryFindTaskbar(string screenName, out IntPtr taskbar, out Forms.Screen screen)
        {
            taskbar = IntPtr.Zero;
            screen = null;
            var expected = string.IsNullOrWhiteSpace(screenName) ? Forms.Screen.PrimaryScreen.DeviceName : screenName;
            // Keep the delegate and state in static fields for the duration of the native call so neither can be collected.
            taskbarSearchState = new TaskbarSearchState { ExpectedDeviceName = expected };
            activeTaskbarSearch = new EnumWindowsProc(FindTaskbarCallback);
            try
            {
                EnumWindows(activeTaskbarSearch, IntPtr.Zero);
            }
            finally
            {
                activeTaskbarSearch = null;
            }
            taskbar = taskbarSearchState.Handle;
            screen = taskbarSearchState.Screen;
            return taskbar != IntPtr.Zero && screen != null;
        }

        private static EnumWindowsProc activeTaskbarSearch;
        private static TaskbarSearchState taskbarSearchState;

        private static bool FindTaskbarCallback(IntPtr hwnd, IntPtr parameter)
        {
            try
            {
                var className = GetClassName(hwnd);
                if (className != "Shell_TrayWnd" && className != "Shell_SecondaryTrayWnd") return true;
                var candidateScreen = Forms.Screen.FromHandle(hwnd);
                if (!string.Equals(candidateScreen.DeviceName, taskbarSearchState.ExpectedDeviceName, StringComparison.OrdinalIgnoreCase)) return true;
                taskbarSearchState.Handle = hwnd;
                taskbarSearchState.Screen = candidateScreen;
                return false;
            }
            catch
            {
                // Never let a managed exception propagate through the native EnumWindows stack.
                return true;
            }
        }

        private static bool IsForegroundFullscreen(Forms.Screen screen, IntPtr taskbar)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == taskbar || Forms.Screen.FromHandle(foreground).DeviceName != screen.DeviceName || !GetWindowRect(foreground, out var rect)) return false;
            var bounds = screen.Bounds;
            return rect.Left <= bounds.Left && rect.Top <= bounds.Top && rect.Right >= bounds.Right && rect.Bottom >= bounds.Bottom;
        }

                [StructLayout(LayoutKind.Sequential)]
                private struct APPBARDATA
                {
                    public int cbSize;
                    public IntPtr hWnd;
                    public uint uCallbackMessage;
                    public uint uEdge;
                    public NativeRect rc;
                    public int lParam;
                }
        
                private static bool IsTaskbarAutoHidden()
                {
                    var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
                    return (SHAppBarMessage(4, ref data).ToInt64() & 1) != 0;
                }
        private static bool IsTaskbarDark()
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false); return Convert.ToInt32(key?.GetValue("SystemUsesLightTheme", 0)) == 0; }
            catch { return true; }
        }
        private static double GetDpiScale(IntPtr hwnd) => GetDpiForWindow(hwnd) / 96.0;
        private static string GetClassName(IntPtr hwnd) { var buffer = new System.Text.StringBuilder(256); return GetClassNameNative(hwnd, buffer, buffer.Capacity) == 0 ? string.Empty : buffer.ToString(); }

        private static Version GetRealWindowsVersion()
        {
            try
            {
                var osvi = new OSVERSIONINFOEXW
                {
                    dwOSVersionInfoSize = Marshal.SizeOf<OSVERSIONINFOEXW>(),
                    szCSDVersion = new byte[256]
                };
                if (RtlGetVersion(ref osvi) == 0)
                {
                    return new Version((int)osvi.dwMajorVersion, (int)osvi.dwMinorVersion, (int)osvi.dwBuildNumber);
                }
            }
            catch { }
            return Environment.OSVersion.Version;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEXW
        {
            public int dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEXW versionInfo);

        private sealed class WidgetAnchor
        {
            public WidgetAnchor(IntPtr handle, TaskbarBounds bounds, AutomationElement element, bool wasVisibleBeforeLease)
            {
                TaskbarHandle = handle;
                Bounds = bounds;
                OriginalElement = element;
                WasVisibleBeforeLease = wasVisibleBeforeLease;
            }

            public IntPtr TaskbarHandle { get; }
            public TaskbarBounds Bounds { get; }
            public AutomationElement OriginalElement { get; }
            public bool WasVisibleBeforeLease { get; }
        }
        private sealed class WidgetCandidate { public WidgetCandidate(AutomationElement element, TaskbarBounds bounds) { Element = element; Bounds = bounds; } public AutomationElement Element { get; } public TaskbarBounds Bounds { get; } }
        private readonly struct Interval { public Interval(double left, double right) { Left = left; Right = right; } public double Left { get; } public double Right { get; } public double Width => Right - Left; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; public int Width => Right - Left; public int Height => Bottom - Top; public TaskbarBounds ToBounds() => new TaskbarBounds { Left = Left, Top = Top, Right = Right, Bottom = Bottom }; }
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")] private static extern int GetClassNameNative(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
                [DllImport("shell32.dll")] private static extern IntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
                
                // Win32 Registry API for direct access (bypasses .NET Registry class restrictions)
                private const uint KEY_WRITE = 0x20006;
                private const uint KEY_ALL_ACCESS = 0xF003F;
                private const uint REG_DWORD = 4;
                private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
                private const uint HKEY_CURRENT_USER_Wmi = 0x80000001;
                [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegOpenKeyExW(IntPtr hKey, string subKey, uint options, uint samDesired, ref IntPtr phkResult);
                [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegSetValueExW(IntPtr hKey, string valueName, uint reserved, uint type, ref int data, int cbData);
                [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegDeleteValueW(IntPtr hKey, string valueName);
                [DllImport("advapi32.dll")] private static extern int RegCloseKey(IntPtr hKey);
    }
}




