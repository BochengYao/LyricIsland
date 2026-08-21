using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LyricHover.Core;

namespace LyricHover.App.LyricDock
{
    // A deliberately small taskbar-only surface.  It has no island card/background and does
    // not consume playback actions; the normal island remains the interactive surface.
    // Lyric line changes replay the island's fade+slide transition, overlong lines marquee
    // horizontally instead of being trimmed with an ellipsis, and a single-line snapshot
    // (single-line mode) vertically centers its only row in the taskbar.
    public sealed class LyricDockWindow : Window, ILyricDockSurface
    {
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const uint SwpNoActivate = 0x0010;
        private const double PrimaryLineHeight = 18;
        private const double SecondaryLineHeight = 14;

        private readonly LyricTextTransitionTracker transitionTracker = new LyricTextTransitionTracker();
        private readonly Grid textViewport = new Grid { ClipToBounds = true };
        private readonly StackPanel currentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel incomingPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
        private readonly TranslateTransform currentSlide = new TranslateTransform();
        private readonly TranslateTransform incomingSlide = new TranslateTransform { Y = 10 };
        private readonly TextBlock currentPrimary = CreatePrimaryText();
        private readonly TextBlock currentSecondary = CreateSecondaryText();
        private readonly TextBlock incomingPrimary = CreatePrimaryText();
        private readonly TextBlock incomingSecondary = CreateSecondaryText();
        private readonly Grid currentPrimaryClip = CreateClipRow(PrimaryLineHeight);
        private readonly Grid currentSecondaryClip = CreateClipRow(SecondaryLineHeight);
        private readonly Grid incomingPrimaryClip = CreateClipRow(PrimaryLineHeight);
        private readonly Grid incomingSecondaryClip = CreateClipRow(SecondaryLineHeight);
        private Brush foreground = Brushes.White;
        private bool textLeftAligned;
        private IntPtr handle;
        private int transitionVersion;
        private TimeSpan lineDuration = TimeSpan.FromSeconds(4);
        private string displayedPrimary;
        private string displayedSecondary;

        public LyricDockWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            Focusable = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            var root = new Grid { Background = Brushes.Transparent, Margin = new Thickness(8, 0, 8, 0) };

            currentPanel.RenderTransform = currentSlide;
            incomingPanel.RenderTransform = incomingSlide;
            AssemblePanel(currentPanel, currentPrimary, currentSecondary, currentPrimaryClip, currentSecondaryClip);
            AssemblePanel(incomingPanel, incomingPrimary, incomingSecondary, incomingPrimaryClip, incomingSecondaryClip);
            textViewport.Children.Add(currentPanel);
            textViewport.Children.Add(incomingPanel);
            // A viewport resize (taskbar placement change) invalidates marquee measurements;
            // re-apply the current text so overflow detection and centering run again.
            textViewport.SizeChanged += (sender, args) => ReapplyCurrentText();
            root.Children.Add(textViewport);
            Content = root;
            PreviewMouseLeftButtonDown += (sender, args) => args.Handled = true;
            PreviewMouseRightButtonUp += (sender, args) => { SettingsRequested?.Invoke(this, EventArgs.Empty); args.Handled = true; };
            SourceInitialized += (sender, args) =>
            {
                handle = new WindowInteropHelper(this).Handle;
                var style = GetWindowLong(handle, GwlExStyle).ToInt64();
                SetWindowLong(handle, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
            };
        }

        public event EventHandler SettingsRequested;

        void ILyricDockSurface.Show()
        {
            if (!IsVisible) Show();
            if (handle != IntPtr.Zero) SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoActivate | 0x0001 | 0x0002);
        }

