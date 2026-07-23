using System.Windows.Media;
using LyricsIsland.Core.Layout;

namespace LyricsIsland.App.LayoutEditing
{
    public sealed class ModuleToolboxItemDescriptor
    {
        public ModuleToolboxItemDescriptor(
            IslandModuleType value,
            string displayName,
            double previewWidth,
            string iconData)
        {
            Value = value;
            DisplayName = displayName ?? string.Empty;
            PreviewWidth = previewWidth;
            IconGeometry = Geometry.Parse(iconData);
        }

        public IslandModuleType Value { get; }

        public string DisplayName { get; }

        public double PreviewWidth { get; }

        public Geometry IconGeometry { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
