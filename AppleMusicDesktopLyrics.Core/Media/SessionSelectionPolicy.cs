using System;
using System.Collections.Generic;
using System.Linq;

namespace AppleMusicDesktopLyrics.Core.Media
{
    public static class SessionSelectionPolicy
    {
        public static MediaSessionSnapshot Select(
            IEnumerable<MediaSessionSnapshot> sessions,
            string lockedSourceAppUserModelId,
            string windowsCurrentSessionId)
        {
            var candidates = (sessions ?? Enumerable.Empty<MediaSessionSnapshot>())
                .Where(session => session != null && !string.IsNullOrWhiteSpace(session.Title))
                .ToList();

            var locked = candidates.FirstOrDefault(session =>
                !string.IsNullOrWhiteSpace(lockedSourceAppUserModelId) &&
                string.Equals(session.SourceAppUserModelId, lockedSourceAppUserModelId, StringComparison.OrdinalIgnoreCase));
            if (locked != null) return locked;

            var playing = candidates
                .Where(session => session.PlaybackStatus == MediaPlaybackStatus.Playing)
                .OrderByDescending(session => session.LastActivityUtc)
                .FirstOrDefault();
            if (playing != null) return playing;

            var current = candidates.FirstOrDefault(session =>
                string.Equals(session.SessionId, windowsCurrentSessionId, StringComparison.OrdinalIgnoreCase));
            return current ?? candidates.OrderByDescending(session => session.LastActivityUtc).FirstOrDefault();
        }
    }
}
