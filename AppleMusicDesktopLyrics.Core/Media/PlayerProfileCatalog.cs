using System;
using System.Linq;

namespace AppleMusicDesktopLyrics.Core.Media
{
    public static class PlayerProfileCatalog
    {
        private static readonly PlayerProfile[] Profiles =
        {
            new PlayerProfile(PlayerKind.AppleMusic, "Apple Music", "applemusic"),
            new PlayerProfile(PlayerKind.QQMusic, "QQ 音乐", "qqmusic"),
            new PlayerProfile(PlayerKind.NetEaseCloudMusicUwp, "网易云音乐 UWP", "netease", "cloudmusicuwp"),
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
    }
}
