using System;

namespace LyricsIsland.Core.Media
{
    public interface IMonotonicClock
    {
        TimeSpan Elapsed { get; }
    }
}
