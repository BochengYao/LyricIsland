using System;
using System.Text.RegularExpressions;

namespace AppleMusicDesktopLyrics.Core
{
    internal static class TrackTitleNormalizer
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        public static string RemoveFeaturedArtistCredit(string title)
        {
            var value = (title ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            value = Regex.Replace(value, @"\s*[\(\[\{（【]\s*(feat\.?|ft\.?|featuring)\b[^\)\]\}）】]*[\)\]\}）】]", string.Empty, Options);
            value = Regex.Replace(value, @"\s+(feat\.?|ft\.?|featuring)\b.*$", string.Empty, Options);
            return value.Trim();
        }
    }
}
