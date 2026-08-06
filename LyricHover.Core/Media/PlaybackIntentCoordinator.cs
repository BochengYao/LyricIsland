using System;

namespace LyricHover.Core.Media
{
    public sealed class PlaybackIntentCoordinator
    {
        private readonly object sync = new object();
        private string sessionId = string.Empty;
        private MediaPlaybackStatus? desiredStatus;

        public MediaPlaybackStatus Toggle(string nextSessionId, MediaPlaybackStatus observedStatus)
        {
            lock (sync)
            {
                if (!string.Equals(sessionId, nextSessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    sessionId = nextSessionId ?? string.Empty;
                    desiredStatus = null;
                }

                var baseline = desiredStatus ?? observedStatus;
                desiredStatus = baseline == MediaPlaybackStatus.Playing
                    ? MediaPlaybackStatus.Paused
                    : MediaPlaybackStatus.Playing;
                return desiredStatus.Value;
            }
        }

        public MediaPlaybackStatus? GetDesiredStatus(string currentSessionId)
        {
            lock (sync)
            {
                return string.Equals(sessionId, currentSessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    ? desiredStatus
                    : null;
            }
        }

        public bool Confirm(string currentSessionId, MediaPlaybackStatus observedStatus)
        {
            lock (sync)
            {
                if (!string.Equals(sessionId, currentSessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    desiredStatus != observedStatus)
                {
                    return false;
                }

                desiredStatus = null;
                return true;
            }
        }

        public bool CancelUnless(string currentSessionId)
        {
            lock (sync)
            {
                if (string.Equals(sessionId, currentSessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                sessionId = currentSessionId ?? string.Empty;
                var changed = desiredStatus.HasValue;
                desiredStatus = null;
                return changed;
            }
        }

        public void Cancel(string currentSessionId)
        {
            lock (sync)
            {
                if (string.Equals(sessionId, currentSessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    desiredStatus = null;
                }
            }
        }
    }
}
