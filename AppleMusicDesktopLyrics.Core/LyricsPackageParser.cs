using System;

namespace AppleMusicDesktopLyrics.Core
{
    public static class LyricsPackageParser
    {
        public const string TranslationSeparator = "[aml:translation]";

        public static TimedLyrics Parse(string value)
        {
            value = value ?? string.Empty;
            var separatorIndex = FindTranslationSeparator(value);
            if (separatorIndex < 0)
            {
                return LrcParser.Parse(value);
            }

            var originalLrc = value.Substring(0, separatorIndex);
            var translationLrc = value.Substring(separatorIndex + TranslationSeparator.Length);
            var original = LrcParser.Parse(originalLrc);
            var translation = LrcParser.Parse(translationLrc);
            return new TimedLyrics(original.Lines, original.Title, original.Artist, translation.Lines);
        }

        public static bool HasTranslation(string value)
        {
            return FindTranslationSeparator(value ?? string.Empty) >= 0;
        }

        private static int FindTranslationSeparator(string value)
        {
            return value.IndexOf(TranslationSeparator, StringComparison.Ordinal);
        }
    }
}
