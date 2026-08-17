using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricHover.App.TaskbarLyrics
{
    public static class TaskbarSafeSlotCalculator
    {
        public static bool TrySelect(TaskbarBounds taskbar, TaskbarBounds widgets, IEnumerable<TaskbarBounds> occupiedBounds, TaskbarLyricsAlignment alignment, out TaskbarBounds safeBounds)
        {
            safeBounds = null;
            if (taskbar == null || widgets == null || occupiedBounds == null) return false;
            var intervals = occupiedBounds
                .Where(bounds => bounds != null && bounds.Width > 0)
                .Where(bounds => !Intersects(bounds.Left, bounds.Right, widgets.Left, widgets.Right))
                .Select(bounds => new Interval(Math.Max(taskbar.Left, bounds.Left), Math.Min(taskbar.Right, bounds.Right)))
                .Where(interval => interval.Width > 0)
                .Append(new Interval(taskbar.Left, taskbar.Left + 2))
                .Append(new Interval(taskbar.Right - 2, taskbar.Right))
                .OrderBy(interval => interval.Left)
                .ToList();
            var merged = Merge(intervals);
            var gaps = new List<Interval>();
            for (var index = 0; index < merged.Count - 1; index++)
            {
                var gap = new Interval(merged[index].Right, merged[index + 1].Left);
                if (gap.Width >= TaskbarLyricsController.MinimumWidth) gaps.Add(gap);
            }
            if (gaps.Count == 0) return false;
            var selected = alignment == TaskbarLyricsAlignment.Left
                ? gaps.OrderBy(gap => Math.Abs(gap.Left - widgets.Left)).First()
                : gaps.OrderBy(gap => Math.Abs(((gap.Left + gap.Right) / 2) - ((taskbar.Left + taskbar.Right) / 2))).First();
            safeBounds = new TaskbarBounds { Left = selected.Left, Top = taskbar.Top, Right = selected.Right, Bottom = taskbar.Bottom };
            return true;
        }

        private static List<Interval> Merge(IEnumerable<Interval> intervals)
        {
            var merged = new List<Interval>();
            foreach (var current in intervals)
            {
                if (merged.Count == 0 || current.Left > merged[merged.Count - 1].Right + 2) merged.Add(current);
                else merged[merged.Count - 1] = new Interval(merged[merged.Count - 1].Left, Math.Max(merged[merged.Count - 1].Right, current.Right));
            }
            return merged;
        }

        private static bool Intersects(double left, double right, double otherLeft, double otherRight) => left < otherRight && right > otherLeft;
        private readonly struct Interval { public Interval(double left, double right) { Left = left; Right = right; } public double Left { get; } public double Right { get; } public double Width => Right - Left; }
    }
}
