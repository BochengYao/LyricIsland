using System;
using System.Collections.Generic;

namespace AppleMusicDesktopLyrics.Core
{
    public static class LyricsDisplaySelector
    {
        public static IReadOnlyList<LyricLine> Select(
            TimedLyrics lyrics,
            TimeSpan position,
            TimeSpan offset,
            bool multiLine,
            bool showTranslation)
        {
            lyrics = lyrics ?? new TimedLyrics(new LyricLine[0]);
            var current = lyrics.GetCurrentLine(position, offset);

            if (showTranslation)
            {
                var translated = lyrics.GetCurrentTranslationLine(position, offset);
                if (HasMatchingTranslation(current, translated))
                {
                    return new List<LyricLine> { current, translated }.AsReadOnly();
                }

                return new List<LyricLine> { current }.AsReadOnly();
            }

            return lyrics.GetCurrentLines(position, offset, multiLine ? 2 : 1);
        }

        private static bool HasMatchingTranslation(LyricLine current, LyricLine translated)
        {
            if (current == null || translated == null || string.IsNullOrWhiteSpace(translated.Text))
            {
                return false;
            }

            var text = translated.Text.Trim();
            if (text.Length > 0 && text.Trim('/', '／').Length == 0)
            {
                return false;
            }

            return Math.Abs((current.Timestamp - translated.Timestamp).TotalMilliseconds) <= 1000;
        }
    }
}