        public void Present(LyricsPresentationSnapshot snapshot)
        {
            snapshot = snapshot ?? new LyricsPresentationSnapshot { IsWaitingForPlayback = true };
            var primary = snapshot.IsWaitingForPlayback && string.IsNullOrWhiteSpace(snapshot.PrimaryText)
                ? "等待播放"
                : snapshot.PrimaryText ?? string.Empty;
            var secondary = snapshot.SecondaryText ?? string.Empty;
            if (snapshot.LineDuration > TimeSpan.Zero) lineDuration = snapshot.LineDuration;

            if (displayedPrimary == primary && displayedSecondary == secondary)
            {
                return;
            }

            var shouldAnimate = transitionTracker.Update(primary, secondary);
            displayedPrimary = primary;
            displayedSecondary = secondary;
            var version = ++transitionVersion;

            if (!shouldAnimate)
            {
                ApplyCurrentText(primary, secondary);
                ResetSlideAnimation();
                StartCurrentMarquees();
                return;
            }

            ResetSlideAnimation();
            StopAllMarquees();
            PrepareLine(incomingPrimary, incomingPrimaryClip, primary, true);
            PrepareLine(incomingSecondary, incomingSecondaryClip, secondary, !string.IsNullOrWhiteSpace(secondary));
            incomingPanel.Opacity = 0;
            incomingSlide.Y = 10;
            currentPanel.Opacity = 1;
            currentSlide.Y = 0;

            var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(280);
            var incomingOpacity = new DoubleAnimation(1, duration) { EasingFunction = easing };
            incomingOpacity.Completed += (sender, args) =>
            {
                if (version != transitionVersion)
                {
                    return;
                }

                ApplyCurrentText(primary, secondary);
                ResetSlideAnimation();
                StartCurrentMarquees();
            };

            currentPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration) { EasingFunction = easing });
            currentSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-9, duration) { EasingFunction = easing });
            incomingPanel.BeginAnimation(OpacityProperty, incomingOpacity);
            incomingSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { EasingFunction = easing });
        }

        public void Place(LyricDockPlacement placement, double width)
        {
            Width = width / placement.DpiScale;
            Height = placement.Height / placement.DpiScale;
            // The alignment setting positions the TEXT inside this window (like the island's
            // lyrics module), not the window inside the taskbar gap: the window always starts
            // at the gap's left edge and spans up to MaximumWidth, and each lyric line is
            // then left-aligned or centered within the viewport.
            textLeftAligned = placement.IsLeftAligned;
            Left = placement.Left / placement.DpiScale;
            Top = placement.Top / placement.DpiScale;
            foreground = placement.IsDarkTheme ? Brushes.White : Brushes.Black;
            ApplyForeground(currentPrimary);
            ApplyForeground(currentSecondary);
            ApplyForeground(incomingPrimary);
            ApplyForeground(incomingSecondary);
        }

        private static TextBlock CreatePrimaryText()
        {
            return new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new TranslateTransform()
            };
        }

        private static TextBlock CreateSecondaryText()
        {
            return new TextBlock
            {
                FontSize = 11,
                Opacity = .78,
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new TranslateTransform()
            };
        }

        private static Grid CreateClipRow(double height)
        {
            var grid = new Grid { Height = height, ClipToBounds = true };
            grid.Children.Add(new Canvas { Height = height });
            return grid;
        }

        private static void AssemblePanel(StackPanel panel, TextBlock primary, TextBlock secondary, Grid primaryClip, Grid secondaryClip)
        {
            ((Canvas)primaryClip.Children[0]).Children.Add(primary);
            ((Canvas)secondaryClip.Children[0]).Children.Add(secondary);
            panel.Children.Add(primaryClip);
            panel.Children.Add(secondaryClip);
        }

        private void ApplyForeground(TextBlock textBlock)
        {
            textBlock.Foreground = foreground;
        }

        private void PrepareLine(TextBlock textBlock, Grid clip, string text, bool visible)
        {
            textBlock.Text = text ?? string.Empty;
            textBlock.Foreground = foreground;
            // Collapse the whole clip row (not just the text) so an absent secondary line
            // removes its reserved height: in single-line mode the remaining row is then
            // vertically centered by the panel's VerticalAlignment.
            textBlock.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            clip.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible)
            {
                MeasureAndPosition(textBlock, clip);
            }
        }

        private void ApplyCurrentText(string primary, string secondary)
        {
            StopAllMarquees();
            PrepareLine(currentPrimary, currentPrimaryClip, primary, true);
            PrepareLine(currentSecondary, currentSecondaryClip, secondary, !string.IsNullOrWhiteSpace(secondary));
        }

        private void ReapplyCurrentText()
        {
            transitionVersion++;
            ApplyCurrentText(displayedPrimary ?? string.Empty, displayedSecondary ?? string.Empty);
            ResetSlideAnimation();
            StartCurrentMarquees();
        }

        private void ResetSlideAnimation()
        {
            currentPanel.BeginAnimation(OpacityProperty, null);
            currentSlide.BeginAnimation(TranslateTransform.YProperty, null);
            incomingPanel.BeginAnimation(OpacityProperty, null);
            incomingSlide.BeginAnimation(TranslateTransform.YProperty, null);

            currentPanel.Opacity = 1;
            currentSlide.Y = 0;
            incomingPanel.Opacity = 0;
            incomingSlide.Y = 10;
        }

        private void StartCurrentMarquees()
        {
            StartMarqueeIfNeeded(currentPrimary, currentPrimaryClip);
            StartMarqueeIfNeeded(currentSecondary, currentSecondaryClip);
        }

        private void StopAllMarquees()
        {
            StopMarquee(currentPrimary);
            StopMarquee(currentSecondary);
            StopMarquee(incomingPrimary);
            StopMarquee(incomingSecondary);
        }

        private static void StopMarquee(TextBlock textBlock)
        {
            var transform = (TranslateTransform)textBlock.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
        }

        private void StartMarqueeIfNeeded(TextBlock textBlock, Grid clip)
        {
            if (textBlock.Visibility != Visibility.Visible)
            {
                return;
            }

            var placement = MeasureAndPosition(textBlock, clip);
            if (!placement.RequiresMarquee)
            {
                return;
            }

            var duration = TimeSpan.FromMilliseconds(Math.Max(1800, lineDuration.TotalMilliseconds - 450));
            var animation = new DoubleAnimation
            {
                From = 0,
                To = -placement.Overflow,
                BeginTime = TimeSpan.FromMilliseconds(260),
                Duration = duration,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            ((TranslateTransform)textBlock.RenderTransform).BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private LyricTextPlacement MeasureAndPosition(TextBlock textBlock, Grid clip)
        {
            var transform = (TranslateTransform)textBlock.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
            // Measure at infinite width so the text block keeps its natural (untrimmed) size;
            // the clip row hides the overflow while the marquee transform scrolls it through.
            textBlock.Width = double.NaN;
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = textBlock.DesiredSize.Width;
            textBlock.Width = textWidth;

            var availableWidth = clip.ActualWidth > 0 ? clip.ActualWidth : textViewport.ActualWidth;
            // The taskbar-lyrics alignment preference controls where a fitting line sits
            // inside the viewport; overflowing lines always marquee from the left edge.
            var placement = LyricTextPlacement.Calculate(availableWidth, textWidth, 28, textLeftAligned);
            Canvas.SetLeft(textBlock, placement.Left);
            return placement;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)] private static extern IntPtr GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)] private static extern IntPtr SetWindowLong(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}




