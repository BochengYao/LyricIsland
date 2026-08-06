using System.Collections.Generic;
using System.Linq;
using LyricHover.Core.Layout;

namespace LyricHover.App.LayoutEditing
{
    public static class ModuleToolboxCatalog
    {
        private static readonly IReadOnlyList<ModuleToolboxItemDescriptor> Items =
            new[]
            {
                new ModuleToolboxItemDescriptor(
                    IslandModuleType.Lyrics,
                    "歌词",
                    ModulePreviewMetrics.GetWidth(IslandModuleType.Lyrics),
                    "M2,3 L7,3 L7,8 L4,8 L4,11 L2,11 Z M10,3 L15,3 L15,8 L12,8 L12,11 L10,11 Z M2,14 L15,14 L15,16 L2,16 Z"),
                new ModuleToolboxItemDescriptor(
                    IslandModuleType.AlbumArt,
                    "封面",
                    ModulePreviewMetrics.GetWidth(IslandModuleType.AlbumArt),
                    "M2,2 L16,2 L16,16 L2,16 Z M4,13 L8,8 L11,11 L13,9 L15,13 Z M12,5 A2,2 0 1 1 11.99,5 Z"),
                new ModuleToolboxItemDescriptor(
                    IslandModuleType.PlaybackControls,
                    "播放",
                    ModulePreviewMetrics.GetWidth(IslandModuleType.PlaybackControls),
                    "M3,2 L3,16 L13,9 Z M14,2 L16,2 L16,16 L14,16 Z"),
                new ModuleToolboxItemDescriptor(
                    IslandModuleType.TrackInfo,
                    "信息",
                    ModulePreviewMetrics.GetWidth(IslandModuleType.TrackInfo),
                    "M2,2 L16,2 L16,5 L2,5 Z M5,7 A2,2 0 1 1 4.99,7 Z M1,15 C1,12.5 3,11 5,11 C7,11 9,12.5 9,15 Z M11,9 L17,9 L17,11 L11,11 Z M11,13 L16,13 L16,15 L11,15 Z"),
                new ModuleToolboxItemDescriptor(
                    IslandModuleType.Progress,
                    "进度",
                    ModulePreviewMetrics.GetWidth(IslandModuleType.Progress),
                    "M2,8 L16,8 L16,10 L2,10 Z M9,6 A3,3 0 1 1 8.99,6 Z"),
                new ModuleToolboxItemDescriptor(
                    IslandModuleType.Divider,
                    "分割",
                    ModulePreviewMetrics.GetWidth(IslandModuleType.Divider),
                    "M8,1 L10,1 L10,17 L8,17 Z")
            };

        public static IReadOnlyList<ModuleToolboxItemDescriptor> All => Items;

        public static ModuleToolboxItemDescriptor Get(IslandModuleType type)
        {
            return Items.FirstOrDefault(item => item.Value == type) ?? Items[1];
        }
    }
}
