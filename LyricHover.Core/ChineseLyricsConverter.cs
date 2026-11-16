using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
            var translationIsChinese = lyrics.TranslationLanguage != LyricsTranslationLanguage.English &&
                lyrics.TranslationLanguage != LyricsTranslationLanguage.Japanese;
            foreach (var line in lyrics.TranslationLines)
                translations.Add(translationIsChinese ? ConvertLine(line, script) : line);
            return new TimedLyrics(originals, lyrics.Title, lyrics.Artist, translations, lyrics.TranslationLanguage);
        }

        public static string ConvertText(string text, ChineseLyricsScript script)
        {
            if (string.IsNullOrEmpty(text) || !LooksChinese(text)) return text ?? string.Empty;
            var flags = script == ChineseLyricsScript.Traditional ? TraditionalChinese : SimplifiedChinese;
            var locale = script == ChineseLyricsScript.Traditional ? "zh-TW" : "zh-CN";
            var length = LCMapStringEx(locale, flags, text, text.Length, null, 0,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (length <= 0) return text;
            var result = new char[length];
            var convertedLength = LCMapStringEx(locale, flags, text, text.Length, result, length,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return convertedLength > 0 ? new string(result, 0, convertedLength) : text;
        }

        private static LyricLine ConvertLine(LyricLine line, ChineseLyricsScript script)
        {
            if (line == null) return null;
            if (!LooksChinese(line.Text)) return line;
            var words = new List<LyricWord>(line.Words.Count);
            foreach (var word in line.Words)
                words.Add(new LyricWord(word.Offset, word.Duration, ConvertText(word.Text, script)));
            return new LyricLine(line.Timestamp, ConvertText(line.Text, script), words);
        }

        private static bool LooksChinese(string text)
        {
            var hasHan = false;
            foreach (var character in text)
            {
                if (character >= '\u3040' && character <= '\u30ff' ||
                    character >= '\uac00' && character <= '\ud7af') return false;
                if (character >= '\u3400' && character <= '\u9fff' ||
                    character >= '\uf900' && character <= '\ufaff') hasHan = true;
            }
            return hasHan;
        }
    }
}
