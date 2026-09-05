using System.Windows;
using System.Windows.Controls;
using LyricsIsland.Core.Layout;

namespace LyricsIsland.App.Modules
{
    public partial class DividerModuleView : UserControl, IIslandModuleView
    {
        public DividerModuleView(IslandModuleInstance module)
        {
            InitializeComponent();
            module = module ?? new IslandModuleInstance(IslandModuleType.Divider);
            DividerLine.Opacity = module.DividerOpacity;
            Margin = new Thickness(module.MarginBefore, 0, module.MarginAfter, 0);
        }

        public void Update(IslandRenderState state)
        {
        }
    }
}
