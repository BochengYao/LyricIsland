using System;

namespace LyricHover.App.LyricDock
{
    public enum TaskbarDaValueState { Absent, Disabled, Enabled }
    public enum LyricDockAlignment { Center, Left }
    public enum LyricDockFailureReason
    {
        None,
        UnsupportedOS,
        TaskbarNotFound,
        WidgetsNotFound,
        InsufficientSafeSpace,
        TaskbarAutoHiddenOrFullscreen,
        RegistryOrRefreshFailed,
        TaskbarChanged
    }

    public sealed class LyricDockPlacement
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double DpiScale { get; set; } = 1;
        public bool IsLeftAligned { get; set; }
        public bool IsVisible { get; set; }
        public bool IsFullscreenCovered { get; set; }
        public bool IsDarkTheme { get; set; }
        public TaskbarBounds TaskbarBounds { get; set; }
        public TaskbarBounds WidgetsBounds { get; set; }
    }

    public sealed class TaskbarBounds
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
        public double Width => Right - Left;
        public double Height => Bottom - Top;
    }

    public interface ILyricDockEnvironment
    {
        bool IsSupported { get; }
        event EventHandler Changed;
        bool TryGetPlacement(string screenName, LyricDockAlignment alignment, out LyricDockPlacement placement, out LyricDockFailureReason failureReason);
        bool TryReadTaskbarDa(out TaskbarDaValueState state);
        bool TryWriteTaskbarDa(TaskbarDaValueState state);
        bool TryDisableWidgetsThroughSettingsUi();
        bool TryPrepareWidgetsRestore(string screenName);
        bool TryRefreshTaskbarAndVerify(TaskbarDaValueState expectedState, bool forceHide = false);
    }

    internal static class LyricDockVisibilityPolicy
    {
        public static bool HasExposedTaskbarSample(params bool[] exposedSamples)
        {
            if (exposedSamples == null) return false;
            foreach (var sample in exposedSamples)
            {
                if (sample) return true;
            }
            return false;
        }
    }

    internal static class LyricDockPlacementStabilityPolicy
    {
        private const uint GuiInMenuMode = 0x00000004;
        private const uint GuiPopupMenuMode = 0x00000010;

        public static bool IsNativeMenuMode(uint guiThreadFlags) =>
            (guiThreadFlags & (GuiInMenuMode | GuiPopupMenuMode)) != 0;

        public static LyricDockPlacement RetainForTaskbarContextMenu(
            bool contextMenuOpen,
            LyricDockPlacement lastPlacement,
            TaskbarBounds currentTaskbarBounds,
            bool isLeftAligned,
            bool isDarkTheme)
        {
            if (!contextMenuOpen ||
                lastPlacement == null ||
                !lastPlacement.IsVisible ||
                lastPlacement.Width <= 0 ||
                lastPlacement.Height <= 0 ||
                lastPlacement.DpiScale <= 0 ||
                !AreEquivalent(lastPlacement.TaskbarBounds, currentTaskbarBounds))
            {
                return null;
            }

            return new LyricDockPlacement
            {
                Left = lastPlacement.Left,
                Top = lastPlacement.Top,
                Width = lastPlacement.Width,
                Height = lastPlacement.Height,
                DpiScale = lastPlacement.DpiScale,
                IsLeftAligned = isLeftAligned,
                IsVisible = true,
                IsFullscreenCovered = false,
                IsDarkTheme = isDarkTheme,
                TaskbarBounds = Copy(lastPlacement.TaskbarBounds),
                WidgetsBounds = Copy(lastPlacement.WidgetsBounds)
            };
        }

        private static bool AreEquivalent(TaskbarBounds left, TaskbarBounds right)
        {
            if (left == null || right == null) return false;
            return NearlyEqual(left.Left, right.Left) &&
                NearlyEqual(left.Top, right.Top) &&
                NearlyEqual(left.Right, right.Right) &&
                NearlyEqual(left.Bottom, right.Bottom);
        }

        private static TaskbarBounds Copy(TaskbarBounds bounds)
        {
            return bounds == null ? null : new TaskbarBounds
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Right = bounds.Right,
                Bottom = bounds.Bottom
            };
        }

        private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.5;
    }

    internal static class LyricDockMotionPolicy
    {
        public static bool ShouldAnimateHorizontalMove(
            bool isVisible,
            bool hasPlacement,
            double currentTargetLeft,
            double nextTargetLeft,
            TaskbarBounds currentTaskbarBounds,
            TaskbarBounds nextTaskbarBounds,
            double currentDpiScale,
            double nextDpiScale)
        {
            if (!isVisible || !hasPlacement ||
                currentTaskbarBounds == null || nextTaskbarBounds == null ||
                Math.Abs(currentTargetLeft - nextTargetLeft) < 0.5 ||
                Math.Abs(currentDpiScale - nextDpiScale) >= 0.001)
            {
                return false;
            }

            return Math.Abs(currentTaskbarBounds.Left - nextTaskbarBounds.Left) < 0.5 &&
                Math.Abs(currentTaskbarBounds.Top - nextTaskbarBounds.Top) < 0.5 &&
                Math.Abs(currentTaskbarBounds.Right - nextTaskbarBounds.Right) < 0.5 &&
                Math.Abs(currentTaskbarBounds.Bottom - nextTaskbarBounds.Bottom) < 0.5;
        }
    }
}
