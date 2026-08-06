using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LyricHover.Core;

namespace LyricHover.App.Modules
{
    public partial class TrackInfoModuleView : UserControl, IIslandModuleView
    {
        private string lastTitle;
        private string lastArtist;

        public event EventHandler PreferredWidthChanged;

        public TrackInfoModuleView()
        {
            InitializeComponent();
        }

        public void Update(IslandRenderState state)
        {
            var session = state?.Session;
            var track = TrackIdentityCleaner.Clean(new TrackIdentity(
                session?.Title,
                session?.Artist,
                session?.Duration ?? System.TimeSpan.Zero,
                session?.Album));
            var title = track.Title;
            var artist = track.Artist;
            if (lastTitle == title && lastArtist == artist)
            {
                return;
            }

            lastTitle = title;
            lastArtist = artist;
            TitleText.Text = title;
            ArtistText.Text = artist;
            UpdatePreferredWidthAndMarquee();
        }

        private void UpdatePreferredWidthAndMarquee()
        {
            TitleTextTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            TitleTextTransform.X = 0;
            TitleText.Width = double.NaN;
            TitleText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ArtistText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var titleWidth = TitleText.DesiredSize.Width;
            var artistWidth = ArtistText.DesiredSize.Width;
            var preferredWidth = TrackInfoWidthCalculator.Calculate(titleWidth, artistWidth);
            var widthChanged = Math.Abs(Width - preferredWidth) >= 0.5;
            Width = preferredWidth;

            var viewportWidth = Math.Max(0, preferredWidth - TrackInfoWidthCalculator.HorizontalPadding);
            TitleText.Width = Math.Max(titleWidth, viewportWidth);
            StartTitleMarquee(titleWidth, viewportWidth);
            if (widthChanged)
            {
                PreferredWidthChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void StartTitleMarquee(double titleWidth, double viewportWidth)
        {
            var overflow = titleWidth - viewportWidth;
            if (overflow <= 1)
            {
                return;
            }

            var travelSeconds = Math.Max(2.8, overflow / 24.0);
            var animation = new DoubleAnimation
            {
                From = 0,
                To = -overflow,
                BeginTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(travelSeconds),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            TitleTextTransform.BeginAnimation(
                System.Windows.Media.TranslateTransform.XProperty,
                animation);
        }
    }
}
