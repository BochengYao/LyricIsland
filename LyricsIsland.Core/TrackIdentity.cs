using System;

namespace LyricsIsland.Core
{
    public sealed class TrackIdentity
    {
        public TrackIdentity(string title, string artist, TimeSpan duration)
            : this(title, artist, duration, string.Empty)
        {
        }

        public TrackIdentity(string title, string artist, TimeSpan duration, string album)
        {
            Title = title ?? string.Empty;
            Artist = artist ?? string.Empty;
            Duration = duration;
            Album = album ?? string.Empty;
        }

        public string Title { get; }

        public string Artist { get; }

        public TimeSpan Duration { get; }

        public string Album { get; }
    }
}
