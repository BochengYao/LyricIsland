using System;

namespace LyricsIsland.Core.Media
{
    public static class TimelineMetadataResolver
    {
        public static TimeSpan ResolveDuration(TimeSpan endTime, TimeSpan maxSeekTime)
        {
            if (endTime > TimeSpan.Zero)
            {
                return endTime;
            }

            return maxSeekTime > TimeSpan.Zero ? maxSeekTime : TimeSpan.Zero;
        }

        public static bool HasReliableTimeline(TimeSpan duration, TimeSpan position)
        {
            return duration > TimeSpan.Zero && position >= TimeSpan.Zero;
        }
    }
}
