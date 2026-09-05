using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace LyricHover.Core
{
    /// <summary>Parses the public text forms used by QQ QRC and NetEase YRC.</summary>
    internal static class WordTimedLyricsParser
    {
        private static readonly Regex LinePattern = new Regex(@"^\[(\d+),(\d+)\](.*)$", RegexOptions.Compiled);
        private static readonly Regex YrcWordPattern = new Regex(@"\((\d+),(\d+)(?:,\d+)?\)([^()]+)", RegexOptions.Compiled);
        private static readonly Regex QrcWordPattern = new Regex(@"(.*?)\((\d+),(\d+)\)", RegexOptions.Compiled);

        public static bool TryParse(string value, out TimedLyrics lyrics)
        {
            var lines = new List<LyricLine>();
            foreach (var rawLine in (value ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var match = LinePattern.Match(rawLine.Trim());
                if (!match.Success) continue;

                var timestamp = TimeSpan.FromMilliseconds(ParseMilliseconds(match.Groups[1].Value));
                var lineDuration = TimeSpan.FromMilliseconds(ParseMilliseconds(match.Groups[2].Value));
                var body = match.Groups[3].Value;
                var words = body.TrimStart().StartsWith("(", StringComparison.Ordinal)
                    ? ParseYrcWords(body, timestamp, lineDuration)
                    : ParseQrcWords(body, timestamp, lineDuration);

                if (words.Count == 0) continue;
                var text = string.Concat(words.Select(word => word.Text)).Trim();
                if (!string.IsNullOrWhiteSpace(text)) lines.Add(new LyricLine(timestamp, text, words));
            }

            lyrics = new TimedLyrics(lines);
            return lines.Count > 0;
        }

        public static bool TryParseLineTimed(string value, out TimedLyrics lyrics)
        {
            var lines = new List<LyricLine>();
            foreach (var rawLine in (value ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var match = LinePattern.Match(rawLine.Trim());
                if (!match.Success)
                {
                    continue;
                }

                var body = match.Groups[3].Value;
                var text = YrcWordPattern.Replace(body, "$3");
                text = QrcWordPattern.Replace(text, "$1").Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(new LyricLine(
                        TimeSpan.FromMilliseconds(ParseMilliseconds(match.Groups[1].Value)),
                        text));
                }
            }

            lyrics = new TimedLyrics(lines);
            return lines.Count > 0;
        }

        private static List<LyricWord> ParseYrcWords(string body, TimeSpan timestamp, TimeSpan lineDuration)
        {
            var words = new List<LyricWord>();
            foreach (Match wordMatch in YrcWordPattern.Matches(body))
            {
                words.Add(CreateWord(
                    timestamp,
                    lineDuration,
                    wordMatch.Groups[1].Value,
                    wordMatch.Groups[2].Value,
                    wordMatch.Groups[3].Value));
            }

            return words;
        }

        private static List<LyricWord> ParseQrcWords(string body, TimeSpan timestamp, TimeSpan lineDuration)
        {
            var words = new List<LyricWord>();
            foreach (Match wordMatch in QrcWordPattern.Matches(body))
            {
                words.Add(CreateWord(
                    timestamp,
                    lineDuration,
                    wordMatch.Groups[2].Value,
                    wordMatch.Groups[3].Value,
                    wordMatch.Groups[1].Value));
            }

            return words;
        }

        private static LyricWord CreateWord(
            TimeSpan timestamp,
            TimeSpan lineDuration,
            string startValue,
            string durationValue,
            string text)
        {
            var wordStart = TimeSpan.FromMilliseconds(ParseMilliseconds(startValue));
            var duration = TimeSpan.FromMilliseconds(ParseMilliseconds(durationValue));
            // Both current QQ QRC and NetEase YRC normally expose absolute word
            // positions. Keep accepting relative positions for older cached payloads.
            var offset = wordStart >= timestamp && wordStart <= timestamp + lineDuration
                ? wordStart - timestamp
                : wordStart;
            return new LyricWord(offset, duration, text);
        }

        private static long ParseMilliseconds(string value)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)
                ? milliseconds
                : 0;
        }
    }
}
