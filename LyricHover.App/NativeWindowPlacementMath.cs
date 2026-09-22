using System;

namespace LyricHover.App
{
    internal readonly struct NativePixelBounds
    {
        public NativePixelBounds(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public int Left { get; }
        public int Top { get; }
        public int Width { get; }
        public int Height { get; }
    }

    internal static class NativeWindowPlacementMath
    {
        public static double ToWindowLogicalCoordinate(
            double physicalCoordinate,
            double windowDpiScale)
        {
            if (windowDpiScale <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowDpiScale));
            }

            return physicalCoordinate / windowDpiScale;
        }

        public static NativePixelBounds CenterInWorkingArea(
            int workingLeft,
            int workingTop,
            int workingWidth,
            int workingHeight,
            int windowWidth,
            int windowHeight)
        {
            var width = Math.Max(1, windowWidth);
            var height = Math.Max(1, windowHeight);
            var left = workingLeft + Math.Max(0, (workingWidth - width) / 2);
            var top = workingTop + Math.Max(0, (workingHeight - height) / 2);
            return new NativePixelBounds(left, top, width, height);
        }
    }
}
