using System;
using AppleMusicDesktopLyrics.Core.Media;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public sealed class IslandRenderState
    {
        public MediaSessionSnapshot Session { get; set; }
        public string PrimaryLyric { get; set; } = string.Empty;
        public string SecondaryLyric { get; set; } = string.Empty;
        public TimelineReliability TimelineReliability { get; set; }
        public TimeSpan EffectivePosition { get; set; }
        public TimeSpan LineDuration { get; set; } = TimeSpan.FromSeconds(4);
    }
}
