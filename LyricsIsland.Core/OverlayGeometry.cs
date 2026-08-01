namespace LyricsIsland.Core
{
    public sealed class OverlayScreenArea
    {
        public OverlayScreenArea(
            string name,
            double boundsLeft,
            double boundsTop,
            double boundsWidth,
            double boundsHeight,
            double workLeft,
            double workTop,
            double workWidth,
            double workHeight)
        {
            Name = name ?? string.Empty;
            BoundsLeft = boundsLeft;
            BoundsTop = boundsTop;
            BoundsWidth = boundsWidth;
            BoundsHeight = boundsHeight;
            WorkLeft = workLeft;
            WorkTop = workTop;
            WorkWidth = workWidth;
            WorkHeight = workHeight;
        }

        public string Name { get; }

        public double BoundsLeft { get; }

        public double BoundsTop { get; }

        public double BoundsWidth { get; }

        public double BoundsHeight { get; }

        public double WorkLeft { get; }

        public double WorkTop { get; }

        public double WorkWidth { get; }

        public double WorkHeight { get; }
    }

    public sealed class OverlaySize
    {
        public OverlaySize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }

        public double Height { get; }
    }

    public sealed class OverlayPoint
    {
        public OverlayPoint(double left, double top)
        {
            Left = left;
            Top = top;
        }

        public double Left { get; }

        public double Top { get; }
    }
}
