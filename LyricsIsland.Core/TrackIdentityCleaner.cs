using System;

namespace LyricsIsland.Core
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
            var album = (track.Album ?? string.Empty).Trim();

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

            artist = RemoveAlbumFromArtist(artist, album);
            title = TrackTitleNormalizer.RemoveFeaturedArtistCredit(title);
            return new TrackIdentity(title, artist, track.Duration, track.Album);
        }

        private static string RemoveAlbumFromArtist(string artist, string album)
        {
            if (string.IsNullOrWhiteSpace(artist))
            {
                return string.Empty;
            }

            var value = artist.Trim();
            if (!string.IsNullOrWhiteSpace(album))
            {
                var suffix = " — " + album.Trim();
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring(0, value.Length - suffix.Length).Trim();
                }
            }

            var albumSeparator = value.IndexOf(" — ", StringComparison.Ordinal);
            if (albumSeparator > 0)
            {
                return value.Substring(0, albumSeparator).Trim();
            }

            return value;
        }
    }
}
