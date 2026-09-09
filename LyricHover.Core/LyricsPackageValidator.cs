using System;
using System.Globalization;
using System.Text;

namespace LyricHover.Core
{
    public static class LyricsPackageValidator
    {
        public static bool TryAccept(TrackIdentity track, string package, out TimedLyrics lyrics)
        {
            lyrics = null;
            if (track == null || string.IsNullOrWhiteSpace(package)) return false;
            try
            {
                var parsed = LyricsPackageParser.Parse(package);
                if (parsed.Lines.Count == 0) return false;
                var maximum = track.Duration > TimeSpan.Zero ? track.Duration + TimeSpan.FromMinutes(10) : TimeSpan.FromHours(24);
                foreach (var line in parsed.Lines)
                {
                    if (line.Timestamp < TimeSpan.Zero || line.Timestamp > maximum || string.IsNullOrWhiteSpace(line.Text))
                        return false;
                }
                if (!LyricsCoverageValidator.HasSufficientWordTimedOriginalCoverage(parsed)) return false;
                if (!MetadataMatches(track, parsed)) return false;
                lyrics = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool MetadataMatches(TrackIdentity track, TimedLyrics lyrics)
        {
            var expectedArtist = Normalize(track.Artist);
            var actualArtist = Normalize(lyrics.Artist);
            if (actualArtist.Length > 0 && expectedArtist.Length > 0 &&
                actualArtist != expectedArtist && !actualArtist.Contains(expectedArtist) && !expectedArtist.Contains(actualArtist))
                return false;

            var expectedTitle = Normalize(track.Title);
            var actualTitle = Normalize(lyrics.Title);
            if (actualTitle.Length == 0 || expectedTitle.Length == 0 || actualTitle == expectedTitle) return true;
            // Providers may append only a content-rating marker to an otherwise identical
            // title. This is not an arrangement/version alias, so accept that narrow
            // equivalence without using a broad title-contains rule.
            if (NormalizeWithoutContentRatingQualifier(track.Title) == NormalizeWithoutContentRatingQualifier(lyrics.Title))
                return true;
            // Different scripts are not evidence that two titles identify the same song.
            // A string package carries no verified alias provenance, so readable conflicts
            // must fail closed even when the artist matches.
            return false;
        }

        private static string NormalizeWithoutContentRatingQualifier(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length < 3) return Normalize(text);

            var closing = text[text.Length - 1];
            var opening = closing == ')' ? '(' : closing == ']' ? '[' : '\0';
            if (opening == '\0') return Normalize(text);

            var openingIndex = text.LastIndexOf(opening);
            if (openingIndex <= 0) return Normalize(text);
            var qualifier = text.Substring(openingIndex + 1, text.Length - openingIndex - 2).Trim();
            if (!qualifier.Equals("explicit", StringComparison.OrdinalIgnoreCase) &&
                !qualifier.Equals("clean", StringComparison.OrdinalIgnoreCase))
                return Normalize(text);

            return Normalize(text.Substring(0, openingIndex));
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in (value ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture))
                if (char.IsLetterOrDigit(character)) builder.Append(character);
            return builder.ToString();
        }

    }
}
