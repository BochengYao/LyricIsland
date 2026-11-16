using System;

namespace LyricHover.App
{
    public sealed class HoverRetreatController
    {
        public const double ReleasePadding = 24;

        public bool IsRetreated { get; private set; }

        public bool Update(bool enabled, double distance)
        {
            var previous = IsRetreated;
            if (!enabled || double.IsNaN(distance) || double.IsInfinity(distance))
            {
                IsRetreated = false;
            }
            else
            {
                IsRetreated = previous
                    ? distance < ReleasePadding
                    : distance <= 0;
            }

            return previous != IsRetreated;
        }

        public bool Reset()
        {
            if (!IsRetreated)
            {
                return false;
            }

            IsRetreated = false;
            return true;
        }
    }
}
