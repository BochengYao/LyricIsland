using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricHover.Core
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
            return Select(
                lyrics,
                position,
                offset,
                multiLine,
                showTranslation,
                LyricsTranslationLanguage.SourceDefault);
        }

        public static IReadOnlyList<LyricLine> Select(
            TimedLyrics lyrics,
            TimeSpan position,
            TimeSpan offset,
            bool multiLine,
            bool showTranslation,
            LyricsTranslationLanguage targetTranslationLanguage)
        {
            lyrics = lyrics ?? new TimedLyrics(new LyricLine[0]);
            var current = lyrics.GetCurrentLine(position, offset);

            if (showTranslation && HasTranslationForTarget(lyrics, targetTranslationLanguage))
            {
                if (ShouldIgnoreTranslation(lyrics))
                {
                    // Chinese originals use the normal two-line presentation even when the
                    // global translation preference is enabled: current lyric + next lyric.
                    return lyrics.GetCurrentLines(position, offset, 2);
                }

                var translated = lyrics.GetCurrentTranslationLine(position, offset);
                if (HasMatchingTranslation(current, translated))
                {
                    return new List<LyricLine> { current, translated }.AsReadOnly();
                }

                return new List<LyricLine> { current }.AsReadOnly();
            }

            return lyrics.GetCurrentLines(position, offset, multiLine ? 2 : 1);
        }

        public static bool HasTranslationForTarget(
            TimedLyrics lyrics,
            LyricsTranslationLanguage targetTranslationLanguage)
        {
            return lyrics != null &&
                lyrics.TranslationLines.Count > 0 &&
                LyricsTranslationLanguages.IsMatch(lyrics.TranslationLanguage, targetTranslationLanguage);
        }

        public static bool ShouldIgnoreTranslation(TimedLyrics lyrics)
        {
            var lines = (lyrics?.Lines ?? new List<LyricLine>().AsReadOnly())
                .Where(line => line != null && !string.IsNullOrWhiteSpace(line.Text))
                .Take(64)
                .ToList();
            if (lines.Count == 0)
            {
                return false;
            }

            var chineseLines = lines.Count(line => IsChineseLine(line.Text));
            return chineseLines > 0 && chineseLines * 2 >= lines.Count;
        }

        private static bool IsChineseLine(string text)
        {
            var han = 0;
            var latin = 0;
            var kana = 0;
            var hangul = 0;
            foreach (var character in text ?? string.Empty)
            {
                if (IsHan(character))
                {
                    han++;
                }
                else if (character >= '\u3040' && character <= '\u30ff')
                {
                    kana++;
                }
                else if (character >= '\uac00' && character <= '\ud7af')
                {
                    hangul++;
                }
                else if (character <= '\u024f' && char.IsLetter(character))
                {
                    latin++;
                }
            }

            return han > 0 && kana == 0 && hangul == 0 && han * 2 >= Math.Max(1, latin);
        }

        private static bool IsHan(char character)
        {
            return character >= '\u3400' && character <= '\u4dbf' ||
                character >= '\u4e00' && character <= '\u9fff' ||
                character >= '\uf900' && character <= '\ufaff';
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
