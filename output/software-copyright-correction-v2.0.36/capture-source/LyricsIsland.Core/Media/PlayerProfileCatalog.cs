using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricsIsland.Core.Media
{
    public static class PlayerProfileCatalog
    {
        private static readonly PlayerProfile[] Profiles =
        {
            new PlayerProfile(PlayerKind.AppleMusic, "Apple Music", "applemusic"),
            new PlayerProfile(PlayerKind.QQMusic, "QQ 音乐", "qqmusic"),
            new PlayerProfile(PlayerKind.NetEaseCloudMusicUwp, "网易云音乐", "netease", "cloudmusic", "orpheus"),
            new PlayerProfile(PlayerKind.KuGou, "酷狗音乐", "kugou"),
            new PlayerProfile(PlayerKind.Spotify, "Spotify", "spotify"),
            new PlayerProfile(PlayerKind.Kuwo, "酷我音乐", "kwmusic", "kuwo")
        };

        public static PlayerProfile Resolve(string sourceAppUserModelId)
        {
            var source = sourceAppUserModelId ?? string.Empty;
            return Profiles.FirstOrDefault(profile => profile.SourceTokens.Any(token =>
                source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                ?? new PlayerProfile(PlayerKind.Generic, "其他播放器");
        }

        public static string GetSelectionKey(PlayerKind kind)
        {
            return kind == PlayerKind.Generic ? string.Empty : "player:" + kind;
        }

        public static IReadOnlyList<PlayerProfile> GetKnownProfiles()
        {
            return Profiles.ToList().AsReadOnly();
        }

        public static bool IsSupportedMusicPlayer(string sourceAppUserModelId)
        {
            return Resolve(sourceAppUserModelId).Kind != PlayerKind.Generic;
        }

        public static bool TryResolveSelectionKey(string selectionKey, out PlayerProfile profile)
        {
            profile = null;
            const string prefix = "player:";
            if (string.IsNullOrWhiteSpace(selectionKey) ||
                !selectionKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            PlayerKind kind;
            if (!Enum.TryParse(selectionKey.Substring(prefix.Length), true, out kind) || kind == PlayerKind.Generic)
            {
                return false;
            }

            profile = Profiles.FirstOrDefault(candidate => candidate.Kind == kind);
            return profile != null;
        }

        public static bool MatchesSelection(MediaSessionSnapshot session, string selectionKey)
        {
            if (session == null || string.IsNullOrWhiteSpace(selectionKey))
            {
                return false;
            }

            if (string.Equals(session.SourceAppUserModelId, selectionKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            PlayerProfile selectedProfile;
            return TryResolveSelectionKey(selectionKey, out selectedProfile) &&
                Resolve(session.SourceAppUserModelId).Kind == selectedProfile.Kind;
        }
    }
}
