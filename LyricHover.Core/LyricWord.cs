using System;

namespace LyricHover.Core
{
    /// <summary>A word or character timing segment relative to its lyric line.</summary>
    public sealed class LyricWord
    {
        public LyricWord(TimeSpan offset, TimeSpan duration, string text)
        {
            Offset = offset < TimeSpan.Zero ? TimeSpan.Zero : offset;
            Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
            Text = text ?? string.Empty;
        }

        public TimeSpan Offset { get; }
        public TimeSpan Duration { get; }
        public string Text { get; }
    }
}
