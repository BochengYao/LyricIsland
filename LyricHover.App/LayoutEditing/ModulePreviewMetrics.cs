using LyricHover.Core.Layout;

namespace LyricHover.App.LayoutEditing
{
    public static class ModulePreviewMetrics
    {
        public const double Height = 48;

        public static double GetWidth(IslandModuleType type)
        {
            switch (type)
            {
                case IslandModuleType.Lyrics: return 92;
                case IslandModuleType.AlbumArt: return 48;
                case IslandModuleType.PlaybackControls: return 68;
                case IslandModuleType.TrackInfo: return 78;
                case IslandModuleType.Progress: return 74;
                case IslandModuleType.Divider: return 38;
                default: return 48;
            }
        }
    }
}
