using System;

namespace LyricHover.Core
{
    public static class LyricsPackageParser
    {
        public const string TranslationSeparator = "[aml:translation]";
        public const string TranslationLanguageMetadataPrefix = "[aml:translation-language:";

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
            var translationLanguage = ExtractTranslationLanguage(ref translationLrc);
            var original = LrcParser.Parse(originalLrc);
            var translation = LrcParser.Parse(translationLrc);
            return new TimedLyrics(
                original.Lines,
                original.Title,
                original.Artist,
                translation.Lines,
                translationLanguage);
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

        public static bool HasTranslationForLanguage(string value, LyricsTranslationLanguage targetLanguage)
        {
            if (!HasTranslation(value))
            {
                return false;
            }

            return LyricsTranslationLanguages.IsMatch(GetTranslationLanguage(value), targetLanguage);
        }

        public static LyricsTranslationLanguage GetTranslationLanguage(string value)
        {
            return Parse(value).TranslationLanguage;
        }

        public static string CreatePackage(
            string originalLrc,
            string translationLrc,
            LyricsTranslationLanguage translationLanguage = LyricsTranslationLanguage.SourceDefault)
        {
            originalLrc = originalLrc ?? string.Empty;
            if (string.IsNullOrWhiteSpace(translationLrc))
            {
                return originalLrc;
            }

            var package = originalLrc +
                Environment.NewLine +
                TranslationSeparator +
                Environment.NewLine;
            var languageCode = LyricsTranslationLanguages.ToCode(translationLanguage);
            if (!string.IsNullOrEmpty(languageCode))
            {
                package += TranslationLanguageMetadataPrefix + languageCode + "]" + Environment.NewLine;
            }

            package += translationLrc;
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

        private static LyricsTranslationLanguage ExtractTranslationLanguage(ref string translationLrc)
        {
            translationLrc = translationLrc ?? string.Empty;
            var firstLineStart = 0;
            while (firstLineStart < translationLrc.Length &&
                (translationLrc[firstLineStart] == '\r' || translationLrc[firstLineStart] == '\n'))
            {
                firstLineStart++;
            }

            var firstLineEnd = translationLrc.IndexOf('\n', firstLineStart);
            var firstLine = (firstLineEnd >= 0
                ? translationLrc.Substring(firstLineStart, firstLineEnd - firstLineStart)
                : translationLrc.Substring(firstLineStart)).Trim();
            if (!firstLine.StartsWith(TranslationLanguageMetadataPrefix, StringComparison.OrdinalIgnoreCase) ||
                !firstLine.EndsWith("]", StringComparison.Ordinal))
            {
                return LyricsTranslationLanguage.SourceDefault;
            }

            var codeLength = firstLine.Length - TranslationLanguageMetadataPrefix.Length - 1;
            var code = codeLength > 0
                ? firstLine.Substring(TranslationLanguageMetadataPrefix.Length, codeLength)
                : string.Empty;
            if (!LyricsTranslationLanguages.TryParseCode(code, out var language))
            {
                return LyricsTranslationLanguage.SourceDefault;
            }

            translationLrc = firstLineEnd >= 0
                ? translationLrc.Substring(firstLineEnd + 1)
                : string.Empty;
            return language;
        }
    }
}
