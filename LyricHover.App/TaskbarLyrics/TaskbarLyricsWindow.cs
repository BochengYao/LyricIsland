using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LyricHover.App.Modules;

namespace LyricHover.App.TaskbarLyrics
{
    public sealed class TaskbarLyricsWindow : Window, ITaskbarLyricsSurface
    {
        private readonly LyricsModuleView lyrics = new LyricsModuleView();
        public event EventHandler SettingsRequested;

        public TaskbarLyricsWindow()
        {
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
            ShowActivated = false; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; Focusable = false; WindowStartupLocation = WindowStartupLocation.Manual;
            var root = new Grid { Background = Brushes.Transparent, Margin = new Thickness(8, 0, 8, 0) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(new TextBlock { Text = "♪", FontSize = 16, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
            Grid.SetColumn(lyrics, 1); lyrics.VerticalAlignment = VerticalAlignment.Center; root.Children.Add(lyrics);
            Content = root;
            PreviewMouseLeftButtonDown += (s, e) => e.Handled = true;
            PreviewMouseRightButtonUp += (s, e) => { SettingsRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; };
        }

        public void Present(LyricsPresentationSnapshot snapshot) { lyrics.Update((snapshot ?? new LyricsPresentationSnapshot()).ToIslandRenderState()); }
        public void Place(TaskbarLyricsPlacement placement, double width)
        {
            Width = width / placement.DpiScale; Height = placement.Height / placement.DpiScale;
            lyrics.ApplyModuleSettings(Math.Max(180, width - 42));
            Left = (placement.Left + Math.Max(0, (placement.Width - width) / 2)) / placement.DpiScale;
            if (placement.IsLeftAligned) Left = (placement.Left + 8) / placement.DpiScale;
            Top = placement.Top / placement.DpiScale;
        }
    }
}
