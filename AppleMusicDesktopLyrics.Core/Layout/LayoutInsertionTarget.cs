namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class LayoutInsertionTarget
    {
        public LayoutInsertionTarget(int index, double x)
        {
            Index = index;
            X = x;
        }

        public int Index { get; }
        public double X { get; }
    }
}
