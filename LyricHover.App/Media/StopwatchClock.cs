using System.Diagnostics;
using LyricHover.Core.Media;

namespace LyricHover.App.Media
{
    public sealed class StopwatchClock : IMonotonicClock
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        public System.TimeSpan Elapsed => stopwatch.Elapsed;
    }
}
