using System;

namespace LyricHover.Core.Media
{
    public sealed class TimelineResult
    {
        public TimelineResult(TimeSpan position, TimelineReliability reliability)
        {
            Position = position;
            Reliability = reliability;
        }

        public TimeSpan Position { get; }
        public TimelineReliability Reliability { get; }
    }
}
