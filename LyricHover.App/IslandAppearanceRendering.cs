using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LyricHover.App
{
    internal static class IslandAppearanceRendering
    {
        public static Geometry CreateGeometry(double width, double height, OverlayPlacementSettings settings)
        {
            settings = settings ?? new OverlayPlacementSettings();
            var w = Math.Max(120, width);
            var h = Math.Max(40, height);
            var side = Math.Min(48, w * 0.12);
            var bottom = h - 5;
            var convex = Clamp(settings.IslandConvexRadius, 0, GetMaximumConvexRadius(w, h, false));
            var concave = Clamp(settings.IslandConcaveRadius, 0, Math.Max(0, bottom - convex));
            const double k = 0.5522847498;
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                if (settings.IslandTopCornersConvex)
                {
                    var r = Clamp(settings.IslandConvexRadius, 0, GetMaximumConvexRadius(w, h, true));
                    c.BeginFigure(new Point(r, 0), true, true);
                    c.LineTo(new Point(w - r, 0), true, false);
                    c.BezierTo(new Point(w - r + r * k, 0), new Point(w, r * (1 - k)), new Point(w, r), true, false);
                    c.LineTo(new Point(w, h - r), true, false);
                    c.BezierTo(new Point(w, h - r + r * (1 - k)), new Point(w - r * (1 - k), h), new Point(w - r, h), true, false);
                    c.LineTo(new Point(r, h), true, false);
                    c.BezierTo(new Point(r * (1 - k), h), new Point(0, h - r * (1 - k)), new Point(0, h - r), true, false);
                    c.LineTo(new Point(0, r), true, false);
                    c.BezierTo(new Point(0, r * (1 - k)), new Point(r * (1 - k), 0), new Point(r, 0), true, false);
                }
                else
                {
                    c.BeginFigure(new Point(0, 0), true, true);
                    c.LineTo(new Point(w, 0), true, false);
                    c.BezierTo(new Point(w - side * 0.58, 0), new Point(w - side, concave * 0.25), new Point(w - side, concave), true, false);
                    c.LineTo(new Point(w - side, bottom - convex), true, false);
                    c.BezierTo(new Point(w - side, bottom - convex + convex * (1 - k)), new Point(w - side - convex * (1 - k), bottom), new Point(w - side - convex, bottom), true, false);
                    c.LineTo(new Point(side + convex, bottom), true, false);
                    c.BezierTo(new Point(side + convex * (1 - k), bottom), new Point(side, bottom - convex * (1 - k)), new Point(side, bottom - convex), true, false);
                    c.LineTo(new Point(side, concave), true, false);
                    c.BezierTo(new Point(side, concave * 0.25), new Point(side * 0.58, 0), new Point(0, 0), true, false);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        public static double GetMaximumConvexRadius(double width, double height, bool topCornersConvex)
        {
            var w = Math.Max(120, width);
            var h = Math.Max(40, height);
            var side = Math.Min(48, w * 0.12);
            return topCornersConvex
                ? Math.Min(h / 2, w / 2)
                : Math.Min(h / 2, Math.Min(side, w / 2));
        }

        public static double GetMaximumConcaveRadius(double width, double height, double convexRadius)
        {
            var h = Math.Max(40, height);
            var bottom = h - 5;
            var maximumConvexRadius = GetMaximumConvexRadius(width, height, false);
            var effectiveConvexRadius = Clamp(convexRadius, 0, maximumConvexRadius);
            return Math.Max(0, bottom - effectiveConvexRadius);
        }

        public static double GetScreenEdgeOffset(OverlayPlacementSettings settings)
        {
            return settings != null && settings.IslandTopCornersConvex
                ? Clamp(settings.IslandConvexScreenEdgeDistance, 0, 120)
                : 0;
        }

        public static FontFamily ResolveFont(string family, string fallback)
        {
            var requested = string.IsNullOrWhiteSpace(family) ? fallback : family.Trim();
            foreach (var installed in Fonts.SystemFontFamilies)
            {
                if (string.Equals(installed.Source, requested, StringComparison.OrdinalIgnoreCase)) return installed;
            }
            foreach (var installed in Fonts.SystemFontFamilies)
            {
                if (string.Equals(installed.Source, fallback, StringComparison.OrdinalIgnoreCase)) return installed;
            }
            try { return new FontFamily(fallback); }
            catch { return new FontFamily("Segoe UI"); }
        }

        public static void SetMixedText(TextBlock block, string text, FontFamily chinese, FontFamily english)
        {
            if (block == null) return;
            block.FontFamily = english ?? new FontFamily("Segoe UI");
            block.Inlines.Clear();
            var value = text ?? string.Empty;
            if (value.Length == 0) return;
            var fallbackCjk = ContainsJapaneseOrKorean(value);
            var start = 0;
            var script = GetScript(value[0], fallbackCjk);
            for (var i = 1; i <= value.Length; i++)
            {
                var next = i < value.Length ? GetScript(value[i], fallbackCjk) : -1;
                if (i == value.Length || next != script)
                {
                    var run = new Run(value.Substring(start, i - start));
                    if (script == 2) run.FontFamily = chinese;
                    else if (script == 1) run.FontFamily = english;
                    else run.FontFamily = SystemFonts.MessageFontFamily;
                    block.Inlines.Add(run);
                    start = i;
                }
                script = next;
            }
        }

        public static void ApplyMixedFonts(FormattedText text, string value, FontFamily chinese, FontFamily english)
        {
            if (text == null || string.IsNullOrEmpty(value)) return;
            var fallbackCjk = ContainsJapaneseOrKorean(value);
            text.SetFontFamily(english ?? new FontFamily("Segoe UI"), 0, value.Length);
            var start = 0;
            var script = GetScript(value[0], fallbackCjk);
            for (var i = 1; i <= value.Length; i++)
            {
                var next = i < value.Length ? GetScript(value[i], fallbackCjk) : -1;
                if (i == value.Length || next != script)
                {
                    if (script == 2) text.SetFontFamily(chinese ?? new FontFamily("Microsoft YaHei UI"), start, i - start);
                    else if (script == 0) text.SetFontFamily(SystemFonts.MessageFontFamily, start, i - start);
                    start = i;
                }
                script = next;
            }
        }

        public static double SnapToDevicePixel(double value, double dpiScale)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            if (double.IsNaN(dpiScale) || double.IsInfinity(dpiScale) || dpiScale <= 0) dpiScale = 1;
            return Math.Round(value * dpiScale, MidpointRounding.AwayFromZero) / dpiScale;
        }

        public static LinearGradientBrush CreateHorizontalOverflowFadeMask(double viewportWidth, double fadeWidth = 10)
        {
            var width = double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth)
                ? 0
                : Math.Max(0, viewportWidth);
            var fade = double.IsNaN(fadeWidth) || double.IsInfinity(fadeWidth)
                ? 0
                : Math.Max(0, Math.Min(fadeWidth, width / 2));
            var fadeOffset = width > 0 ? fade / width : 0.5;
            var mask = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                SpreadMethod = GradientSpreadMethod.Pad
            };
            mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            mask.GradientStops.Add(new GradientStop(Colors.Black, fadeOffset));
            mask.GradientStops.Add(new GradientStop(Colors.Black, 1 - fadeOffset));
            mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            mask.Freeze();
            return mask;
        }

        private static int GetScript(char value, bool fallbackCjk)
        {
            if ((value >= '\u3400' && value <= '\u9FFF') ||
                (value >= '\uF900' && value <= '\uFAFF') ||
                (value >= '\u3000' && value <= '\u303F') ||
                (value >= '\uFF00' && value <= '\uFFEF')) return fallbackCjk ? 0 : 2;
            if (value <= '\u007F' || (value >= '\u00C0' && value <= '\u024F')) return 1;
            return 0;
        }

        private static bool ContainsJapaneseOrKorean(string value)
        {
            foreach (var character in value)
            {
                if ((character >= '\u3040' && character <= '\u30FF') ||
                    (character >= '\uAC00' && character <= '\uD7AF')) return true;
            }
            return false;
        }

        private static double Clamp(double value, double min, double max)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
        }
    }
}
