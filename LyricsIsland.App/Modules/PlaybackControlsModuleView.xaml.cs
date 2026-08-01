using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LyricsIsland.Core.Media;

namespace LyricsIsland.App.Modules
{
    public partial class PlaybackControlsModuleView : UserControl, IIslandModuleView
    {
        private bool renderStateInitialized;
        private bool lastHasSession;
        private MediaPlaybackStatus? lastPlaybackStatus;

        public PlaybackControlsModuleView()
        {
            InitializeComponent();
            SetInteractionEnabled(false);
        }

        public event EventHandler PreviousRequested;
        public event EventHandler PlayPauseRequested;
        public event EventHandler NextRequested;

        public void SetInteractionEnabled(bool value)
        {
            PreviousButton.IsHitTestVisible = value;
            PlayPauseButton.IsHitTestVisible = value;
            NextButton.IsHitTestVisible = value;
        }

        public void Update(IslandRenderState state)
        {
            var session = state?.Session;
            var hasSession = session != null;
            var playbackStatus = state?.PendingPlaybackStatus ?? session?.PlaybackStatus;
            if (renderStateInitialized &&
                lastHasSession == hasSession &&
                lastPlaybackStatus == playbackStatus)
            {
                return;
            }

            renderStateInitialized = true;
            lastHasSession = hasSession;
            lastPlaybackStatus = playbackStatus;
            PreviousButton.IsEnabled = session != null;
            PlayPauseButton.IsEnabled = session != null;
            NextButton.IsEnabled = session != null;
            var isPlaying = playbackStatus == MediaPlaybackStatus.Playing;
            PlayGlyph.Visibility = isPlaying ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            PauseGlyph.Visibility = isPlaying ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void PreviousButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PreviousRequested?.Invoke(this, EventArgs.Empty);
        }

        private void PlayPauseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void NextButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            AnimateButton((Button)sender, Color.FromArgb(0x24, 255, 255, 255), 1.0, 120);
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimateButton((Button)sender, Colors.Transparent, 1.0, 120);
        }

        private void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            AnimateButton((Button)sender, Color.FromArgb(0x3D, 255, 255, 255), 0.92, 80);
        }

        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AnimateButton((Button)sender, Color.FromArgb(0x24, 255, 255, 255), 1.0, 140);
        }

        private static void AnimateButton(Button button, Color color, double scale, int milliseconds)
        {
            var border = button.Template.FindName("HitBackground", button) as Border;
            if (border == null) return;
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            var brush = border.Background as SolidColorBrush;
            if (brush == null)
            {
                brush = new SolidColorBrush(Colors.Transparent);
            }
            else if (brush.IsFrozen)
            {
                brush = brush.Clone();
            }

            border.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(color, duration));
            var transform = border.RenderTransform as ScaleTransform;
            if (transform == null)
            {
                transform = new ScaleTransform(1, 1);
            }
            else if (transform.IsFrozen)
            {
                transform = transform.Clone();
            }

            border.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            border.RenderTransform = transform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration));
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration));
        }
    }
}
