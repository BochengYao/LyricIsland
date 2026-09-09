using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LyricHover.Core
{
    internal static class LyricsCoverageValidator
    {
        private static readonly TimeSpan MinimumMissingTail = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LaterLineTolerance = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ReferenceTimestampTolerance = TimeSpan.FromMilliseconds(1500);
        private const int MinimumLaterLineCount = 3;
        private const int MinimumReferenceMatchCount = 2;
        private const double MinimumCoverageRatio = 0.75;

        public static bool HasSufficientWordTimedOriginalCoverage(
            TimedLyrics candidate,
            TimedLyrics reference = null)
        {
            if (candidate == null || !candidate.Lines.Any(line => line.HasWordTiming))
            {
                return true;
            }

            var originalEnd = GetLastTimestamp(candidate.Lines);
            if (HasSubstantialLaterCoverage(originalEnd, candidate.TranslationLines))
            {
                return false;
            }

            return reference == null ||
                !HasComparableSource(candidate.Lines, reference.Lines) ||
                !HasSubstantialLaterCoverage(originalEnd, reference.Lines);
        }

        private static bool HasSubstantialLaterCoverage(
            TimeSpan originalEnd,
            IReadOnlyList<LyricLine> evidenceLines)
        {
            if (evidenceLines == null || evidenceLines.Count == 0)
            {
                return false;
            }

            var evidenceEnd = GetLastTimestamp(evidenceLines);
            if (evidenceEnd - originalEnd < MinimumMissingTail ||
                originalEnd.TotalMilliseconds >= evidenceEnd.TotalMilliseconds * MinimumCoverageRatio)
            {
                return false;
            }

            var laterThreshold = originalEnd + LaterLineTolerance;
            return evidenceLines.Count(line =>
                line != null &&
                !string.IsNullOrWhiteSpace(line.Text) &&
                line.Timestamp > laterThreshold) >= MinimumLaterLineCount;
        }

        private static TimeSpan GetLastTimestamp(IReadOnlyList<LyricLine> lines)
        {
            return lines
                .Where(line => line != null && !string.IsNullOrWhiteSpace(line.Text))
                .Select(line => line.Timestamp)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
        }

        private static bool HasComparableSource(
            IReadOnlyList<LyricLine> candidateLines,
            IReadOnlyList<LyricLine> referenceLines)
        {
            var matches = 0;
            foreach (var candidate in candidateLines)
            {
                var candidateText = NormalizeText(candidate?.Text);
                if (candidateText.Length == 0)
                {
                    continue;
                }

                if (referenceLines.Any(reference =>
                    string.Equals(candidateText, NormalizeText(reference?.Text), StringComparison.Ordinal) &&
                    Math.Abs((candidate.Timestamp - reference.Timestamp).TotalMilliseconds) <=
                        ReferenceTimestampTolerance.TotalMilliseconds))
                {
                    matches++;
                    if (matches >= MinimumReferenceMatchCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeText(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in (value ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture))
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
