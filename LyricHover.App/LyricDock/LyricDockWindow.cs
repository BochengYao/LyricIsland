using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LyricHover.App.Modules;
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
        private const int WmNcHitTest = 0x0084;
        private const int WmLButtonDown = 0x0201;
        private const int WmRButtonDown = 0x0204;
        private const int WmRButtonUp = 0x0205;
        private const int WmContextMenu = 0x007B;
        private const int HtClient = 1;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const double PrimaryLineHeight = 18;
        private const double SecondaryLineHeight = 14;

        private readonly LyricTextTransitionTracker transitionTracker = new LyricTextTransitionTracker();
        private readonly Grid textViewport = new Grid { ClipToBounds = true };
        private readonly StackPanel currentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel incomingPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
        private readonly TranslateTransform currentSlide = new TranslateTransform();
        private readonly TranslateTransform incomingSlide = new TranslateTransform { Y = 10 };
        private readonly WordTrackingTextBlock currentPrimary = CreatePrimaryText();
        private readonly TextBlock currentSecondary = CreateSecondaryText();
        private readonly WordTrackingTextBlock incomingPrimary = CreatePrimaryText();
        private readonly TextBlock incomingSecondary = CreateSecondaryText();
        private readonly Grid currentPrimaryClip = CreateClipRow(PrimaryLineHeight);
        private readonly Grid currentSecondaryClip = CreateClipRow(SecondaryLineHeight);
        private readonly Grid incomingPrimaryClip = CreateClipRow(PrimaryLineHeight);
        private readonly Grid incomingSecondaryClip = CreateClipRow(SecondaryLineHeight);
        private readonly Func<bool> refreshModifierPressed;
        private Brush foreground = Brushes.White;
        private bool textLeftAligned;
        private IntPtr handle;
        private int transitionVersion;
        private TimeSpan lineDuration = TimeSpan.FromSeconds(4);
        private string displayedPrimary;
        private string displayedSecondary;
        private bool transitionInProgress;
        private LyricLine displayedWordTrackingLine;
        private TimeSpan displayedWordTrackingPosition;
        private bool displayedWordTrackingPlaying;
        private double displayedWordTrackingProgress = -1;

        public LyricDockWindow(Func<bool> refreshModifierPressed = null)
        {
            this.refreshModifierPressed = refreshModifierPressed ?? (() =>
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0);
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
            SourceInitialized += (sender, args) =>
            {
                handle = new WindowInteropHelper(this).Handle;
                var style = GetWindowLong(handle, GwlExStyle).ToInt64();
                SetWindowLong(handle, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
                HwndSource.FromHwnd(handle)?.AddHook(WindowMessageHook);
            };
        }

        public event EventHandler SettingsRequested;
        public event EventHandler RefreshRequested;

        private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // WPF's routed right-button event is not reliable for this transparent,
            // non-activating taskbar overlay.  Claim the native hit and context messages so
            // Explorer cannot open its own taskbar menu underneath the lyrics surface.
            if (message == WmNcHitTest)
            {
                handled = true;
                return new IntPtr(HtClient);
            }
            if (message == WmLButtonDown)
            {
                if (refreshModifierPressed())
                {
                    RefreshRequested?.Invoke(this, EventArgs.Empty);
                }
                handled = true;
                return IntPtr.Zero;
            }
            if (message == WmRButtonDown)
            {
                // Consume the press too: otherwise Explorer can remember it and show the
                // taskbar context menu after this no-activate overlay handles button-up.
                handled = true;
                return IntPtr.Zero;
            }
            if (message == WmRButtonUp)
            {
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                return IntPtr.Zero;
            }
            if (message == WmContextMenu)
            {
                handled = true;
            }
            return IntPtr.Zero;
        }

        void ILyricDockSurface.Show()
        {
            if (!IsVisible) Show();
            // Keep the transparent native surface above the taskbar itself; HWND_TOP can
            // leave a non-activating WPF window behind Explorer on some Windows builds.
            if (handle != IntPtr.Zero) SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoSize | SwpNoMove);
        }

        public void Present(LyricsPresentationSnapshot snapshot)
        {
            snapshot = snapshot ?? new LyricsPresentationSnapshot { IsWaitingForPlayback = true };
            var primary = snapshot.IsWaitingForPlayback && string.IsNullOrWhiteSpace(snapshot.PrimaryText)
                ? "等待播放"
                : snapshot.PrimaryText ?? string.Empty;
            var secondary = snapshot.SecondaryText ?? string.Empty;
            var wordTrackingProgress = snapshot.PrimaryWordTrackingProgress;
            if (snapshot.LineDuration > TimeSpan.Zero) lineDuration = snapshot.LineDuration;

            var textChanged = displayedPrimary != primary || displayedSecondary != secondary;
            displayedWordTrackingLine = snapshot.PrimaryWordTrackingLine;
            displayedWordTrackingPosition = snapshot.WordTrackingPosition;
            displayedWordTrackingPlaying = ResolveIsPlaying(snapshot);
            displayedWordTrackingProgress = wordTrackingProgress;

            if (!textChanged)
            {
                UpdateActiveWordTracking();
                return;
            }

            var shouldAnimate = transitionTracker.Update(primary, secondary);
            displayedPrimary = primary;
            displayedSecondary = secondary;
            var version = ++transitionVersion;
            transitionInProgress = false;

            if (!shouldAnimate)
            {
                ApplyCurrentText(primary, secondary);
                ResetSlideAnimation();
                StartCurrentMarquees();
                return;
            }

            ResetSlideAnimation();
            StopAllMarquees();
            PreparePrimaryLine(incomingPrimary, incomingPrimaryClip, primary, true);
            PrepareSecondaryLine(incomingSecondary, incomingSecondaryClip, secondary, !string.IsNullOrWhiteSpace(secondary));
            incomingPanel.Opacity = 0;
            incomingSlide.Y = 10;
            currentPanel.Opacity = 1;
            currentSlide.Y = 0;
            transitionInProgress = true;

            var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(280);
            var incomingOpacity = new DoubleAnimation(1, duration) { EasingFunction = easing };
            incomingOpacity.Completed += (sender, args) =>
            {
                if (version != transitionVersion)
                {
                    return;
                }

                transitionInProgress = false;
                ApplyCurrentText(displayedPrimary, displayedSecondary);
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

        private static WordTrackingTextBlock CreatePrimaryText()
        {
            return new WordTrackingTextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                DimOpacity = 0.45,
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

        private static void AssemblePanel(StackPanel panel, WordTrackingTextBlock primary, TextBlock secondary, Grid primaryClip, Grid secondaryClip)
        {
            ((Canvas)primaryClip.Children[0]).Children.Add(primary);
            ((Canvas)secondaryClip.Children[0]).Children.Add(secondary);
            panel.Children.Add(primaryClip);
            panel.Children.Add(secondaryClip);
        }

        private void ApplyForeground(WordTrackingTextBlock textBlock)
        {
            textBlock.Foreground = foreground;
        }

        private void ApplyForeground(TextBlock textBlock)
        {
            textBlock.Foreground = foreground;
        }

        private void PreparePrimaryLine(WordTrackingTextBlock textBlock, Grid clip, string text, bool visible)
        {
            PresentWordTracking(textBlock, text);
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

        private void PrepareSecondaryLine(TextBlock textBlock, Grid clip, string text, bool visible)
        {
            textBlock.Text = text ?? string.Empty;
            textBlock.Foreground = foreground;
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
            PreparePrimaryLine(currentPrimary, currentPrimaryClip, primary, true);
            PrepareSecondaryLine(currentSecondary, currentSecondaryClip, secondary, !string.IsNullOrWhiteSpace(secondary));
        }

        private void ReapplyCurrentText()
        {
            transitionVersion++;
            transitionInProgress = false;
            ApplyCurrentText(displayedPrimary ?? string.Empty, displayedSecondary ?? string.Empty);
            ResetSlideAnimation();
            StartCurrentMarquees();
        }

        private void ResetSlideAnimation()
        {
            incomingPrimary.StopPlaybackProjection();
            currentPanel.BeginAnimation(OpacityProperty, null);
            currentSlide.BeginAnimation(TranslateTransform.YProperty, null);
            incomingPanel.BeginAnimation(OpacityProperty, null);
            incomingSlide.BeginAnimation(TranslateTransform.YProperty, null);

            currentPanel.Opacity = 1;
            currentSlide.Y = 0;
            incomingPanel.Opacity = 0;
            incomingSlide.Y = 10;
        }

        private void UpdateActiveWordTracking()
        {
            var target = transitionInProgress ? incomingPrimary : currentPrimary;
            PresentWordTracking(target, displayedPrimary);
        }

        private void PresentWordTracking(WordTrackingTextBlock textBlock, string text)
        {
            textBlock.Present(
                text,
                displayedWordTrackingLine,
                displayedWordTrackingPosition,
                displayedWordTrackingPlaying,
                displayedWordTrackingProgress);
        }

        private static bool ResolveIsPlaying(LyricsPresentationSnapshot snapshot)
        {
            var status = snapshot.PendingPlaybackStatus ?? snapshot.Session?.PlaybackStatus;
            return status == LyricHover.Core.Media.MediaPlaybackStatus.Playing;
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

        private static void StopMarquee(FrameworkElement textBlock)
        {
            var transform = (TranslateTransform)textBlock.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
        }

        private void StartMarqueeIfNeeded(FrameworkElement textBlock, Grid clip)
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

        private LyricTextPlacement MeasureAndPosition(FrameworkElement textBlock, Grid clip)
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




