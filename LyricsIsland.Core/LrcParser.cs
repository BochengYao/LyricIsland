using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsIsland.Core
{
    public static class LrcParser
    {
        private static readonly Regex TimestampPattern = new Regex(@"\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\]", RegexOptions.Compiled);
        private static readonly Regex MetadataPattern = new Regex(@"^\[(ar|ti):(.+)\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static TimedLyrics Parse(string lrc)
        {
            var lines = new List<LyricLine>();
            var title = string.Empty;
            var artist = string.Empty;

            foreach (var rawLine in (lrc ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var metadata = MetadataPattern.Match(line);
                if (metadata.Success)
                {
                    if (metadata.Groups[1].Value.Equals("ar", StringComparison.OrdinalIgnoreCase))
                    {
                        artist = metadata.Groups[2].Value.Trim();
                    }
                    else if (metadata.Groups[1].Value.Equals("ti", StringComparison.OrdinalIgnoreCase))
                    {
                        title = metadata.Groups[2].Value.Trim();
                    }

                    continue;
                }

                var matches = TimestampPattern.Matches(line);
                if (matches.Count == 0)
                {
                    continue;
                }

                var text = TimestampPattern.Replace(line, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foreach (Match match in matches)
                {
                    lines.Add(new LyricLine(ToTimestamp(match), text));
                }
            }

            return new TimedLyrics(lines, title, artist);
        }

        private static TimeSpan ToTimestamp(Match match)
        {
            var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var fraction = match.Groups[3].Success ? match.Groups[3].Value : "0";

            while (fraction.Length < 3)
            {
                fraction += "0";
            }

            if (fraction.Length > 3)
            {
                fraction = fraction.Substring(0, 3);
            }

            var milliseconds = int.Parse(fraction, CultureInfo.InvariantCulture);
            return TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}
