using System;
using System.Collections.Generic;

namespace LyricHover.Core
{
    public static class OverlayPositioner
    {
        private const double HiddenPadding = 8;

        public static OverlayPoint GetVisiblePosition(OverlayPlacement placement, OverlayScreenArea screen, OverlaySize size)
        {
            var left = GetDockedLeft(placement, screen, size);
            var top = GetDockedTop(placement, screen, size);
            return new OverlayPoint(left, top);
        }

        public static OverlayPoint GetHiddenPosition(OverlayPlacement placement, OverlayScreenArea screen, OverlaySize size)
        {
            var visible = GetVisiblePosition(placement, screen, size);
            return new OverlayPoint(visible.Left, screen.WorkTop - size.Height - HiddenPadding);
        }

        public static OverlayPlacement SnapToNearestEdge(double left, double top, OverlaySize size, IReadOnlyList<OverlayScreenArea> screens)
        {
            if (screens == null || screens.Count == 0)
            {
                return new OverlayPlacement(string.Empty, OverlayDockEdge.Top, 0.5);
            }

            var centerX = left + size.Width / 2;
            var centerY = top + size.Height / 2;
            var screen = FindNearestScreen(centerX, centerY, screens);
            var edge = OverlayDockEdge.Top;

            var offsetRatio = GetOffsetRatio(left, top, edge, screen, size);
            return new OverlayPlacement(screen.Name, edge, offsetRatio);
        }

        public static OverlayPlacement GetHorizontalDragPlacement(double pointerX, double pointerY, OverlaySize size, IReadOnlyList<OverlayScreenArea> screens)
        {
            if (screens == null || screens.Count == 0)
            {
                return new OverlayPlacement(string.Empty, OverlayDockEdge.Top, 0.5);
            }

            var screen = FindNearestScreen(pointerX, pointerY, screens);
            var targetLeft = pointerX - size.Width / 2;
            var offsetRatio = Ratio(targetLeft - screen.WorkLeft, GetAvailableHorizontal(screen, size));
            return new OverlayPlacement(screen.Name, OverlayDockEdge.Top, offsetRatio);
        }

        private static double GetDockedLeft(OverlayPlacement placement, OverlayScreenArea screen, OverlaySize size)
        {
            return screen.WorkLeft + GetAvailableHorizontal(screen, size) * placement.OffsetRatio;
        }

        private static double GetDockedTop(OverlayPlacement placement, OverlayScreenArea screen, OverlaySize size)
        {
            return screen.WorkTop;
        }

        private static double GetOffsetRatio(double left, double top, OverlayDockEdge edge, OverlayScreenArea screen, OverlaySize size)
        {
            return Ratio(left - screen.WorkLeft, GetAvailableHorizontal(screen, size));
        }

        private static OverlayScreenArea FindNearestScreen(double x, double y, IReadOnlyList<OverlayScreenArea> screens)
        {
            OverlayScreenArea nearest = screens[0];
            var nearestDistance = double.MaxValue;

            foreach (var screen in screens)
            {
                if (x >= screen.BoundsLeft &&
                    x <= screen.BoundsLeft + screen.BoundsWidth &&
                    y >= screen.BoundsTop &&
                    y <= screen.BoundsTop + screen.BoundsHeight)
                {
                    return screen;
                }

                var clampedX = Math.Max(screen.BoundsLeft, Math.Min(screen.BoundsLeft + screen.BoundsWidth, x));
                var clampedY = Math.Max(screen.BoundsTop, Math.Min(screen.BoundsTop + screen.BoundsHeight, y));
                var dx = x - clampedX;
                var dy = y - clampedY;
                var distance = dx * dx + dy * dy;
                if (distance < nearestDistance)
                {
                    nearest = screen;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static double GetAvailableHorizontal(OverlayScreenArea screen, OverlaySize size)
        {
            return Math.Max(0, screen.WorkWidth - size.Width);
        }

        private static OverlayDockEdge NormalizeEdge(OverlayDockEdge edge)
        {
            return OverlayDockEdge.Top;
        }

        private static double Ratio(double value, double max)
        {
            if (max <= 0)
            {
                return 0.5;
            }

            return Math.Max(0, Math.Min(1, value / max));
        }
    }
}
