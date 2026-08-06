using System;
using LyricHover.Core.Media;

namespace LyricHover.App.Modules
{
    public sealed class IslandRenderState
    {
        public MediaSessionSnapshot Session { get; set; }
        public MediaPlaybackStatus? PendingPlaybackStatus { get; set; }
        public string PrimaryLyric { get; set; } = string.Empty;
        public string PrimaryAccent { get; set; } = string.Empty;
        public string SecondaryLyric { get; set; } = string.Empty;
        public TimelineReliability TimelineReliability { get; set; }
        public TimeSpan EffectivePosition { get; set; }
        public TimeSpan LineDuration { get; set; } = TimeSpan.FromSeconds(4);
    }
}
