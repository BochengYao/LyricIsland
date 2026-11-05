using System;

namespace LyricHover.App.Fullscreen
{
    public enum FullscreenMotionMode { Static, Ambient, AudioReactive }
    public enum FullscreenMotionIntensity { Low, Medium, High }
    public enum FullscreenRenderQuality { Static, Balanced30Fps, High60Fps }

    internal static class FullscreenLyricsPresets
    {
        public static void ApplyClassic(OverlayPlacementSettings settings)
        {
            settings.FullscreenPrimaryChineseFontFamily = OverlayPlacementSettings.DefaultChineseFontFamily;
            settings.FullscreenPrimaryEnglishFontFamily = OverlayPlacementSettings.DefaultEnglishFontFamily;
            settings.FullscreenTranslationChineseFontFamily = OverlayPlacementSettings.DefaultChineseFontFamily;
            settings.FullscreenTranslationEnglishFontFamily = OverlayPlacementSettings.DefaultEnglishFontFamily;
            settings.FullscreenPrimaryFontSize = 64;
            settings.FullscreenTranslationFontSize = 34;
            settings.FullscreenPrimaryFontWeight = 700;
            settings.FullscreenTranslationFontWeight = 500;
            settings.FullscreenLineHeightRatio = 1.24;
        }

        public static void ApplyAiry(OverlayPlacementSettings settings)
        {
            settings.FullscreenPrimaryFontSize = 54;
            settings.FullscreenTranslationFontSize = 28;
            settings.FullscreenPrimaryFontWeight = 400;
            settings.FullscreenTranslationFontWeight = 300;
            settings.FullscreenLineHeightRatio = 1.38;
        }

        public static void ApplyChineseReading(OverlayPlacementSettings settings)
        {
            settings.FullscreenPrimaryChineseFontFamily = "Microsoft YaHei UI";
            settings.FullscreenTranslationChineseFontFamily = "Microsoft YaHei UI";
            settings.FullscreenPrimaryFontSize = 58;
            settings.FullscreenTranslationFontSize = 32;
            settings.FullscreenPrimaryFontWeight = 600;
            settings.FullscreenTranslationFontWeight = 400;
            settings.FullscreenLineHeightRatio = 1.32;
        }

        public static void Reset(OverlayPlacementSettings settings)
        {
            settings.FullscreenShowTranslation = true;
            settings.FullscreenPrimaryChineseFontFamily = OverlayPlacementSettings.DefaultChineseFontFamily;
            settings.FullscreenPrimaryEnglishFontFamily = OverlayPlacementSettings.DefaultEnglishFontFamily;
            settings.FullscreenTranslationChineseFontFamily = OverlayPlacementSettings.DefaultChineseFontFamily;
            settings.FullscreenTranslationEnglishFontFamily = OverlayPlacementSettings.DefaultEnglishFontFamily;
            settings.FullscreenPrimaryFontSize = OverlayPlacementSettings.DefaultFullscreenPrimaryFontSize;
            settings.FullscreenTranslationFontSize = OverlayPlacementSettings.DefaultFullscreenTranslationFontSize;
            settings.FullscreenPrimaryFontWeight = 600;
            settings.FullscreenTranslationFontWeight = 400;
            settings.FullscreenLineHeightRatio = OverlayPlacementSettings.DefaultFullscreenLineHeightRatio;
            settings.FullscreenMotionMode = FullscreenMotionMode.Ambient;
            settings.FullscreenMotionIntensity = FullscreenMotionIntensity.Medium;
            settings.FullscreenRenderQuality = FullscreenRenderQuality.High60Fps;
            settings.FullscreenReduceMotion = false;
            settings.FullscreenSongEasterEggsEnabled = true;
        }

        public static string ResolveInstalledFont(string requested, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                foreach (var family in System.Windows.Media.Fonts.SystemFontFamilies)
                {
                    if (string.Equals(family.Source, requested, StringComparison.OrdinalIgnoreCase))
                    {
                        return requested;
                    }
                }
            }
            return fallback;
        }
    }
}
