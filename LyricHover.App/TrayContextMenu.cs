using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
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
                BackColor = Color.FromArgb(255, 248, 248, 250),
                DropShadowEnabled = true,
                Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Regular, GraphicsUnit.Point),
                MinimumSize = new Size(160, 0),
                Padding = new Forms.Padding(5),
                ShowCheckMargin = false,
                ShowImageMargin = false
            };

            var settingsItem = CreateItem("TraySettingsMenuItem", "偏好设置", openSettings);
            settingsItem.Margin = new Forms.Padding(5, 3, 5, 1);
            menu.Items.Add(settingsItem);
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
                Margin = new Forms.Padding(5, 0, 5, 3),
                Name = name,
                Padding = new Forms.Padding(8, 0, 8, 0),
                Size = new Size(150, 33),
                TextAlign = ContentAlignment.MiddleLeft
            };
            item.Click += (sender, args) => action();
            return item;
        }
    }

    internal static class TrayContextMenuTheme
    {
        private const int DwmWindowCornerPreference = 33;
        private const int DwmCornerRound = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);

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

            if (menu.IsHandleCreated)
            {
                var oldRegion = menu.Region;
                menu.Region = null;
                oldRegion?.Dispose();
                if (TryApplyDwmRoundedCorners(menu))
                {
                    return;
                }
            }

            var scale = Math.Max(1F, menu.DeviceDpi / 96F);
            using (var path = TrayMenuRenderer.CreateRoundedPath(
                new Rectangle(0, 0, menu.Width, menu.Height),
                10F * scale))
            {
                var oldRegion = menu.Region;
                menu.Region = new Region(path);
                oldRegion?.Dispose();
            }
        }

        private static bool TryApplyDwmRoundedCorners(Forms.ContextMenuStrip menu)
        {
            try
            {
                var preference = DwmCornerRound;
                return DwmSetWindowAttribute(
                    menu.Handle,
                    DwmWindowCornerPreference,
                    ref preference,
                    sizeof(int)) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
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
            using (var path = CreateRoundedPath(bounds, Scale(e.Graphics, 10F)))
            using (var brush = new SolidBrush(palette.Background))
            {
                WithVectorQuality(e.Graphics, () => e.Graphics.FillPath(brush, path));
            }
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            var bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var path = CreateRoundedPath(bounds, Scale(e.Graphics, 10F)))
            using (var pen = new Pen(palette.Border, Math.Max(1F, Scale(e.Graphics, 1F))))
            {
                pen.Alignment = PenAlignment.Inset;
                WithVectorQuality(e.Graphics, () => e.Graphics.DrawPath(pen, path));
            }
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            var item = e.Item as Forms.ToolStripMenuItem;
            if (item == null || (!item.Selected && !item.Pressed))
            {
                return;
            }

            var bounds = new Rectangle(
                0,
                0,
                Math.Max(1, item.Width),
                Math.Max(1, item.Height));
            var fill = item.Pressed ? palette.PressedBackground : palette.HoverBackground;
            using (var path = CreateRoundedPath(bounds, Scale(e.Graphics, 6F)))
            using (var brush = new SolidBrush(fill))
            {
                WithVectorQuality(e.Graphics, () => e.Graphics.FillPath(brush, path));
            }
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled
                ? palette.MutedForeground
                : e.Item.Selected ? palette.SelectedForeground : palette.Foreground;
            DrawItemGlyph(e.Graphics, e.Item);
            PaintMenuText(e);
        }

        private static void PaintMenuText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            using (var brush = new SolidBrush(e.TextColor))
            using (var format = (StringFormat)StringFormat.GenericDefault.Clone())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                format.HotkeyPrefix = HotkeyPrefix.None;

                var previousHint = e.Graphics.TextRenderingHint;
                e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                try
                {
                    e.Graphics.DrawString(
                        e.Text,
                        e.TextFont,
                        brush,
                        GetTextBounds(e.Graphics, e.Item),
                        format);
                }
                finally
                {
                    e.Graphics.TextRenderingHint = previousHint;
                }
            }
        }

        internal static RectangleF GetTextBounds(Graphics graphics, Forms.ToolStripItem item)
        {
            var left = Scale(graphics, 31F);
            var right = Scale(graphics, 9F);
            return new RectangleF(
                left,
                0,
                Math.Max(1F, item.Width - left - right),
                item.Height);
        }

        private void DrawItemGlyph(Graphics graphics, Forms.ToolStripItem item)
        {
            var glyphColor = !item.Enabled
                ? palette.MutedForeground
                : palette.HighContrast && item.Selected ? palette.SelectedForeground : palette.Glyph;
            var glyph = string.Equals(item.Name, "TraySettingsMenuItem", StringComparison.Ordinal)
                ? "\uE713"
                : string.Equals(item.Name, "TrayExitMenuItem", StringComparison.Ordinal)
                    ? "\uE7E8"
                    : null;
            if (glyph == null)
            {
                return;
            }

            using (var path = CreateGlyphPath(glyph, GetGlyphBounds(graphics, item)))
            using (var brush = new SolidBrush(glyphColor))
            {
                WithVectorQuality(graphics, () => graphics.FillPath(brush, path));
            }
        }

        internal static RectangleF GetGlyphBounds(Graphics graphics, Forms.ToolStripItem item)
        {
            var left = Scale(graphics, 8F);
            var size = Scale(graphics, 15F);
            return new RectangleF(left, (item.Height - size) / 2F, size, size);
        }

        internal static GraphicsPath CreateGlyphPath(string glyph, RectangleF targetBounds)
        {
            var path = new GraphicsPath(FillMode.Winding);
            using (var fontFamily = new FontFamily("Segoe MDL2 Assets"))
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                path.AddString(glyph, fontFamily, (int)FontStyle.Regular, 100F, PointF.Empty, format);
            }

            var sourceBounds = path.GetBounds();
            if (sourceBounds.Width <= 0F || sourceBounds.Height <= 0F)
            {
                return path;
            }

            var inset = Math.Min(targetBounds.Width, targetBounds.Height) / 30F;
            var target = RectangleF.Inflate(targetBounds, -inset, -inset);
            var scale = Math.Min(target.Width / sourceBounds.Width, target.Height / sourceBounds.Height);
            var offsetX = target.X + ((target.Width - (sourceBounds.Width * scale)) / 2F) - (sourceBounds.X * scale);
            var offsetY = target.Y + ((target.Height - (sourceBounds.Height * scale)) / 2F) - (sourceBounds.Y * scale);
            using (var transform = new Matrix(scale, 0F, 0F, scale, offsetX, offsetY))
            {
                path.Transform(transform);
            }
            return path;
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

        private static void WithVectorQuality(Graphics graphics, Action render)
        {
            var previousSmoothing = graphics.SmoothingMode;
            var previousPixelOffset = graphics.PixelOffsetMode;
            var previousCompositing = graphics.CompositingQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.GammaCorrected;
            try
            {
                render();
            }
            finally
            {
                graphics.SmoothingMode = previousSmoothing;
                graphics.PixelOffsetMode = previousPixelOffset;
                graphics.CompositingQuality = previousCompositing;
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
        public bool HighContrast { get; private set; }

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
                    HighContrast = true
                };
            }

            return dark
                ? new TrayMenuPalette
                {
                    Background = Color.FromArgb(255, 36, 36, 38),
                    Border = Color.FromArgb(26, 255, 255, 255),
                    Foreground = Color.FromArgb(255, 242, 242, 247),
                    SelectedForeground = Color.FromArgb(255, 242, 242, 247),
                    MutedForeground = Color.FromArgb(255, 142, 142, 147),
                    Glyph = Color.FromArgb(255, 209, 209, 214),
                    HoverBackground = Color.FromArgb(20, 255, 255, 255),
                    PressedBackground = Color.FromArgb(31, 255, 255, 255),
                    HighContrast = false
                }
                : new TrayMenuPalette
                {
                    Background = Color.FromArgb(255, 248, 248, 250),
                    Border = Color.FromArgb(20, 0, 0, 0),
                    Foreground = Color.FromArgb(255, 29, 29, 31),
                    SelectedForeground = Color.FromArgb(255, 29, 29, 31),
                    MutedForeground = Color.FromArgb(255, 134, 134, 139),
                    Glyph = Color.FromArgb(255, 58, 58, 60),
                    HoverBackground = Color.FromArgb(14, 0, 0, 0),
                    PressedBackground = Color.FromArgb(23, 0, 0, 0),
                    HighContrast = false
                };
        }
    }
}
