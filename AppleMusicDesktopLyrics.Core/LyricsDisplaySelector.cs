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
                if (!string.IsNullOrWhiteSpace(translated.Text))
                {
                    return new List<LyricLine> { current, translated }.AsReadOnly();
                }

                return new List<LyricLine> { current }.AsReadOnly();
            }

            return lyrics.GetCurrentLines(position, offset, multiLine ? 2 : 1);
        }
    }
}
