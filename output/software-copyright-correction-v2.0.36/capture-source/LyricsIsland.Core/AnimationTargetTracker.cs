namespace LyricsIsland.Core
{
    public sealed class AnimationTargetTracker
    {
        private bool hasTarget;

        public double Left { get; private set; }

        public double Top { get; private set; }

        public bool TrySet(double left, double top)
        {
            if (hasTarget && Left == left && Top == top)
            {
                return false;
            }

            hasTarget = true;
            Left = left;
            Top = top;
            return true;
        }

        public void Clear()
        {
            hasTarget = false;
        }
    }
}
