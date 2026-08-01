using System;

namespace LyricsIsland.Core.Media
{
    public sealed class TimelineCoordinator
    {
        private static readonly TimeSpan MaxBackwardJitter = TimeSpan.FromMilliseconds(1500);

        private readonly IMonotonicClock clock;
        private TimeSpan anchorPosition;
        private TimeSpan anchorElapsed;
        private bool hasAnchor;
        private bool hasReportedPosition;
        private TimeSpan lastReportedPosition;
        private MediaPlaybackStatus lastStatus;

        public TimelineCoordinator(IMonotonicClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public TimelineResult Update(TimeSpan reportedPosition, bool isReliable, MediaPlaybackStatus status)
        {
            var normalizedPosition = reportedPosition < TimeSpan.Zero ? TimeSpan.Zero : reportedPosition;
            if (isReliable)
            {
                if (!hasReportedPosition || normalizedPosition != lastReportedPosition)
                {
                    var estimatedPosition = GetEstimatedPosition();
                    lastReportedPosition = normalizedPosition;
                    hasReportedPosition = true;
                    if (status == MediaPlaybackStatus.Playing &&
                        hasAnchor &&
                        normalizedPosition < estimatedPosition &&
                        estimatedPosition - normalizedPosition <= MaxBackwardJitter)
                    {
                        return EstimateFromAnchor(status);
                    }

                    SetAnchor(normalizedPosition, status);
                    return new TimelineResult(anchorPosition, TimelineReliability.Reliable);
                }

                if (hasAnchor)
                {
                    return EstimateFromAnchor(status);
                }
            }

            if (!hasAnchor)
            {
                if (status == MediaPlaybackStatus.Playing)
                {
                    SetAnchor(normalizedPosition, status);
                    return new TimelineResult(anchorPosition, TimelineReliability.Estimated);
                }

                return new TimelineResult(TimeSpan.Zero, TimelineReliability.Unavailable);
            }

            return EstimateFromAnchor(status);
        }

        public void Reset()
        {
            anchorPosition = TimeSpan.Zero;
            anchorElapsed = TimeSpan.Zero;
            hasAnchor = false;
            hasReportedPosition = false;
            lastReportedPosition = TimeSpan.Zero;
            lastStatus = MediaPlaybackStatus.Unknown;
        }

        private void SetAnchor(TimeSpan position, MediaPlaybackStatus status)
        {
            anchorPosition = position;
            anchorElapsed = clock.Elapsed;
            hasAnchor = true;
            lastStatus = status;
        }

        private TimelineResult EstimateFromAnchor(MediaPlaybackStatus status)
        {
            var estimated = GetEstimatedPosition();
            anchorPosition = estimated;
            anchorElapsed = clock.Elapsed;
            lastStatus = status;

            return new TimelineResult(estimated, TimelineReliability.Estimated);
        }

        private TimeSpan GetEstimatedPosition()
        {
            var advance = lastStatus == MediaPlaybackStatus.Playing ? clock.Elapsed - anchorElapsed : TimeSpan.Zero;
            return anchorPosition + (advance < TimeSpan.Zero ? TimeSpan.Zero : advance);
        }
    }
}
