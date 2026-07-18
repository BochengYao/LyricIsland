using AppleMusicDesktopLyrics.Core.Layout;

namespace AppleMusicDesktopLyrics.App.LayoutEditing
{
    public sealed class IslandLayoutDragPayload
    {
        public IslandModuleType? NewType { get; set; }
        public string ExistingInstanceId { get; set; }
    }
}
