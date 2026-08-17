using System;

namespace LyricHover.App.TaskbarLyrics
{
    public enum TaskbarDaValueState { Absent, Disabled, Enabled }
    public enum TaskbarLyricsAlignment { Center, Left }
    public enum TaskbarLyricsFailureReason
    {
        None,
        Windows11Required,
        TaskbarNotFound,
        WidgetsNotFound,
        InsufficientSafeSpace,
        TaskbarAutoHiddenOrFullscreen,
        RegistryOrRefreshFailed,
        TaskbarChanged
    }

    public sealed class TaskbarLyricsPlacement
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

    public interface ITaskbarEnvironment
    {
        bool IsWindows11 { get; }
        event EventHandler Changed;
        bool TryGetPlacement(string screenName, TaskbarLyricsAlignment alignment, out TaskbarLyricsPlacement placement, out TaskbarLyricsFailureReason failureReason);
        bool TryReadTaskbarDa(out TaskbarDaValueState state);
        bool TryWriteTaskbarDa(TaskbarDaValueState state);
        bool TryRefreshTaskbarAndVerify(TaskbarDaValueState expectedState);
    }
}
