using System;
using LyricHover.Core.Media;
using LyricHover.App.Modules;

namespace LyricHover.App.TaskbarLyrics
{
    public sealed class LyricsPresentationSnapshot
    {
        public MediaSessionSnapshot Session { get; set; }
        public string PrimaryText { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public TimeSpan LineDuration { get; set; } = TimeSpan.FromSeconds(4);
        public bool IsWaitingForPlayback { get; set; }
        public string AccentText { get; set; } = string.Empty;
        public TimelineReliability TimelineReliability { get; set; }
        public TimeSpan EffectivePosition { get; set; }
        public MediaPlaybackStatus? PendingPlaybackStatus { get; set; }

        public IslandRenderState ToIslandRenderState()
        {
            return new IslandRenderState
            {
                Session = Session,
                PrimaryLyric = PrimaryText ?? string.Empty,
                SecondaryLyric = SecondaryText ?? string.Empty,
                PrimaryAccent = AccentText ?? string.Empty,
                TimelineReliability = TimelineReliability,
                EffectivePosition = EffectivePosition,
                PendingPlaybackStatus = PendingPlaybackStatus,
                LineDuration = LineDuration
            };
        }
    }
}
