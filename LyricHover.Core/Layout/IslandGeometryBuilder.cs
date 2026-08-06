using System;
using System.Globalization;

namespace LyricHover.Core.Layout
{
    public static class IslandGeometryBuilder
    {
        public static string BuildTopPath(double width, double height)
        {
            var w = Math.Max(240, width);
            var h = Math.Max(60, height);
            var bottom = h - 5;

            return string.Format(
                CultureInfo.InvariantCulture,
                "M 0,0 L {0},0 C {1},0 {2},5 {3},16 C {4},24 {4},{5} {4},{6} C {4},{7} {8},{9} {10},{9} L 69,{9} C 56,{9} 48,{7} 48,{6} C 48,{5} 48,24 44,16 C 38,5 28,0 0,0 Z",
                F(w), F(w - 28), F(w - 38), F(w - 44), F(w - 48), F(h - 36), F(h - 24), F(h - 11), F(w - 56), F(bottom), F(w - 69));
        }

        private static string F(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
