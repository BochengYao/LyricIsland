using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LyricHover.Core;
using Forms = System.Windows.Forms;

namespace LyricHover.App
{
    public sealed class ScreenCatalog
    {
        public IReadOnlyList<OverlayScreenArea> GetScreens()
        {
            var primary = Forms.Screen.PrimaryScreen;
            var scaleX = primary != null && primary.Bounds.Width > 0
                ? SystemParameters.PrimaryScreenWidth / primary.Bounds.Width
                : 1.0;
            var scaleY = primary != null && primary.Bounds.Height > 0
                ? SystemParameters.PrimaryScreenHeight / primary.Bounds.Height
                : 1.0;

            return Forms.Screen.AllScreens
                .Select(screen => new OverlayScreenArea(
                    screen.DeviceName,
                    screen.Bounds.Left * scaleX,
                    screen.Bounds.Top * scaleY,
                    screen.Bounds.Width * scaleX,
                    screen.Bounds.Height * scaleY,
                    screen.WorkingArea.Left * scaleX,
                    screen.WorkingArea.Top * scaleY,
                    screen.WorkingArea.Width * scaleX,
                    screen.WorkingArea.Height * scaleY))
                .ToList()
                .AsReadOnly();
        }

        public OverlayScreenArea FindScreen(string screenName)
        {
            var screens = GetScreens();
            var screen = screens.FirstOrDefault(item => item.Name == screenName);
            return screen ?? screens.FirstOrDefault();
        }
    }
}
