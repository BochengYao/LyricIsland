using System;

namespace LyricHover.Core
{
    public sealed class OverlayPlacement
    {
        public OverlayPlacement(string screenName, OverlayDockEdge edge, double offsetRatio)
        {
            ScreenName = screenName ?? string.Empty;
            Edge = edge;
            OffsetRatio = Math.Max(0, Math.Min(1, offsetRatio));
        }

        public string ScreenName { get; }

        public OverlayDockEdge Edge { get; }

        public double OffsetRatio { get; }
    }
}
