using System.Diagnostics;
using LyricsIsland.Core.Media;

namespace LyricsIsland.App.Media
{
    public sealed class StopwatchClock : IMonotonicClock
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        public System.TimeSpan Elapsed => stopwatch.Elapsed;
    }
}
