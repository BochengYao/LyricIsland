using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AppleMusicDesktopLyrics.Core;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public partial class LyricsModuleView : UserControl, IIslandModuleView
    {
        private readonly LyricTextTransitionTracker lyricTextTransitionTracker = new LyricTextTransitionTracker();
        private string displayedPrimary;
        private string displayedSecondary;

        public LyricsModuleView()
        {
            InitializeComponent();
        }

        public void Update(IslandRenderState state)
        {
            state = state ?? new IslandRenderState();
            SetIslandText(state.PrimaryLyric, state.SecondaryLyric, state.LineDuration);
        }

        private void SetIslandText(string primary, string secondary, TimeSpan lineDuration)
        {
            primary = primary ?? string.Empty;
            secondary = secondary ?? string.Empty;

            if (displayedPrimary == primary && displayedSecondary == secondary)
            {
                return;
            }

            var shouldAnimate = lyricTextTransitionTracker.Update(primary, secondary);
            displayedPrimary = primary;
            displayedSecondary = secondary;

            if (!shouldAnimate)
            {
                ApplyCurrentLyricsText(primary, secondary);
                ResetLyricsAnimationState();
                QueueMarquee(lineDuration);
                return;
            }

            IncomingPrimaryLyricText.Text = primary;
            IncomingSecondaryLyricText.Text = secondary;
            IncomingSecondaryLyricText.Visibility = string.IsNullOrWhiteSpace(secondary) ? Visibility.Collapsed : Visibility.Visible;
            IncomingLyricsPanel.Opacity = 0;
            IncomingLyricsTransform.Y = 10;
            CurrentLyricsPanel.Opacity = 1;
            CurrentLyricsTransform.Y = 0;

            var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(280);

            var outgoingOpacity = new DoubleAnimation(0, duration) { EasingFunction = easing };
            var outgoingMove = new DoubleAnimation(-9, duration) { EasingFunction = easing };
            var incomingOpacity = new DoubleAnimation(1, duration) { EasingFunction = easing };
            var incomingMove = new DoubleAnimation(0, duration) { EasingFunction = easing };

            incomingOpacity.Completed += (sender, args) =>
            {
                ApplyCurrentLyricsText(primary, secondary);
                ResetLyricsAnimationState();
                QueueMarquee(lineDuration);
            };

            CurrentLyricsPanel.BeginAnimation(OpacityProperty, outgoingOpacity);
            CurrentLyricsTransform.BeginAnimation(TranslateTransform.YProperty, outgoingMove);
            IncomingLyricsPanel.BeginAnimation(OpacityProperty, incomingOpacity);
            IncomingLyricsTransform.BeginAnimation(TranslateTransform.YProperty, incomingMove);
        }

        private void ApplyCurrentLyricsText(string primary, string secondary)
        {
            StopMarquee();
            PrimaryLyricText.Text = primary ?? string.Empty;
            SecondaryLyricText.Text = secondary ?? string.Empty;
            SecondaryLyricText.Visibility = string.IsNullOrWhiteSpace(secondary) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ResetLyricsAnimationState()
        {
            CurrentLyricsPanel.BeginAnimation(OpacityProperty, null);
            CurrentLyricsTransform.BeginAnimation(TranslateTransform.YProperty, null);
            IncomingLyricsPanel.BeginAnimation(OpacityProperty, null);
            IncomingLyricsTransform.BeginAnimation(TranslateTransform.YProperty, null);

            CurrentLyricsPanel.Opacity = 1;
            CurrentLyricsTransform.Y = 0;
            IncomingLyricsPanel.Opacity = 0;
            IncomingLyricsTransform.Y = 10;
        }

        private void QueueMarquee(TimeSpan lineDuration)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StartMarqueeIfNeeded(PrimaryLyricText, PrimaryLyricTransform, PrimaryLyricClip, lineDuration);
                StartMarqueeIfNeeded(SecondaryLyricText, SecondaryLyricTransform, SecondaryLyricClip, lineDuration);
            }), DispatcherPriority.Background);
        }

        private void StartMarqueeIfNeeded(TextBlock textBlock, TranslateTransform transform, FrameworkElement clip, TimeSpan lineDuration)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            textBlock.Width = double.NaN;
            Canvas.SetLeft(textBlock, 0);

            if (string.IsNullOrWhiteSpace(textBlock.Text) || textBlock.Visibility != Visibility.Visible)
            {
                return;
            }

            var availableWidth = clip.ActualWidth;
            if (availableWidth <= 0)
            {
                availableWidth = 436;
            }

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = textBlock.DesiredSize.Width;
            textBlock.Width = textWidth;

            if (textWidth <= availableWidth)
            {
                Canvas.SetLeft(textBlock, (availableWidth - textWidth) / 2);
                return;
            }

            var overflow = textWidth - availableWidth + 28;
            var duration = TimeSpan.FromMilliseconds(Math.Max(1800, lineDuration.TotalMilliseconds - 450));
            Canvas.SetLeft(textBlock, 0);

            var animation = new DoubleAnimation
            {
                From = 0,
                To = -overflow,
                BeginTime = TimeSpan.FromMilliseconds(260),
                Duration = duration,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void StopMarquee()
        {
            PrimaryLyricTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SecondaryLyricTransform.BeginAnimation(TranslateTransform.XProperty, null);
            PrimaryLyricTransform.X = 0;
            SecondaryLyricTransform.X = 0;
            PrimaryLyricText.Width = double.NaN;
            SecondaryLyricText.Width = double.NaN;
            Canvas.SetLeft(PrimaryLyricText, 0);
            Canvas.SetLeft(SecondaryLyricText, 0);
        }
    }
}
