using System;

namespace AppleMusicDesktopLyrics.Core
{
    public static class TrackIdentityCleaner
    {
        public static TrackIdentity Clean(TrackIdentity track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            var title = (track.Title ?? string.Empty).Trim();
            var artist = (track.Artist ?? string.Empty).Trim();

            var albumSeparator = title.IndexOf(" — ", StringComparison.Ordinal);
            if (albumSeparator >= 0)
            {
                title = title.Substring(0, albumSeparator).Trim();
            }

            if (string.IsNullOrWhiteSpace(artist))
            {
                var titleArtistSeparator = title.LastIndexOf(" - ", StringComparison.Ordinal);
                if (titleArtistSeparator > 0 && titleArtistSeparator < title.Length - 3)
                {
                    artist = title.Substring(titleArtistSeparator + 3).Trim();
                    title = title.Substring(0, titleArtistSeparator).Trim();
                }
            }

            return new TrackIdentity(title, artist, track.Duration, track.Album);
        }
    }
}
