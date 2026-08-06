using System;

namespace LyricHover.Core
{
    public enum SupporterBadgeInitialSide
    {
        Front = 0,
        Back = 1
    }

    public enum SupporterBadgeSize
    {
        Compact = 0,
        Regular = 1,
        Large = 2
    }

    public sealed class SupporterBadgeOptions
    {
        public SupporterBadgeIdentity Identity { get; set; } = new SupporterBadgeIdentity
        {
            DisplayName = "LYRIC HOVER",
            AcquiredDate = DateTimeOffset.UtcNow
        };

        public bool AutoRotate { get; set; } = true;

        public SupporterBadgeInitialSide InitialSide { get; set; } =
            SupporterBadgeInitialSide.Front;

        public SupporterBadgeSize Size { get; set; } = SupporterBadgeSize.Large;
    }
}
