using System;

namespace LyricsIsland.Core
{
    public readonly struct LyricTextPlacement
    {
        public LyricTextPlacement(double left, double overflow)
        {
            Left = left;
            Overflow = overflow;
        }

        public double Left { get; }
        public double Overflow { get; }
        public bool RequiresMarquee => Overflow > 0;

        public static LyricTextPlacement Calculate(
            double availableWidth,
            double textWidth,
            double overflowPadding = 28)
        {
            var available = Math.Max(0, availableWidth);
            var text = Math.Max(0, textWidth);
            if (text <= available)
            {
                return new LyricTextPlacement((available - text) / 2, 0);
            }

            return new LyricTextPlacement(0, text - available + Math.Max(0, overflowPadding));
        }
    }
}
