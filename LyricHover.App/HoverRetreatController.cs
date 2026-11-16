using System;

namespace LyricHover.App
{
    public sealed class HoverRetreatController
    {
        public const double ReleasePadding = 24;
        public static readonly TimeSpan ContactConfirmationDelay = TimeSpan.FromMilliseconds(80);

        private TimeSpan? contactStartedAt;

        public bool IsRetreated { get; private set; }

        public bool Update(bool enabled, bool isTouching, double distance, TimeSpan now)
        {
            var previous = IsRetreated;
            if (!enabled || double.IsNaN(distance) || double.IsInfinity(distance))
            {
                IsRetreated = false;
                contactStartedAt = null;
            }
            else if (previous)
            {
                IsRetreated = distance < ReleasePadding;
                if (!IsRetreated)
                {
                    contactStartedAt = null;
                }
            }
            else if (!isTouching)
            {
                contactStartedAt = null;
            }
            else
            {
                if (!contactStartedAt.HasValue || now < contactStartedAt.Value)
                {
                    contactStartedAt = now;
                }

                IsRetreated = now - contactStartedAt.Value >= ContactConfirmationDelay;
            }

            return previous != IsRetreated;
        }

        public bool Reset()
        {
            contactStartedAt = null;
            if (!IsRetreated)
            {
                return false;
            }

            IsRetreated = false;
            return true;
        }
    }
}
