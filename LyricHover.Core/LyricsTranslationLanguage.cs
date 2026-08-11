using System;

namespace LyricHover.Core
{
    public enum LyricsTranslationLanguage
    {
        SourceDefault,
        SimplifiedChinese,
        TraditionalChinese,
        English,
        Japanese
    }

    public static class LyricsTranslationLanguages
    {
        public static LyricsTranslationLanguage Normalize(LyricsTranslationLanguage language)
        {
            return Enum.IsDefined(typeof(LyricsTranslationLanguage), language)
                ? language
                : LyricsTranslationLanguage.SourceDefault;
        }

        public static bool IsMatch(
            LyricsTranslationLanguage availableLanguage,
            LyricsTranslationLanguage requestedLanguage)
        {
            requestedLanguage = Normalize(requestedLanguage);
            return requestedLanguage == LyricsTranslationLanguage.SourceDefault ||
                Normalize(availableLanguage) == requestedLanguage;
        }

        public static string ToCode(LyricsTranslationLanguage language)
        {
            switch (Normalize(language))
            {
                case LyricsTranslationLanguage.SimplifiedChinese:
                    return "zh-Hans";
                case LyricsTranslationLanguage.TraditionalChinese:
                    return "zh-Hant";
                case LyricsTranslationLanguage.English:
                    return "en";
                case LyricsTranslationLanguage.Japanese:
                    return "ja";
                default:
                    return string.Empty;
            }
        }

        public static bool TryParseCode(string value, out LyricsTranslationLanguage language)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "zh-hans":
                case "zh-cn":
                    language = LyricsTranslationLanguage.SimplifiedChinese;
                    return true;
                case "zh-hant":
                case "zh-tw":
                case "zh-hk":
                    language = LyricsTranslationLanguage.TraditionalChinese;
                    return true;
                case "en":
                case "en-us":
                case "en-gb":
                    language = LyricsTranslationLanguage.English;
                    return true;
                case "ja":
                case "ja-jp":
                    language = LyricsTranslationLanguage.Japanese;
                    return true;
                default:
                    language = LyricsTranslationLanguage.SourceDefault;
                    return false;
            }
        }
    }
}
