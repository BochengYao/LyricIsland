using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricHover.App.LyricDock
{
    public static class LyricDockSafeSlotCalculator
    {
        public static bool TrySelect(TaskbarBounds taskbar, TaskbarBounds widgets, IEnumerable<TaskbarBounds> occupiedBounds, LyricDockAlignment alignment, out TaskbarBounds safeBounds)
        {
            safeBounds = null;
            if (taskbar == null || occupiedBounds == null || taskbar.Width <= 0 || taskbar.Height <= 0) return false;
            if (widgets == null)
            {
                // Widgets were hidden manually via Windows Settings and no footprint was ever
                // observed, so there is no anchor region; select the widest gap that fits.
                return TrySelectWidestGap(taskbar, occupiedBounds, out safeBounds);
            }
            if (widgets.Width <= 0 || widgets.Height <= 0 || widgets.Left < taskbar.Left || widgets.Right > taskbar.Right || widgets.Top < taskbar.Top || widgets.Bottom > taskbar.Bottom) return false;
            var intervals = occupiedBounds
                .Where(bounds => bounds != null && bounds.Width > 0)
                .SelectMany(bounds => SplitAroundWidgets(taskbar, widgets, bounds))
                .Where(interval => interval.Width > 0)
                .Append(new Interval(taskbar.Left, taskbar.Left))
                .Append(new Interval(taskbar.Right, taskbar.Right))
                .OrderBy(interval => interval.Left)
                .ToList();
            var merged = Merge(intervals);
            var widgetsCenter = (widgets.Left + widgets.Right) / 2;
            var gaps = new List<Interval>();
            for (var index = 0; index < merged.Count - 1; index++)
            {
                var gap = new Interval(merged[index].Right, merged[index + 1].Left);
                if (gap.Width >= LyricDockController.MinimumWidth && gap.Left <= widgetsCenter && gap.Right >= widgetsCenter) gaps.Add(gap);
            }
            if (gaps.Count == 0) return false;
            // Gaps are disjoint; after anchoring to the original Widgets center there is exactly one
            // viable replacement region. Alignment controls only the lyric window inside this region.
            var selected = gaps.Single();
            safeBounds = new TaskbarBounds { Left = selected.Left, Top = taskbar.Top, Right = selected.Right, Bottom = taskbar.Bottom };
            return true;
        }

        private static bool TrySelectWidestGap(TaskbarBounds taskbar, IEnumerable<TaskbarBounds> occupiedBounds, out TaskbarBounds safeBounds)
        {
            safeBounds = null;
            var intervals = occupiedBounds
                .Where(bounds => bounds != null && bounds.Width > 0)
                .Select(bounds => new Interval(Math.Max(taskbar.Left, bounds.Left), Math.Min(taskbar.Right, bounds.Right)))
                .Where(interval => interval.Width > 0)
                .Append(new Interval(taskbar.Left, taskbar.Left))
                .Append(new Interval(taskbar.Right, taskbar.Right))
                .OrderBy(interval => interval.Left)
                .ToList();
            var merged = Merge(intervals);
            Interval? best = null;
            for (var index = 0; index < merged.Count - 1; index++)
            {
                var gap = new Interval(merged[index].Right, merged[index + 1].Left);
                if (gap.Width >= LyricDockController.MinimumWidth && (best == null || gap.Width > best.Value.Width)) best = gap;
            }
            if (best == null) return false;
            var selected = best.Value;
            safeBounds = new TaskbarBounds { Left = selected.Left, Top = taskbar.Top, Right = selected.Right, Bottom = taskbar.Bottom };
            return true;
        }

        private static List<Interval> Merge(IEnumerable<Interval> intervals)
        {
            var merged = new List<Interval>();
            foreach (var current in intervals)
            {
                if (merged.Count == 0 || current.Left > merged[merged.Count - 1].Right) merged.Add(current);
                else merged[merged.Count - 1] = new Interval(merged[merged.Count - 1].Left, Math.Max(merged[merged.Count - 1].Right, current.Right));
            }
            return merged;
        }

        private static IEnumerable<Interval> SplitAroundWidgets(TaskbarBounds taskbar, TaskbarBounds widgets, TaskbarBounds occupied)
        {
            var left = Math.Max(taskbar.Left, occupied.Left);
            var right = Math.Min(taskbar.Right, occupied.Right);
            if (right <= left) yield break;

            // The Widgets footprint itself becomes the lyric anchor after it is hidden, but an
            // overlapping UIA parent may also cover adjacent interactive controls. Preserve both
            // adjacent segments instead of discarding the entire occupied rectangle.
            if (!Intersects(left, right, widgets.Left, widgets.Right))
            {
                yield return new Interval(left, right);
                yield break;
            }
            if (left < widgets.Left) yield return new Interval(left, Math.Min(right, widgets.Left));
            if (right > widgets.Right) yield return new Interval(Math.Max(left, widgets.Right), right);
        }

        private static bool Intersects(double left, double right, double otherLeft, double otherRight) => left < otherRight && right > otherLeft;
        private readonly struct Interval { public Interval(double left, double right) { Left = left; Right = right; } public double Left { get; } public double Right { get; } public double Width => Right - Left; }
    }
}



