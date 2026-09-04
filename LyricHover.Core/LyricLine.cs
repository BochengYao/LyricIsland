using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricHover.Core
{
    public sealed class LyricLine
    {
        public LyricLine(TimeSpan timestamp, string text, IEnumerable<LyricWord> words = null)
        {
            Timestamp = timestamp;
            Text = text ?? string.Empty;
            Words = (words ?? Enumerable.Empty<LyricWord>())
                .Where(word => word != null && !string.IsNullOrEmpty(word.Text))
                .OrderBy(word => word.Offset)
                .ToList()
                .AsReadOnly();
        }

        public TimeSpan Timestamp { get; }

        public string Text { get; }

        public IReadOnlyList<LyricWord> Words { get; }

        public bool HasWordTiming => Words.Count > 0;

        public double GetWordProgress(TimeSpan position)
        {
            if (!HasWordTiming || string.IsNullOrEmpty(Text)) return -1;

            var elapsed = position - Timestamp;
            if (elapsed <= TimeSpan.Zero) return 0;

            var totalWeight = Words.Sum(word => Math.Max(1, word.Text.Length));
            if (totalWeight <= 0) return -1;

            double completedWeight = 0;
            foreach (var word in Words)
            {
                var weight = Math.Max(1, word.Text.Length);
                if (elapsed >= word.Offset + word.Duration)
                {
                    completedWeight += weight;
                    continue;
                }

                if (elapsed > word.Offset)
                {
                    var fraction = word.Duration <= TimeSpan.Zero
                        ? 1
                        : Math.Min(1, (elapsed - word.Offset).TotalMilliseconds / word.Duration.TotalMilliseconds);
                    completedWeight += weight * fraction;
                }
                break;
            }

            return Math.Max(0, Math.Min(1, completedWeight / totalWeight));
        }
    }
}
