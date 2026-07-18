using System;

namespace AppleMusicDesktopLyrics.Core.Media
{
    public interface IMonotonicClock
    {
        TimeSpan Elapsed { get; }
    }
}
