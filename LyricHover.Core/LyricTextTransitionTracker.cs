namespace LyricHover.Core
{
    public sealed class LyricTextTransitionTracker
    {
        private string primary = string.Empty;
        private string secondary = string.Empty;
        private bool hasValue;

        public bool Update(string nextPrimary, string nextSecondary)
        {
            nextPrimary = nextPrimary ?? string.Empty;
            nextSecondary = nextSecondary ?? string.Empty;

            var changed = hasValue && (primary != nextPrimary || secondary != nextSecondary);
            primary = nextPrimary;
            secondary = nextSecondary;
            hasValue = true;
            return changed;
        }
    }
}
