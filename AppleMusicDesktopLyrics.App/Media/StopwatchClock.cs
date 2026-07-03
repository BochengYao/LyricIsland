using System.Diagnostics;
using AppleMusicDesktopLyrics.Core.Media;

namespace AppleMusicDesktopLyrics.App.Media
{
    public sealed class StopwatchClock : IMonotonicClock
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        public System.TimeSpan Elapsed => stopwatch.Elapsed;
    }
}
