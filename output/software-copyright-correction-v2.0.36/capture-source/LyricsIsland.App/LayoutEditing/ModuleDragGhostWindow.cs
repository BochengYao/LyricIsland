using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using LyricsIsland.Core.Layout;
using Forms = System.Windows.Forms;

namespace LyricsIsland.App.LayoutEditing
{
    public sealed class ModuleDragGhostWindow : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;

        public ModuleDragGhostWindow(
            ModuleToolboxItemDescriptor descriptor,
            ResourceDictionary sourceResources = null)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            CopyThemeResources(sourceResources);
            Content = new ModuleToolboxCard
            {
                Descriptor = descriptor ?? ModuleToolboxCatalog.Get(IslandModuleType.AlbumArt),
                Opacity = 0.92,
                IsHitTestVisible = false
            };

            SourceInitialized += (sender, args) => MakeMouseTransparent();
        }

        public void UpdatePosition()
        {
            var cursor = Forms.Cursor.Position;
            var source = PresentationSource.FromVisual(this);
            var point = new Point(cursor.X, cursor.Y);
            if (source?.CompositionTarget != null)
            {
                point = source.CompositionTarget.TransformFromDevice.Transform(point);
            }

            Left = point.X - ActualWidth / 2;
            Top = point.Y - ActualHeight / 2;
        }

        private void CopyThemeResources(ResourceDictionary sourceResources)
        {
            CopyBrush(sourceResources, "SettingsControlBackgroundBrush", Color.FromRgb(32, 37, 46));
            CopyBrush(sourceResources, "SettingsControlBorderBrush", Color.FromRgb(78, 88, 104));
            CopyBrush(sourceResources, "SettingsControlForegroundBrush", Colors.White);
            CopyBrush(sourceResources, "SettingsControlMutedForegroundBrush", Color.FromRgb(215, 220, 229));
        }

        private void CopyBrush(ResourceDictionary sourceResources, string key, Color fallback)
        {
            var brush = sourceResources?[key] as SolidColorBrush;
            Resources[key] = new SolidColorBrush(brush?.Color ?? fallback);
        }

        private void MakeMouseTransparent()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(handle, GwlExStyle);
            SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    }
}
