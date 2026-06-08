namespace AppleMusicDesktopLyrics.Core
{
    public static class PlaybackVisibilityPolicy
    {
        public static bool ShouldHide(bool hasSession, string title, bool isPlaying)
        {
            return ShouldHide(hasSession, title, isPlaying, false);
        }

        public static bool ShouldHide(bool hasSession, string title, bool isPlaying, bool keepVisibleHintActive)
        {
            if (keepVisibleHintActive)
            {
                return false;
            }

            return !hasSession || string.IsNullOrWhiteSpace(title) || !isPlaying;
        }
    }
}
