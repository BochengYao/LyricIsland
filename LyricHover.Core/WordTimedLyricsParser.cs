using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace LyricHover.Core
{
    internal static class WordTimedLyricsParser
    {
        private static readonly Regex LinePattern = new Regex(@"^\[(\d+),(\d+)\](.*)$", RegexOptions.Compiled);
        private static readonly Regex MetadataPattern = new Regex(@"^\[(ar|ti):(.+)\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex YrcWordPattern = new Regex(@"\((\d+),(\d+)(?:,\d+)?\)([^()]+)", RegexOptions.Compiled);
        private static readonly Regex QrcWordPattern = new Regex(@"(.*?)\((\d+),(\d+)\)", RegexOptions.Compiled);
        // QQ/NetEase millisecond fixtures can round adjacent token boundaries in opposite
        // directions. Fifty milliseconds admits only that one-tick overlap while the
        // parser still rejects reversed starts, larger overlaps, and out-of-range data.
        private const long MaximumWordOverlapMilliseconds = 50;

        public static bool TryParse(string value, out TimedLyrics lyrics)
        {
            var lines = new List<LyricLine>();
            var title = string.Empty;
            var artist = string.Empty;
            foreach (var rawLine in SplitLines(value))
            {
                var trimmed = rawLine.Trim();
                var metadata = MetadataPattern.Match(trimmed);
                if (metadata.Success)
                {
                    if (metadata.Groups[1].Value.Equals("ti", StringComparison.OrdinalIgnoreCase)) title = metadata.Groups[2].Value.Trim();
                    else artist = metadata.Groups[2].Value.Trim();
                    continue;
                }

                var match = LinePattern.Match(trimmed);
                if (!match.Success || !TryMilliseconds(match.Groups[1].Value, out var start) ||
                    !TryMilliseconds(match.Groups[2].Value, out var duration)) continue;

                var body = match.Groups[3].Value;
                var text = ExtractVisibleText(body);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var timestamp = TimeSpan.FromMilliseconds(start);
                var lineDuration = TimeSpan.FromMilliseconds(duration);
                var words = body.TrimStart().StartsWith("(", StringComparison.Ordinal)
                    ? ParseYrcWords(body, timestamp, lineDuration, text)
                    : ParseQrcWords(body, timestamp, lineDuration, text);
                lines.Add(new LyricLine(timestamp, text, words));
            }

            lyrics = new TimedLyrics(lines, title, artist);
            return lines.Any(line => line.HasWordTiming);
        }

        public static bool TryParseLineTimed(string value, out TimedLyrics lyrics)
        {
            var lines = new List<LyricLine>();
            foreach (var rawLine in SplitLines(value))
            {
                var match = LinePattern.Match(rawLine.Trim());
                if (!match.Success || !TryMilliseconds(match.Groups[1].Value, out var start)) continue;
                var text = ExtractVisibleText(match.Groups[3].Value);
                if (!string.IsNullOrWhiteSpace(text)) lines.Add(new LyricLine(TimeSpan.FromMilliseconds(start), text));
            }

            lyrics = new TimedLyrics(lines);
            return lines.Count > 0;
        }

        private static List<LyricWord> ParseYrcWords(string body, TimeSpan timestamp, TimeSpan lineDuration, string visibleText)
        {
            return ParseWords(YrcWordPattern.Matches(body).Cast<Match>(), timestamp, lineDuration, visibleText, 1, 2, 3);
        }

        private static List<LyricWord> ParseQrcWords(string body, TimeSpan timestamp, TimeSpan lineDuration, string visibleText)
        {
            return ParseWords(QrcWordPattern.Matches(body).Cast<Match>(), timestamp, lineDuration, visibleText, 2, 3, 1);
        }

        private static List<LyricWord> ParseWords(
            IEnumerable<Match> matches,
            TimeSpan timestamp,
            TimeSpan lineDuration,
            string visibleText,
            int startGroup,
            int durationGroup,
            int textGroup)
        {
            var raw = new List<Tuple<long, long, string>>();
            foreach (var match in matches)
            {
                if (!TryMilliseconds(match.Groups[startGroup].Value, out var start) ||
                    !TryMilliseconds(match.Groups[durationGroup].Value, out var duration))
                    return new List<LyricWord>();
                raw.Add(Tuple.Create(start, duration, match.Groups[textGroup].Value));
            }
            if (raw.Count == 0 || !string.Equals(string.Concat(raw.Select(item => item.Item3)).Trim(), visibleText, StringComparison.Ordinal))
                return new List<LyricWord>();

            var lineStart = (long)timestamp.TotalMilliseconds;
            var lineLength = (long)lineDuration.TotalMilliseconds;
            var absolute = raw.All(item => item.Item1 >= lineStart && item.Item1 <= lineStart + lineLength);
            var relative = raw.All(item => item.Item1 >= 0 && item.Item1 <= lineLength);
            bool useAbsolute;
            if (absolute && !relative) useAbsolute = true;
            else if (relative && !absolute) useAbsolute = false;
            // At a zero line start, absolute and relative values produce exactly the
            // same offsets.  This is not an ambiguity and is common in QRC/YRC files.
            // For every other overlap, the two interpretations differ, so retain the
            // safe line-timed fallback instead of guessing a timeline.
            else if (lineStart == 0) useAbsolute = true;
            else return new List<LyricWord>();

            var result = new List<LyricWord>();
            long previousEnd = -1;
            long previousOffset = -1;
            foreach (var item in raw)
            {
                var offset = useAbsolute ? item.Item1 - lineStart : item.Item1;
                var end = offset + item.Item2;
                if (offset < 0 || end < offset || end > lineLength || offset < previousOffset ||
                    offset < previousEnd - MaximumWordOverlapMilliseconds) return new List<LyricWord>();
                result.Add(new LyricWord(TimeSpan.FromMilliseconds(offset), TimeSpan.FromMilliseconds(item.Item2), item.Item3));
                previousEnd = end;
                previousOffset = offset;
            }
            return result;
        }

        private static string ExtractVisibleText(string body)
        {
            var text = YrcWordPattern.Replace(body ?? string.Empty, "$3");
            return QrcWordPattern.Replace(text, "$1").Trim();
        }

        private static string[] SplitLines(string value) => (value ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        private static bool TryMilliseconds(string value, out long milliseconds)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds) &&
                milliseconds >= 0 && milliseconds <= TimeSpan.MaxValue.TotalMilliseconds;
        }
    }
}
