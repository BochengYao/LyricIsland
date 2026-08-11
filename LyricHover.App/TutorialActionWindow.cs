using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LyricHover.App
{
    public sealed class TutorialActionWindow : Window
    {
        private const double ActionPadding = 28;
        private const double ActionTextHorizontalPadding = 48;

        public TutorialActionWindow(string text, Color background, Action clicked, Action exitRequested = null, bool emphasized = false)
        {
            var fontSize = emphasized ? 18 : 15;
            var buttonWidth = MeasureButtonWidth(
                text,
                fontSize,
                emphasized ? FontWeights.Bold : FontWeights.SemiBold,
                emphasized ? 176 : 142);
            Width = buttonWidth + ActionPadding * 2;
            Height = (emphasized ? 56 : 44) + ActionPadding * 2;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Opacity = 0;

            var button = new Button
            {
                Width = buttonWidth,
                Height = emphasized ? 56 : 44,
                Content = text ?? string.Empty,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(background),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = CreateButtonTemplate(background, emphasized)
            };
            button.Margin = new Thickness(ActionPadding);
            button.Click += (sender, args) => clicked?.Invoke();
            var root = new Grid { ClipToBounds = false };
            root.Children.Add(button);
            Content = root;
            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == System.Windows.Input.Key.Escape && exitRequested != null)
                {
                    exitRequested();
                    args.Handled = true;
                }
            };
        }

        public Task FadeInAsync(TimeSpan duration)
        {
            var completion = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation(1, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            animation.Completed += (sender, args) => completion.TrySetResult(true);
            BeginAnimation(OpacityProperty, animation);
            return completion.Task;
        }

        public Task FadeOutAsync(TimeSpan duration)
        {
            var completion = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation(Opacity, 0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            animation.Completed += (sender, args) =>
            {
                Opacity = 0;
                BeginAnimation(OpacityProperty, null);
                completion.TrySetResult(true);
            };
            BeginAnimation(OpacityProperty, animation);
            return completion.Task;
        }

        public Task PulseInAsync(TimeSpan duration)
        {
            var completion = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = duration,
                FillBehavior = FillBehavior.HoldEnd
            };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.18)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.32, KeyTime.FromPercent(0.34)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.52)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.32, KeyTime.FromPercent(0.68)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(1)));
            animation.Completed += (sender, args) => completion.TrySetResult(true);
            BeginAnimation(OpacityProperty, animation);
            return completion.Task;
        }

        public void BringForward()
        {
            Topmost = false;
            Topmost = true;
        }

        private static double MeasureButtonWidth(string text, double fontSize, FontWeight fontWeight, double minimumWidth)
        {
            var typeface = new Typeface(
                new FontFamily("Microsoft YaHei UI"),
                FontStyles.Normal,
                fontWeight,
                FontStretches.Normal);
            var formattedText = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.White,
                1.0);
            return Math.Ceiling(Math.Max(
                minimumWidth,
                formattedText.WidthIncludingTrailingWhitespace + ActionTextHorizontalPadding));
        }

        private static ControlTemplate CreateButtonTemplate(Color background, bool emphasized)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(emphasized ? 28 : 22));
            border.SetValue(Border.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = emphasized ? 24 : 18,
                ShadowDepth = emphasized ? 0 : 4,
                Opacity = emphasized ? 0.72 : 0.22,
                Color = emphasized ? background : Colors.Black
            });

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            return template;
        }
    }
}
