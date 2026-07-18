using System.Linq;
using System.Windows.Controls;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public partial class TrackInfoModuleView : UserControl, IIslandModuleView
    {
        public TrackInfoModuleView()
        {
            InitializeComponent();
        }

        public void Update(IslandRenderState state)
        {
            TitleText.Text = state?.Session?.Title ?? string.Empty;
            ArtistText.Text = state?.Session == null ? string.Empty :
                string.Join(" · ", new[] { state.Session.Artist, state.Session.Album }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
