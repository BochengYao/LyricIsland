using System;

namespace LyricHover.App.TaskbarLyrics
{
    public enum TaskbarDaValueState { Absent, Disabled, Enabled }

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
    }

    public interface ITaskbarEnvironment
    {
        bool IsWindows11 { get; }
        event EventHandler Changed;
        bool TryGetPlacement(string screenName, out TaskbarLyricsPlacement placement);
        bool TryReadTaskbarDa(out TaskbarDaValueState state);
        bool TryWriteTaskbarDa(TaskbarDaValueState state);
        bool TryRefreshTaskbar();
    }
}
