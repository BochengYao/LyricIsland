using System;

namespace LyricHover.Core
{
    public static class TrackInfoWidthCalculator
    {
        public const double MinimumWidth = 112;
        public const double MaximumWidth = 232;
        public const double HorizontalPadding = 14;

        public static double Calculate(double titleWidth, double artistWidth)
        {
            var contentWidth = Math.Max(Math.Max(0, titleWidth), Math.Max(0, artistWidth));
            return Math.Max(MinimumWidth, Math.Min(MaximumWidth, contentWidth + HorizontalPadding));
        }
    }
}
