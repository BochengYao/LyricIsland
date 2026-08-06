using System;

namespace LyricHover.Core.Media
{
    public interface IMonotonicClock
    {
        TimeSpan Elapsed { get; }
    }
}
