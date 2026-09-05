namespace LyricsIsland.Core
{
    public sealed class ReferenceChangeTracker<T>
        where T : class
    {
        private bool initialized;
        private T current;

        public bool TryUpdate(T value)
        {
            if (initialized && ReferenceEquals(current, value))
            {
                return false;
            }

            initialized = true;
            current = value;
            return true;
        }
    }
}
