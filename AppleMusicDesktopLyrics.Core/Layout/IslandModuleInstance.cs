using System;

namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class IslandModuleInstance
    {
        public IslandModuleInstance() { Id = Guid.NewGuid().ToString("N"); }
        public IslandModuleInstance(IslandModuleType type) : this() { Type = type; }

        public string Id { get; set; }
        public IslandModuleType Type { get; set; }
        public double DividerOpacity { get; set; } = 0.22;
        public double MarginBefore { get; set; } = 4;
        public double MarginAfter { get; set; } = 4;
    }
}
