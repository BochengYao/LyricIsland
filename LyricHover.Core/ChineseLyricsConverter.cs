using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LyricHover.Core
{
    public enum ChineseLyricsScript
    {
        Simplified,
        Traditional
    }

    public static class ChineseLyricsConverter
    {
        private const uint SimplifiedChinese = 0x02000000;
        private const uint TraditionalChinese = 0x04000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int LCMapStringEx(
            string localeName, uint flags, string source, int sourceLength,
            [Out] char[] destination, int destinationLength,
            IntPtr versionInformation, IntPtr reserved, IntPtr sortHandle);

        public static TimedLyrics Convert(TimedLyrics lyrics, ChineseLyricsScript script)
        {
            if (lyrics == null) return null;
            var originals = new List<LyricLine>(lyrics.Lines.Count);
            foreach (var line in lyrics.Lines) originals.Add(ConvertLine(line, script));
            var translations = new List<LyricLine>(lyrics.TranslationLines.Count);
            var translationIsChinese = lyrics.TranslationLanguage == LyricsTranslationLanguage.SimplifiedChinese ||
                lyrics.TranslationLanguage == LyricsTranslationLanguage.TraditionalChinese;
            foreach (var line in lyrics.TranslationLines)
                translations.Add(translationIsChinese || lyrics.TranslationLanguage == LyricsTranslationLanguage.SourceDefault
                    ? ConvertLine(line, script, translationIsChinese)
                    : line);
            return new TimedLyrics(originals, lyrics.Title, lyrics.Artist, translations, lyrics.TranslationLanguage);
        }

        public static string ConvertText(string text, ChineseLyricsScript script)
        {
            if (string.IsNullOrEmpty(text) || !LooksChinese(text)) return text ?? string.Empty;
            return MapText(text, script);
        }

        private static string MapText(string text, ChineseLyricsScript script)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var flags = script == ChineseLyricsScript.Traditional ? TraditionalChinese : SimplifiedChinese;
            var locale = script == ChineseLyricsScript.Traditional ? "zh-TW" : "zh-CN";
            var length = LCMapStringEx(locale, flags, text, text.Length, null, 0,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (length > 0)
            {
                var result = new char[length];
                var convertedLength = LCMapStringEx(locale, flags, text, text.Length, result, length,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (convertedLength > 0) text = new string(result, 0, convertedLength);
            }

            // Windows NLS leaves 後 unchanged on some systems, even in Simplified mode.
            return script == ChineseLyricsScript.Simplified ? text.Replace('後', '后') : text;
        }

        private static LyricLine ConvertLine(LyricLine line, ChineseLyricsScript script, bool knownChinese = false)
        {
            if (line == null) return null;
            if (!knownChinese && !LooksChinese(line.Text)) return line;
            var words = new List<LyricWord>(line.Words.Count);
            foreach (var word in line.Words)
                words.Add(new LyricWord(word.Offset, word.Duration, MapText(word.Text, script)));
            return new LyricLine(line.Timestamp, MapText(line.Text, script), words);
        }

        private static bool LooksChinese(string text)
        {
            var hasHan = false;
            foreach (var rune in text.EnumerateRunes())
            {
                var character = rune.Value;
                // Shared punctuation such as '・' and 'ー' does not identify Japanese.
                if (character >= 0x3041 && character <= 0x3096 ||
                    character >= 0x30a1 && character <= 0x30fa ||
                    character >= 0xff66 && character <= 0xff9d && character != 0xff70 ||
                    character >= 0xac00 && character <= 0xd7af) return false;

                if (character >= 0x3400 && character <= 0x4dbf ||
                    character >= 0x4e00 && character <= 0x9fff ||
                    character >= 0xf900 && character <= 0xfaff ||
                    character >= 0x20000 && character <= 0x323af) hasHan = true;
            }
            return hasHan;
        }
    }
}
