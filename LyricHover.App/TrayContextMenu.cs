using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LyricHover.App
{
    internal static class TrayContextMenuFactory
    {
        public static Forms.ContextMenuStrip Create(Action openSettings, Action exitApplication)
        {
            if (openSettings == null)
            {
                throw new ArgumentNullException(nameof(openSettings));
            }

            if (exitApplication == null)
            {
                throw new ArgumentNullException(nameof(exitApplication));
            }

            var menu = new Forms.ContextMenuStrip
            {
                AutoSize = true,
                BackColor = Color.White,
                DropShadowEnabled = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                MinimumSize = new Size(204, 0),
                Padding = new Forms.Padding(7, 8, 7, 8),
                ShowCheckMargin = false,
                ShowImageMargin = false
            };

            menu.Items.Add(CreateItem("TraySettingsMenuItem", "偏好设置", openSettings));
            menu.Items.Add(new Forms.ToolStripSeparator
            {
                AutoSize = false,
                Margin = new Forms.Padding(12, 4, 12, 4),
                Size = new Size(180, 1)
            });
            menu.Items.Add(CreateItem("TrayExitMenuItem", "退出", exitApplication));
            menu.Opened += (sender, args) => TrayContextMenuTheme.ApplyRoundedRegion(menu);
            menu.SizeChanged += (sender, args) => TrayContextMenuTheme.ApplyRoundedRegion(menu);
            return menu;
        }

        private static Forms.ToolStripMenuItem CreateItem(string name, string text, Action action)
        {
            var item = new Forms.ToolStripMenuItem(text)
            {
                AutoSize = false,
                Name = name,
                Padding = new Forms.Padding(42, 0, 14, 0),
                Size = new Size(190, 40),
                TextAlign = ContentAlignment.MiddleLeft
            };
            item.Click += (sender, args) => action();
            return item;
        }
    }

    internal static class TrayContextMenuTheme
    {
        public static void Apply(Forms.ContextMenuStrip menu, SettingsThemePreference preference)
        {
            if (menu == null)
            {
                return;
            }

            var palette = TrayMenuPalette.Create(
                ResolveDarkTheme(preference),
                Forms.SystemInformation.HighContrast);
            menu.SuspendLayout();
            menu.BackColor = palette.Background;
            menu.ForeColor = palette.Foreground;
            menu.Renderer = new TrayMenuRenderer(palette);
            foreach (Forms.ToolStripItem item in menu.Items)
            {
                item.BackColor = palette.Background;
                item.ForeColor = palette.Foreground;
            }
            menu.ResumeLayout();
            ApplyRoundedRegion(menu);
            menu.Invalidate(true);
        }

        public static bool ResolveDarkTheme(SettingsThemePreference preference)
        {
            if (preference == SettingsThemePreference.Dark)
            {
                return true;
            }

            if (preference == SettingsThemePreference.Light)
            {
                return false;
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyRoundedRegion(Forms.ContextMenuStrip menu)
        {
            if (menu == null || menu.Width <= 0 || menu.Height <= 0)
            {
                return;
            }

            var scale = Math.Max(1F, menu.DeviceDpi / 96F);
            using (var path = TrayMenuRenderer.CreateRoundedPath(
                new Rectangle(0, 0, menu.Width, menu.Height),
                12F * scale))
            {
                var oldRegion = menu.Region;
                menu.Region = new Region(path);
                oldRegion?.Dispose();
            }
        }
    }

    internal sealed class TrayMenuRenderer : Forms.ToolStripRenderer
    {
        private readonly TrayMenuPalette palette;

        public TrayMenuRenderer(TrayMenuPalette palette)
        {
            this.palette = palette;
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            var bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var path = CreateRoundedPath(bounds, Scale(e.Graphics, 12F)))
            using (var brush = new SolidBrush(palette.Background))
            {
                WithAntialiasing(e.Graphics, () => e.Graphics.FillPath(brush, path));
            }
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            var bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var path = CreateRoundedPath(bounds, Scale(e.Graphics, 12F)))
            using (var pen = new Pen(palette.Border, Math.Max(1F, Scale(e.Graphics, 1F))))
            {
                WithAntialiasing(e.Graphics, () => e.Graphics.DrawPath(pen, path));
            }
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            var item = e.Item as Forms.ToolStripMenuItem;
            if (item == null || (!item.Selected && !item.Pressed))
            {
                return;
            }

            var inset = (int)Math.Round(Scale(e.Graphics, 4F));
            var bounds = new Rectangle(
                inset,
                (int)Math.Round(Scale(e.Graphics, 2F)),
                Math.Max(1, item.Width - (inset * 2)),
                Math.Max(1, item.Height - (int)Math.Round(Scale(e.Graphics, 4F))));
            var fill = item.Pressed ? palette.PressedBackground : palette.HoverBackground;
            using (var path = CreateRoundedPath(bounds, Scale(e.Graphics, 8F)))
            using (var brush = new SolidBrush(fill))
            {
                WithAntialiasing(e.Graphics, () => e.Graphics.FillPath(brush, path));
            }
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled
                ? palette.MutedForeground
                : e.Item.Selected ? palette.SelectedForeground : palette.Foreground;
            e.TextFormat |= Forms.TextFormatFlags.VerticalCenter;
            base.OnRenderItemText(e);
            DrawItemGlyph(e.Graphics, e.Item);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using (var pen = new Pen(palette.Separator, Math.Max(1F, Scale(e.Graphics, 1F))))
            {
                e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
            }
        }

        private void DrawItemGlyph(Graphics graphics, Forms.ToolStripItem item)
        {
            var glyphColor = !item.Enabled
                ? palette.MutedForeground
                : item.Selected ? palette.SelectedForeground : palette.Glyph;
            using (var pen = new Pen(glyphColor, Math.Max(1.4F, Scale(graphics, 1.5F))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                WithAntialiasing(graphics, () =>
                {
                    if (string.Equals(item.Name, "TraySettingsMenuItem", StringComparison.Ordinal))
                    {
                        DrawSettingsGlyph(graphics, pen, Scale(graphics, 20F), item.Height / 2F);
                    }
                    else if (string.Equals(item.Name, "TrayExitMenuItem", StringComparison.Ordinal))
                    {
                        DrawExitGlyph(graphics, pen, Scale(graphics, 20F), item.Height / 2F);
                    }
                });
            }
        }

        private static void DrawSettingsGlyph(Graphics graphics, Pen pen, float centerX, float centerY)
        {
            var innerRadius = Scale(graphics, 3F);
            var outerRadius = Scale(graphics, 7F);
            graphics.DrawEllipse(pen, centerX - innerRadius, centerY - innerRadius, innerRadius * 2, innerRadius * 2);
            for (var index = 0; index < 8; index++)
            {
                var angle = Math.PI * index / 4D;
                var startX = centerX + (float)Math.Cos(angle) * (outerRadius - Scale(graphics, 2F));
                var startY = centerY + (float)Math.Sin(angle) * (outerRadius - Scale(graphics, 2F));
                var endX = centerX + (float)Math.Cos(angle) * outerRadius;
                var endY = centerY + (float)Math.Sin(angle) * outerRadius;
                graphics.DrawLine(pen, startX, startY, endX, endY);
            }
        }

        private static void DrawExitGlyph(Graphics graphics, Pen pen, float centerX, float centerY)
        {
            var radius = Scale(graphics, 7F);
            graphics.DrawArc(pen, centerX - radius, centerY - radius, radius * 2, radius * 2, -48F, 276F);
            graphics.DrawLine(
                pen,
                centerX,
                centerY - Scale(graphics, 9F),
                centerX,
                centerY - Scale(graphics, 1F));
        }

        internal static GraphicsPath CreateRoundedPath(Rectangle bounds, float radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return path;
            }

            var diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 1F)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new RectangleF(bounds.Left, bounds.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static float Scale(Graphics graphics, float value)
        {
            return value * Math.Max(1F, graphics.DpiX / 96F);
        }

        private static void WithAntialiasing(Graphics graphics, Action render)
        {
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                render();
            }
            finally
            {
                graphics.SmoothingMode = previous;
            }
        }
    }

    internal sealed class TrayMenuPalette
    {
        public Color Background { get; private set; }
        public Color Border { get; private set; }
        public Color Foreground { get; private set; }
        public Color SelectedForeground { get; private set; }
        public Color MutedForeground { get; private set; }
        public Color Glyph { get; private set; }
        public Color HoverBackground { get; private set; }
        public Color PressedBackground { get; private set; }
        public Color Separator { get; private set; }

        public static TrayMenuPalette Create(bool dark, bool highContrast)
        {
            if (highContrast)
            {
                return new TrayMenuPalette
                {
                    Background = SystemColors.Window,
                    Border = SystemColors.WindowFrame,
                    Foreground = SystemColors.WindowText,
                    SelectedForeground = SystemColors.HighlightText,
                    MutedForeground = SystemColors.GrayText,
                    Glyph = SystemColors.WindowText,
                    HoverBackground = SystemColors.Highlight,
                    PressedBackground = SystemColors.Highlight,
                    Separator = SystemColors.WindowFrame
                };
            }

            return dark
                ? new TrayMenuPalette
                {
                    Background = Color.FromArgb(255, 44, 44, 46),
                    Border = Color.FromArgb(255, 72, 72, 74),
                    Foreground = Color.FromArgb(255, 245, 245, 247),
                    SelectedForeground = Color.FromArgb(255, 245, 245, 247),
                    MutedForeground = Color.FromArgb(255, 142, 142, 147),
                    Glyph = Color.FromArgb(255, 174, 174, 178),
                    HoverBackground = Color.FromArgb(255, 58, 58, 60),
                    PressedBackground = Color.FromArgb(255, 72, 72, 74),
                    Separator = Color.FromArgb(255, 66, 66, 69)
                }
                : new TrayMenuPalette
                {
                    Background = Color.FromArgb(255, 255, 255, 255),
                    Border = Color.FromArgb(255, 218, 218, 223),
                    Foreground = Color.FromArgb(255, 29, 29, 31),
                    SelectedForeground = Color.FromArgb(255, 29, 29, 31),
                    MutedForeground = Color.FromArgb(255, 134, 134, 139),
                    Glyph = Color.FromArgb(255, 99, 99, 102),
                    HoverBackground = Color.FromArgb(255, 242, 242, 247),
                    PressedBackground = Color.FromArgb(255, 229, 229, 234),
                    Separator = Color.FromArgb(255, 232, 232, 237)
                };
        }
    }
}
