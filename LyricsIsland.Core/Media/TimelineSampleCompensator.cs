using System;

namespace LyricsIsland.Core.Media
{
    public static class TimelineSampleCompensator
    {
        public static TimeSpan Compensate(
            TimeSpan reportedPosition,
            DateTimeOffset lastUpdatedTime,
            DateTimeOffset now,
            MediaPlaybackStatus status,
            TimeSpan duration)
        {
            var position = reportedPosition < TimeSpan.Zero ? TimeSpan.Zero : reportedPosition;
            if (status == MediaPlaybackStatus.Playing &&
                lastUpdatedTime > DateTimeOffset.UnixEpoch &&
                now > lastUpdatedTime)
            {
                position += now - lastUpdatedTime;
            }

            if (duration > TimeSpan.Zero && position > duration)
            {
                position = duration;
            }

            return position;
        }
    }
}
