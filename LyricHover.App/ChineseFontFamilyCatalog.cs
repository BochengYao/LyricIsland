using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace LyricHover.App
{
    public sealed class FontFamilyOption
    {
        public FontFamilyOption(string sourceName, string displayName)
        {
            SourceName = sourceName;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? sourceName : displayName;
        }

        public string SourceName { get; }

        public string DisplayName { get; }
    }

    public static class ChineseFontFamilyCatalog
    {
        // Use common unified ideographs shared by simplified and traditional Chinese.
        // Requiring several glyphs avoids admitting icon/symbol fonts that happen to
        // contain a single CJK code point.
        private static readonly int[] RepresentativeCharacters = { '中', '文', '歌', '曲' };
        private static readonly IReadOnlyDictionary<string, string> KnownChineseFamilyNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Microsoft YaHei UI"] = "微软雅黑 UI",
                ["Microsoft YaHei"] = "微软雅黑",
                ["SimSun"] = "宋体",
                ["NSimSun"] = "新宋体",
                ["SimHei"] = "黑体",
                ["KaiTi"] = "楷体",
                ["FangSong"] = "仿宋",
                ["DengXian"] = "等线",
                ["Microsoft JhengHei UI"] = "微軟正黑體 UI",
                ["Microsoft JhengHei"] = "微軟正黑體",
                ["PMingLiU"] = "新細明體",
                ["MingLiU"] = "細明體",
                ["MingLiU_HKSCS"] = "細明體_HKSCS",
                ["DFKai-SB"] = "標楷體"
            };

        public static IReadOnlyList<string> GetInstalledFamilyNames(bool requireChineseGlyphs)
        {
            return GetInstalledFamilies(requireChineseGlyphs, false)
                .Select(option => option.SourceName)
                .ToList();
        }

        public static IReadOnlyList<FontFamilyOption> GetInstalledFamilies(
            bool requireChineseGlyphs,
            bool preferChineseDisplayNames)
        {
            return Fonts.SystemFontFamilies
                .Where(family => family != null &&
                    (!requireChineseGlyphs || ContainsChineseGlyphs(family)))
                .Where(family => !string.IsNullOrWhiteSpace(family.Source))
                .GroupBy(family => family.Source, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(family => new FontFamilyOption(
                    family.Source,
                    preferChineseDisplayNames ? GetChineseDisplayName(family) : family.Source))
                .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string GetChineseDisplayName(FontFamily family)
        {
            try
            {
                var localizedName = family.FamilyNames
                    .Where(name => name.Key != null &&
                        name.Key.IetfLanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => GetChineseLanguagePriority(name.Key.IetfLanguageTag))
                    .Select(name => name.Value)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                if (!string.IsNullOrWhiteSpace(localizedName) &&
                    !string.Equals(localizedName, family.Source, StringComparison.OrdinalIgnoreCase))
                {
                    return localizedName;
                }

                return KnownChineseFamilyNames.TryGetValue(family.Source, out var knownName)
                    ? knownName
                    : family.Source;
            }
            catch
            {
                return KnownChineseFamilyNames.TryGetValue(family.Source, out var knownName)
                    ? knownName
                    : family.Source;
            }
        }

        private static int GetChineseLanguagePriority(string languageTag)
        {
            if (string.Equals(languageTag, "zh-CN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(languageTag, "zh-Hans", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(languageTag, "zh-TW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(languageTag, "zh-Hant", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        internal static bool ContainsChineseGlyphs(FontFamily family)
        {
            if (family == null) return false;
            try
            {
                foreach (var typeface in family.GetTypefaces())
                {
                    if (!typeface.TryGetGlyphTypeface(out var glyphTypeface)) continue;
                    if (RepresentativeCharacters.All(character =>
                        glyphTypeface.CharacterToGlyphMap.ContainsKey(character)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // A malformed or inaccessible installed font should not break Settings.
            }

            return false;
        }
    }
}
