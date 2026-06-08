namespace AppleMusicDesktopLyrics.App
{
    public sealed class NowPlayingState
    {
        public bool HasSession { get; set; }

        public string Title { get; set; }

        public string Artist { get; set; }

        public string Album { get; set; }

        public int DurationSeconds { get; set; }

        public int PositionSeconds { get; set; }

        public bool IsPlaying { get; set; }

        public string SourceAppUserModelId { get; set; }
    }
}
