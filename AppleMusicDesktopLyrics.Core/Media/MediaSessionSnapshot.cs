using System;

namespace AppleMusicDesktopLyrics.Core.Media
{
    public sealed class MediaSessionSnapshot
    {
        public string SessionId { get; set; } = string.Empty;
        public string SourceAppUserModelId { get; set; } = string.Empty;
        public string PlayerDisplayName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public byte[] ArtworkBytes { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public bool HasReliableTimeline { get; set; }
        public MediaPlaybackStatus PlaybackStatus { get; set; }
        public MediaControlCapabilities Controls { get; set; } = new MediaControlCapabilities();
        public DateTimeOffset LastActivityUtc { get; set; }

        public static MediaSessionSnapshot CreateForTest(
            string id,
            MediaPlaybackStatus status,
            DateTimeOffset activity)
        {
            return new MediaSessionSnapshot
            {
                SessionId = id,
                SourceAppUserModelId = id,
                Title = "Song",
                PlaybackStatus = status,
                LastActivityUtc = activity
            };
        }
    }
}
