using System;

namespace AppleMusicDesktopLyrics.Core.Media
{
    public sealed class TimelineCoordinator
    {
        private readonly IMonotonicClock clock;
        private TimeSpan anchorPosition;
        private TimeSpan anchorElapsed;
        private bool hasAnchor;
        private MediaPlaybackStatus lastStatus;

        public TimelineCoordinator(IMonotonicClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public TimelineResult Update(TimeSpan reportedPosition, bool isReliable, MediaPlaybackStatus status)
        {
            if (isReliable)
            {
                anchorPosition = reportedPosition < TimeSpan.Zero ? TimeSpan.Zero : reportedPosition;
                anchorElapsed = clock.Elapsed;
                hasAnchor = true;
                lastStatus = status;
                return new TimelineResult(anchorPosition, TimelineReliability.Reliable);
            }

            if (!hasAnchor)
            {
                return new TimelineResult(TimeSpan.Zero, TimelineReliability.Unavailable);
            }

            var advance = lastStatus == MediaPlaybackStatus.Playing ? clock.Elapsed - anchorElapsed : TimeSpan.Zero;
            var estimated = anchorPosition + (advance < TimeSpan.Zero ? TimeSpan.Zero : advance);
            anchorPosition = estimated;
            anchorElapsed = clock.Elapsed;
            lastStatus = status;

            return new TimelineResult(estimated, TimelineReliability.Estimated);
        }
    }
}
