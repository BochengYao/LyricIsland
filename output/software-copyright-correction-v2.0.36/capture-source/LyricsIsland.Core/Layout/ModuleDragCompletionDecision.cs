namespace LyricsIsland.Core.Layout
{
    public static class ModuleDragCompletionDecision
    {
        public static bool ShouldDeleteExistingModule(
            bool hasExistingInstance,
            bool mouseReleaseObserved,
            bool wasCancelled,
            bool acceptedByIsland)
        {
            return hasExistingInstance &&
                mouseReleaseObserved &&
                !wasCancelled &&
                !acceptedByIsland;
        }
    }
}
