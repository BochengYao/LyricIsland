using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public partial class AlbumArtModuleView : UserControl, IIslandModuleView
    {
        public AlbumArtModuleView()
        {
            InitializeComponent();
        }

        public void Update(IslandRenderState state)
        {
            var bytes = state?.Session?.ArtworkBytes;
            if (bytes == null || bytes.Length == 0)
            {
                UseNeutralArtwork();
                return;
            }

            try
            {
                using (var stream = new MemoryStream(bytes))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    ArtworkImage.Source = image;
                    ArtworkFrame.Background = Brushes.Transparent;
                }
            }
            catch
            {
                UseNeutralArtwork();
            }
        }

        private void UseNeutralArtwork()
        {
            ArtworkImage.Source = null;
            ArtworkFrame.Background = (Brush)FindResource("NeutralArtworkBrush");
        }
    }
}
