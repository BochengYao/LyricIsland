using System;
using System.Collections.Generic;
using System.Linq;

namespace AppleMusicDesktopLyrics.Core
{
    public sealed class TimedLyrics
    {
        public TimedLyrics(IEnumerable<LyricLine> lines, string title = "", string artist = "")
            : this(lines, title, artist, null)
        {
        }

        public TimedLyrics(IEnumerable<LyricLine> lines, string title, string artist, IEnumerable<LyricLine> translationLines)
        {
            Lines = (lines ?? Enumerable.Empty<LyricLine>())
                .OrderBy(line => line.Timestamp)
                .ToList()
                .AsReadOnly();
            TranslationLines = (translationLines ?? Enumerable.Empty<LyricLine>())
                .OrderBy(line => line.Timestamp)
                .ToList()
                .AsReadOnly();
            Title = title ?? string.Empty;
            Artist = artist ?? string.Empty;
        }

        public IReadOnlyList<LyricLine> Lines { get; }

        public IReadOnlyList<LyricLine> TranslationLines { get; }

        public string Title { get; }

        public string Artist { get; }

        public LyricLine GetCurrentLine(TimeSpan position)
        {
            return GetCurrentLine(position, TimeSpan.Zero);
        }

        public LyricLine GetCurrentLine(TimeSpan position, TimeSpan offset)
        {
            LyricLine current = new LyricLine(TimeSpan.Zero, string.Empty);
            var adjustedPosition = position + offset;

            foreach (var line in Lines)
            {
                if (line.Timestamp > adjustedPosition)
                {
                    break;
                }

                current = line;
            }

            return current;
        }

        public IReadOnlyList<LyricLine> GetCurrentLines(TimeSpan position, TimeSpan offset, int count)
        {
            if (count <= 0 || Lines.Count == 0)
            {
                return new List<LyricLine>().AsReadOnly();
            }

            var adjustedPosition = position + offset;
            var currentIndex = -1;

            for (var index = 0; index < Lines.Count; index++)
            {
                if (Lines[index].Timestamp > adjustedPosition)
                {
                    break;
                }

                currentIndex = index;
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            return Lines
                .Skip(currentIndex)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<LyricLine> GetCurrentDisplayLines(TimeSpan position, TimeSpan offset, int count)
        {
            if (count <= 0)
            {
                return new List<LyricLine>().AsReadOnly();
            }

            var current = GetCurrentLine(position, offset);
            var translated = GetCurrentTranslationLine(position, offset);
            if (!string.IsNullOrWhiteSpace(current.Text) && !string.IsNullOrWhiteSpace(translated.Text) && count >= 2)
            {
                return new List<LyricLine> { current, translated }.AsReadOnly();
            }

            return GetCurrentLines(position, offset, count);
        }

        public LyricLine GetCurrentTranslationLine(TimeSpan position, TimeSpan offset)
        {
            LyricLine current = new LyricLine(TimeSpan.Zero, string.Empty);
            var adjustedPosition = position + offset;

            foreach (var line in TranslationLines)
            {
                if (line.Timestamp > adjustedPosition)
                {
                    break;
                }

                current = line;
            }

            return current;
        }

        public TimeSpan GetCurrentLineDuration(TimeSpan position, TimeSpan offset, TimeSpan fallbackDuration)
        {
            if (Lines.Count < 2)
            {
                return fallbackDuration;
            }

            var adjustedPosition = position + offset;
            var currentIndex = -1;

            for (var index = 0; index < Lines.Count; index++)
            {
                if (Lines[index].Timestamp > adjustedPosition)
                {
                    break;
                }

                currentIndex = index;
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (currentIndex >= Lines.Count - 1)
            {
                return fallbackDuration;
            }

            var duration = Lines[currentIndex + 1].Timestamp - Lines[currentIndex].Timestamp;
            return duration > TimeSpan.Zero ? duration : fallbackDuration;
        }
    }
}
