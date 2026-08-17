using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace LyricHover.App.TaskbarLyrics
{
    // A deliberately small taskbar-only surface.  It has no island card/background and does
    // not consume playback actions; the normal island remains the interactive surface.
    public sealed class TaskbarLyricsWindow : Window, ITaskbarLyricsSurface
    {
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const uint SwpNoActivate = 0x0010;
        private readonly TextBlock primary = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock secondary = new TextBlock { FontSize = 11, Opacity = .78, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        private readonly TranslateTransform primaryTransform = new TranslateTransform();
        private readonly TranslateTransform secondaryTransform = new TranslateTransform();
        private readonly Grid textViewport = new Grid { ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
        private IntPtr handle;

        public TaskbarLyricsWindow()
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
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new Image { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center, Stretch = Stretch.Uniform, Source = LoadAppIcon() };
            root.Children.Add(icon);
            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            primary.RenderTransform = primaryTransform;
            secondary.RenderTransform = secondaryTransform;
            lines.Children.Add(primary);
            lines.Children.Add(secondary);
            textViewport.Children.Add(lines);
            textViewport.SizeChanged += (sender, args) => StartMarquee();
            Grid.SetColumn(textViewport, 1);
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

        void ITaskbarLyricsSurface.Show()
        {
            if (!IsVisible) Show();
            if (handle != IntPtr.Zero) SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoActivate | 0x0001 | 0x0002);
        }

        public void Present(LyricsPresentationSnapshot snapshot)
        {
            snapshot = snapshot ?? new LyricsPresentationSnapshot { IsWaitingForPlayback = true };
            primary.Text = snapshot.IsWaitingForPlayback && string.IsNullOrWhiteSpace(snapshot.PrimaryText) ? "等待播放" : snapshot.PrimaryText ?? string.Empty;
            secondary.Text = snapshot.SecondaryText ?? string.Empty;
            StartMarquee();
        }

        public void Place(TaskbarLyricsPlacement placement, double width)
        {
            Width = width / placement.DpiScale;
            Height = placement.Height / placement.DpiScale;
            Left = (placement.IsLeftAligned ? placement.Left : placement.Left + Math.Max(0, placement.Width - width) / 2) / placement.DpiScale;
            Top = placement.Top / placement.DpiScale;
            var foreground = placement.IsDarkTheme ? Brushes.White : Brushes.Black;
            primary.Foreground = foreground;
            secondary.Foreground = foreground;
        }

        private void StartMarquee()
        {
            primaryTransform.BeginAnimation(TranslateTransform.XProperty, null);
            secondaryTransform.BeginAnimation(TranslateTransform.XProperty, null);
            if (textViewport.ActualWidth <= 0 || primary.ActualWidth <= textViewport.ActualWidth) return;
            var distance = primary.ActualWidth - textViewport.ActualWidth + 12;
            var animation = new DoubleAnimation(0, -distance, TimeSpan.FromSeconds(Math.Max(8, distance / 10))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(1) };
            primaryTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private static ImageSource LoadAppIcon()
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/app-logo.png", UriKind.Absolute));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)] private static extern IntPtr GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)] private static extern IntPtr SetWindowLong(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
