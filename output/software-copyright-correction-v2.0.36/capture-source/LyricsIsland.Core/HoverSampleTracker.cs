namespace LyricsIsland.Core
{
    public sealed class HoverSampleTracker
    {
        private bool initialized;
        private double x;
        private double y;
        private double intensity;

        public bool TryUpdate(double nextX, double nextY, double nextIntensity)
        {
            if (initialized && x == nextX && y == nextY && intensity == nextIntensity)
            {
                return false;
            }

            initialized = true;
            x = nextX;
            y = nextY;
            intensity = nextIntensity;
            return true;
        }

        public void Clear()
        {
            initialized = false;
        }
    }
}
