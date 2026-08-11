using System;
using System.Globalization;
using System.Text;

namespace LyricHover.Core
{
    public static class LyricsCandidateMatcher
    {
        public static bool IsReasonable(TrackIdentity track, string title, string artist, string album, TimeSpan duration)
        {
            return Score(track, title, artist, album, duration) >= 55;
        }

        public static int Score(TrackIdentity track, string title, string artist, string album, TimeSpan duration)
        {
            if (track == null)
            {
                return 0;
            }

            var score = 0;
            var expectedTitle = Normalize(track.Title);
            var candidateTitle = Normalize(title);
            var expectedArtist = Normalize(track.Artist);
            var candidateArtist = Normalize(artist);
            var expectedAlbum = Normalize(track.Album);
            var candidateAlbum = Normalize(album);

            if (!string.IsNullOrWhiteSpace(expectedTitle) && expectedTitle == candidateTitle)
            {
                score += 45;
            }
            else if (!string.IsNullOrWhiteSpace(expectedTitle) && candidateTitle.Contains(expectedTitle))
            {
                score += 28;
            }
            else
            {
                score -= 35;
            }

            if (string.IsNullOrWhiteSpace(expectedArtist) || candidateArtist.Contains(expectedArtist) || expectedArtist.Contains(candidateArtist))
            {
                score += 25;
            }
            else
            {
                score -= 20;
            }

            if (track.Duration > TimeSpan.Zero && duration > TimeSpan.Zero)
            {
                var distance = Math.Abs((track.Duration - duration).TotalSeconds);
                if (distance <= 3)
                {
                    score += 25;
                }
                else if (distance <= 10)
                {
                    score += 15;
                }
                else if (distance <= 25)
                {
                    score += 4;
                }
                else
                {
                    score -= 30;
                }
            }

            if (!string.IsNullOrWhiteSpace(expectedAlbum) && !string.IsNullOrWhiteSpace(candidateAlbum))
            {
                if (expectedAlbum == candidateAlbum || expectedAlbum.Contains(candidateAlbum) || candidateAlbum.Contains(expectedAlbum))
                {
                    score += 8;
                }
                else
                {
                    score -= 6;
                }
            }

            if (candidateTitle.Contains("live") || candidateTitle.Contains("remix") || candidateTitle.Contains("karaoke") || candidateTitle.Contains("instrumental"))
            {
                if (!expectedTitle.Contains("live") && !expectedTitle.Contains("remix") && !expectedTitle.Contains("karaoke") && !expectedTitle.Contains("instrumental"))
                {
                    score -= 20;
                }
            }

            return score;
        }

        /// <summary>
        /// Scores a source title that is intentionally different from the title
        /// reported by the player (for example an Apple Music localized title).
        /// This is deliberately stricter than <see cref="Score"/>: the source
        /// artist and duration must both identify a single safe candidate.
        /// </summary>
        public static int ScoreLocalizedTitleAlias(
            TrackIdentity track,
            string title,
            string artist,
            string album,
            TimeSpan duration)
        {
            if (track == null || track.Duration <= TimeSpan.Zero)
            {
                return 0;
            }

            var expectedTitle = Normalize(track.Title);
            var candidateTitle = Normalize(title);
            var expectedArtist = Normalize(track.Artist);
            var candidateArtist = Normalize(artist);
            if (string.IsNullOrWhiteSpace(expectedTitle) ||
                string.IsNullOrWhiteSpace(candidateTitle) ||
                string.IsNullOrWhiteSpace(expectedArtist) ||
                string.IsNullOrWhiteSpace(candidateArtist) ||
                expectedTitle == candidateTitle ||
                (!candidateArtist.Contains(expectedArtist) && !expectedArtist.Contains(candidateArtist)) ||
                duration <= TimeSpan.Zero ||
                HasUnexpectedArrangementQualifier(expectedTitle, candidateTitle))
            {
                return 0;
            }

            var distance = Math.Abs((track.Duration - duration).TotalSeconds);
            var score = candidateArtist == expectedArtist ? 45 : 35;
            if (distance <= 2)
            {
                score += 55;
            }
            else if (distance <= 5)
            {
                score += 45;
            }
            else if (distance <= 8)
            {
                score += 35;
            }
            else
            {
                return 0;
            }

            var expectedAlbum = Normalize(track.Album);
            var candidateAlbum = Normalize(album);
            if (!string.IsNullOrWhiteSpace(expectedAlbum) && !string.IsNullOrWhiteSpace(candidateAlbum) &&
                (expectedAlbum == candidateAlbum || expectedAlbum.Contains(candidateAlbum) || candidateAlbum.Contains(expectedAlbum)))
            {
                score += 10;
            }

            return score;
        }

        private static bool HasUnexpectedArrangementQualifier(string expectedTitle, string candidateTitle)
        {
            foreach (var qualifier in new[] { "live", "remix", "karaoke", "instrumental" })
            {
                if (candidateTitle.Contains(qualifier) && !expectedTitle.Contains(qualifier))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder();
            var title = TrackTitleNormalizer.RemoveFeaturedArtistCredit(value);
            foreach (var character in title.ToLower(CultureInfo.InvariantCulture))
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
                else if (char.IsWhiteSpace(character) && builder.Length > 0 && builder[builder.Length - 1] != ' ')
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString().Trim();
        }
    }
}
