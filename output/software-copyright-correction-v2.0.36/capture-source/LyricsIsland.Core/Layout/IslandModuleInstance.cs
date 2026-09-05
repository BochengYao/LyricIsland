using System;

namespace LyricsIsland.Core.Layout
{
    public sealed class IslandModuleInstance
    {
        public const double MinLyricsWidth = 220;
        public const double MaxLyricsWidth = 900;
        public const double DefaultLyricsWidth = 520;

        public IslandModuleInstance() { Id = Guid.NewGuid().ToString("N"); }
        public IslandModuleInstance(IslandModuleType type) : this() { Type = type; }

        public string Id { get; set; }
        public IslandModuleType Type { get; set; }
        public double DividerOpacity { get; set; } = 0.22;
        public double MarginBefore { get; set; } = 4;
        public double MarginAfter { get; set; } = 4;
        public double LyricsWidth { get; set; } = DefaultLyricsWidth;

        public static double NormalizeLyricsWidth(double value)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                value = DefaultLyricsWidth;
            }

            return Math.Max(MinLyricsWidth, Math.Min(MaxLyricsWidth, value));
        }
    }
}
