using AppleMusicDesktopLyrics.Core.Layout;

namespace AppleMusicDesktopLyrics.App.LayoutEditing
{
    public sealed class IslandLayoutEditorCoordinator
    {
        private LayoutEditSession session;

        public bool IsEditing => session != null;

        public IslandLayoutProfile Draft => session?.Draft;

        public void Begin(IslandLayoutProfile profile)
        {
            session = new LayoutEditSession(profile);
        }

        public void Add(IslandModuleType type, int index)
        {
            session?.Add(type, index);
        }

        public void Move(string id, int index)
        {
            session?.Move(id, index);
        }

        public IslandLayoutProfile Commit()
        {
            var result = session?.Commit();
            session = null;
            return result;
        }

        public void Cancel()
        {
            session?.Cancel();
            session = null;
        }
    }
}
