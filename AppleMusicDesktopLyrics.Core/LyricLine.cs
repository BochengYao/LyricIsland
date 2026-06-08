using System;

namespace AppleMusicDesktopLyrics.Core
{
    public sealed class LyricLine
    {
        public LyricLine(TimeSpan timestamp, string text)
        {
            Timestamp = timestamp;
            Text = text ?? string.Empty;
        }

        public TimeSpan Timestamp { get; }

        public string Text { get; }
    }
}
