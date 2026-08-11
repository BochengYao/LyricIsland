using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LyricHover.Core
{
    internal static class LyricsTranslationMerger
    {
        private const double MaximumTimestampDistanceMilliseconds = 1000;
        private const int MinimumNormalizedTextLength = 6;

        public static string MergeMatchingLines(string targetPackage, string referencePackage)
        {
            var target = LyricsPackageParser.Parse(targetPackage);
            var reference = LyricsPackageParser.Parse(referencePackage);
            if (target.Lines.Count == 0 || reference.Lines.Count == 0 || reference.TranslationLines.Count == 0)
            {
                return targetPackage;
            }

            var translationsByOriginal = BuildReferenceMap(reference);
            var mappedLines = new List<LyricLine>();
            foreach (var line in target.Lines)
            {
                var key = NormalizeText(line.Text);
                if (key.Length >= MinimumNormalizedTextLength &&
                    translationsByOriginal.TryGetValue(key, out var translatedText))
                {
                    mappedLines.Add(new LyricLine(line.Timestamp, translatedText));
                }
            }

            var minimumMatches = Math.Max(3, (target.Lines.Count + 4) / 5);
            if (mappedLines.Count < minimumMatches)
            {
                return targetPackage;
            }

            var translationLrc = new StringBuilder();
            foreach (var line in mappedLines)
            {
                translationLrc
                    .Append(FormatTimestamp(line.Timestamp))
                    .Append(line.Text)
                    .AppendLine();
            }

            return LyricsPackageParser.CreatePackage(
                LyricsPackageParser.GetOriginalLyrics(targetPackage),
                translationLrc.ToString(),
                reference.TranslationLanguage);
        }

        private static Dictionary<string, string> BuildReferenceMap(TimedLyrics reference)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var originalLine in reference.Lines)
            {
                var key = NormalizeText(originalLine.Text);
                if (key.Length < MinimumNormalizedTextLength || result.ContainsKey(key))
                {
                    continue;
                }

                var translatedLine = FindClosestTranslation(reference.TranslationLines, originalLine.Timestamp);
                if (translatedLine != null &&
                    LyricsPackageParser.IsMeaningfulTranslationText(translatedLine.Text))
                {
                    result[key] = translatedLine.Text.Trim();
                }
            }

            return result;
        }

        private static LyricLine FindClosestTranslation(
            IReadOnlyList<LyricLine> translationLines,
            TimeSpan timestamp)
        {
            LyricLine closest = null;
            var closestDistance = MaximumTimestampDistanceMilliseconds + 1;
            foreach (var line in translationLines)
            {
                var distance = Math.Abs((line.Timestamp - timestamp).TotalMilliseconds);
                if (distance < closestDistance)
                {
                    closest = line;
                    closestDistance = distance;
                }
            }

            return closestDistance <= MaximumTimestampDistanceMilliseconds ? closest : null;
        }

        private static string NormalizeText(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in (value ?? string.Empty).ToLower(CultureInfo.InvariantCulture))
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static string FormatTimestamp(TimeSpan timestamp)
        {
            var totalMinutes = Math.Max(0, (int)timestamp.TotalMinutes);
            return "[" +
                totalMinutes.ToString("00", CultureInfo.InvariantCulture) +
                ":" +
                timestamp.Seconds.ToString("00", CultureInfo.InvariantCulture) +
                "." +
                timestamp.Milliseconds.ToString("000", CultureInfo.InvariantCulture) +
                "]";
        }
    }
}
