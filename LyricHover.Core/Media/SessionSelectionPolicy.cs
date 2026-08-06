using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricHover.Core.Media
{
    public static class SessionSelectionPolicy
    {
        public static MediaSessionSnapshot Select(
            IEnumerable<MediaSessionSnapshot> sessions,
            string lockedSourceAppUserModelId,
            string windowsCurrentSessionId)
        {
            var candidates = (sessions ?? Enumerable.Empty<MediaSessionSnapshot>())
                .Where(session => session != null &&
                    !string.IsNullOrWhiteSpace(session.Title) &&
                    PlayerProfileCatalog.IsSupportedMusicPlayer(session.SourceAppUserModelId))
                .ToList();

            var locked = candidates.FirstOrDefault(session =>
                !string.IsNullOrWhiteSpace(lockedSourceAppUserModelId) &&
                PlayerProfileCatalog.MatchesSelection(session, lockedSourceAppUserModelId));
            if (locked != null) return locked;

            var current = candidates.FirstOrDefault(session =>
                string.Equals(session.SessionId, windowsCurrentSessionId, StringComparison.OrdinalIgnoreCase));
            if (current != null) return current;

            var playing = candidates
                .Where(session => session.PlaybackStatus == MediaPlaybackStatus.Playing)
                .OrderByDescending(session => session.LastActivityUtc)
                .FirstOrDefault();
            if (playing != null) return playing;

            return candidates.OrderByDescending(session => session.LastActivityUtc).FirstOrDefault();
        }
    }
}
