using System.Linq;

namespace LyricHover.Core.Layout
{
    public static class IslandLayoutDefaults
    {
        private static IslandLayoutProfile Profile(params IslandModuleType[] types)
        {
            return new IslandLayoutProfile
            {
                Modules = types.Select(type => new IslandModuleInstance(type)).ToList()
            };
        }

        public static IslandLayoutProfile CreateHorizontal() => Profile(
            IslandModuleType.AlbumArt, IslandModuleType.Divider, IslandModuleType.Lyrics,
            IslandModuleType.Divider, IslandModuleType.PlaybackControls);

        public static IslandLayoutProfile CreateCollapsed() => Profile(IslandModuleType.Lyrics);

        public static IslandLayoutProfile CreateExpanded() => Profile(
            IslandModuleType.AlbumArt, IslandModuleType.TrackInfo, IslandModuleType.Lyrics,
            IslandModuleType.Progress, IslandModuleType.Divider, IslandModuleType.PlaybackControls);

        public static IslandLayoutSettings Create()
        {
            return new IslandLayoutSettings
            {
                Horizontal = CreateHorizontal(),
                CompactCollapsed = CreateCollapsed(),
                CompactExpanded = CreateExpanded()
            };
        }
    }
}
