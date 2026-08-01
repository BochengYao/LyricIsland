using System;

namespace LyricsIsland.Core
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
            value = value ?? string.Empty;
            var separatorIndex = FindTranslationSeparator(value);
            if (separatorIndex < 0)
            {
                return false;
            }

            var translationLrc = value.Substring(separatorIndex + TranslationSeparator.Length);
            var translation = LrcParser.Parse(translationLrc);
            foreach (var line in translation.Lines)
            {
                if (IsMeaningfulTranslationText(line?.Text))
                {
                    return true;
                }
            }

            return false;
        }

        public static string CreatePackage(string originalLrc, string translationLrc)
        {
            originalLrc = originalLrc ?? string.Empty;
            if (string.IsNullOrWhiteSpace(translationLrc))
            {
                return originalLrc;
            }

            var package = originalLrc +
                Environment.NewLine +
                TranslationSeparator +
                Environment.NewLine +
                translationLrc;
            return HasTranslation(package) ? package : originalLrc;
        }

        internal static string GetOriginalLyrics(string value)
        {
            value = value ?? string.Empty;
            var separatorIndex = FindTranslationSeparator(value);
            return separatorIndex < 0 ? value : value.Substring(0, separatorIndex);
        }

        internal static bool IsMeaningfulTranslationText(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length > 0 && text.Trim('/', '／').Length > 0;
        }

        private static int FindTranslationSeparator(string value)
        {
            return value.IndexOf(TranslationSeparator, StringComparison.Ordinal);
        }
    }
}
