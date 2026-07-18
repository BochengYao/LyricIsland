namespace AppleMusicDesktopLyrics.Core.Media
{
    public sealed class PlayerProfile
    {
        public PlayerProfile(PlayerKind kind, string displayName, params string[] sourceTokens)
        {
            Kind = kind;
            DisplayName = displayName;
            SourceTokens = sourceTokens;
        }

        public PlayerKind Kind { get; }
        public string DisplayName { get; }
        public string[] SourceTokens { get; }
    }
}
