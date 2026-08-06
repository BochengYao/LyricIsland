using System;
using LyricHover.Core.Media;

namespace LyricHover.Core
{
    public static class PlaybackVisibilityPolicy
    {
        public static bool ShouldHide(bool hasSession, string title, bool isPlaying)
        {
            return ShouldHide(hasSession, title, isPlaying, false);
        }

        public static bool ShouldHide(bool hasSession, string title, bool isPlaying, bool keepVisibleHintActive)
        {
            return ShouldHide(
                hasSession,
                title,
                isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Stopped,
                TimeSpan.Zero,
                keepVisibleHintActive,
                false);
        }

        public static bool ShouldHide(
            bool hasSession,
            string title,
            MediaPlaybackStatus status,
            TimeSpan pausedFor,
            bool keepVisibleHintActive,
            bool layoutEditing)
        {
            if (keepVisibleHintActive || layoutEditing) return false;
            if (!hasSession || string.IsNullOrWhiteSpace(title)) return true;
            if (status == MediaPlaybackStatus.Playing) return false;
            return status != MediaPlaybackStatus.Paused || pausedFor >= TimeSpan.FromSeconds(5);
        }

        public static bool ShouldHide(
            bool hasSession,
            string title,
            MediaPlaybackStatus status,
            TimeSpan inactiveFor,
            bool keepVisibleHintActive,
            bool layoutEditing,
            TimeSpan autoRetractDelay)
        {
            if (keepVisibleHintActive || layoutEditing) return false;
            if (!hasSession || string.IsNullOrWhiteSpace(title)) return true;
            if (status == MediaPlaybackStatus.Playing) return false;
            return inactiveFor >= autoRetractDelay;
        }
    }
}
