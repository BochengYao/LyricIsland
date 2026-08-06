using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using LyricHover.Core;

namespace LyricHover.App
{
    internal sealed class SupporterBadge3DScene
    {
        public SupporterBadge3DScene(
            Model3DGroup model,
            AxisAngleRotation3D yawRotation,
            AxisAngleRotation3D pitchRotation,
            PointLight glintLight,
            SupporterBadgeRuntimeDiagnostics diagnostics,
            SupporterBadgePlaqueInfo plaque)
        {
            Model = model;
            YawRotation = yawRotation;
            PitchRotation = pitchRotation;
            GlintLight = glintLight;
            Diagnostics = diagnostics;
            Plaque = plaque;
        }

        public Model3DGroup Model { get; }

        public AxisAngleRotation3D YawRotation { get; }

        public AxisAngleRotation3D PitchRotation { get; }

        public PointLight GlintLight { get; }

        public SupporterBadgeRuntimeDiagnostics Diagnostics { get; }

        public SupporterBadgePlaqueInfo Plaque { get; }
    }

    internal static class SupporterBadge3DFactory
    {
        // This is intentionally the only formal runtime asset.  The linked Content item in
        // the app project copies the frozen, validator-approved GLB to this output location.
        internal const string FinalSupporterBadgeAssetName = "supporter-badge-final.glb";
        internal const string FinalSupporterBadgeSha256 = "4ac858e8d8843ec4512d410082e4b90e2f2c5c48e89c411e9c410a213e5cbd87";
        // glTF stores this approved badge in metres (about 0.040 scene units across); the
        // existing preview camera was authored in centimetre-like WPF scene units.  This is one
        // uniform metres-to-preview conversion,
        // not an axis correction or a non-uniform model edit.
        private const double WpfUnitsPerGlbUnit = 100.0;

        public static SupporterBadge3DScene Create(
            SupporterBadgeIdentity identity,
            Visual dpiReference = null)
        {
            var model = new Model3DGroup();
            AddStudioLights(model);

            var glint = new PointLight(Colors.Black, new Point3D(-3.0, 1.4, 3.6))
            {
                Range = 7.5,
                ConstantAttenuation = 0.30,
                LinearAttenuation = 0.18
            };
            model.Children.Add(glint);

            var yawRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), -20);
            var pitchRotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), -6);
            var transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(pitchRotation));
            transform.Children.Add(new RotateTransform3D(yawRotation));

            var assetPath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Models",
                FinalSupporterBadgeAssetName);
            var loaded = SupporterBadgeGlbLoader.Load(
                assetPath,
                plaque => CreateIdentityPlaqueMaterial(identity, plaque, dpiReference));
            if (!string.Equals(loaded.Diagnostics.BadgeAssetFormat, "glb", StringComparison.Ordinal) ||
                loaded.Diagnostics.BadgeLoadedFromLegacyObj ||
                !string.Equals(loaded.Diagnostics.BadgeAssetSha256, FinalSupporterBadgeSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The supporter badge must be loaded from the frozen GLB asset.");
            }

            var badge = new Model3DGroup { Transform = transform };
            var millimetreBadge = new Model3DGroup
            {
                Transform = new ScaleTransform3D(WpfUnitsPerGlbUnit, WpfUnitsPerGlbUnit, WpfUnitsPerGlbUnit)
            };
            millimetreBadge.Children.Add(loaded.Model);
            badge.Children.Add(millimetreBadge);
            model.Children.Add(badge);

            return new SupporterBadge3DScene(model, yawRotation, pitchRotation, glint, loaded.Diagnostics, loaded.Plaque);
        }

        internal static Material CreateWpfMaterial(
            string materialName,
            Color parsedBaseColor,
            double roughness,
            double metallic)
        {
            // WPF has no glTF PBR shader.  Keep the final GLB material assignments and map
            // each approved semantic material to a restrained diffuse/specular approximation.
            var diffuseColor = parsedBaseColor;
            if (string.Equals(materialName, "Navy_Enamel_PBR", StringComparison.Ordinal)) diffuseColor = Color.FromRgb(5, 20, 48);
            if (string.Equals(materialName, "Navy_Plaque_PBR", StringComparison.Ordinal)) diffuseColor = Color.FromRgb(2, 9, 23);
            if (string.Equals(materialName, "Back_Brushed_Gold_PBR", StringComparison.Ordinal)) diffuseColor = Color.FromRgb(184, 141, 79);
            if (string.Equals(materialName, "Back_Engraving_PBR", StringComparison.Ordinal)) diffuseColor = Color.FromRgb(174, 179, 184);
            var specularColor = metallic > 0.5
                ? Color.FromRgb((byte)Math.Min(255, diffuseColor.R + 54), (byte)Math.Min(255, diffuseColor.G + 46), (byte)Math.Min(255, diffuseColor.B + 34))
                : Color.FromRgb(88, 96, 108);
            var specularPower = Math.Max(12, 96 * (1.0 - Math.Max(0, Math.Min(1, roughness))));
            var diffuseBrush = new SolidColorBrush(diffuseColor);
            diffuseBrush.Freeze();
            var specularBrush = new SolidColorBrush(specularColor);
            specularBrush.Freeze();

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(diffuseBrush));
            material.Children.Add(new SpecularMaterial(specularBrush, specularPower));
            material.Freeze();
            return material;
        }

        private static Material CreateIdentityPlaqueMaterial(
            SupporterBadgeIdentity identity,
            SupporterBadgePlaqueInfo plaque,
            Visual dpiReference)
        {
            var material = new MaterialGroup();
            // The plate is an existing GLB surface.  An unlit image material prevents the
            // former lighting bands caused by mixing the dynamically painted navy ground with
            // the rounded plate's varying diffuse/specular normals.
            material.Children.Add(new EmissiveMaterial(CreateIdentityImageBrush(identity, plaque, dpiReference)));
            material.Freeze();
            return material;
        }

        private static ImageBrush CreateIdentityImageBrush(
            SupporterBadgeIdentity identity,
            SupporterBadgePlaqueInfo plaque,
            Visual dpiReference)
        {
            const int width = 1024;
            var height = (int)Math.Round(width / plaque.AspectRatio);
            var displayName = identity?.DisplayName ?? "LYRIC HOVER";
            var date = (identity?.AcquiredDate ?? DateTimeOffset.UtcNow)
                .ToLocalTime()
                .ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
            var visual = new DrawingVisual();
            // The text is rasterised into this DrawingVisual/RenderTargetBitmap, so its own
            // DPI is the only valid PixelsPerDip value.  Reusing the containing window's 150%
            // monitor DPI here was a second, mismatched raster-DPI conversion.
            var pixelsPerDip = VisualTreeHelper.GetDpi(visual).PixelsPerDip;
            using (var context = visual.RenderOpen())
            {
                // This is a replacement for the existing plaque's front material, not a
                // second decal mesh.  Its navy ground exactly fills the real plaque UVs.
                context.DrawRectangle(new SolidColorBrush(Color.FromRgb(2, 9, 23)), null, new Rect(0, 0, width, height));
                var silver = new SolidColorBrush(Color.FromRgb(142, 149, 156));
                silver.Freeze();
                // Move the two-line group together by 20 texture pixels.  The inter-line
                // distance, font metrics and horizontal centring are intentionally unchanged.
                const double groupOffsetY = 20.0;
                DrawCenteredText(context, displayName, 0.205 * height, 0.15 * height + groupOffsetY, width * 0.84, FontWeights.SemiBold, silver, width, pixelsPerDip);
                DrawCenteredText(context, date, 0.115 * height, 0.62 * height + groupOffsetY, width * 0.84, FontWeights.Normal, silver, width, pixelsPerDip);
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            WriteTextureDiagnosticIfRequested(bitmap, displayName, date, pixelsPerDip, width, height, plaque.AspectRatio);
            var brush = new ImageBrush(bitmap)
            {
                // The source and the explicit plaque UVs both cover full matching rectangles.
                // Fill is therefore a 1:1 rectangular mapping (0.0144% raster rounding only),
                // not a text scale transform.
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                Viewbox = new Rect(0, 0, width, height),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 1, 1),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Transform = Transform.Identity,
                RelativeTransform = Transform.Identity
            };
            brush.Freeze();
            return brush;
        }

        private static void DrawCenteredText(
            DrawingContext context,
            string value,
            double requestedFontSize,
            double top,
            double maximumWidth,
            FontWeight weight,
            Brush foreground,
            double textureWidth,
            double pixelsPerDip)
        {
            var fontSize = requestedFontSize;
            var text = CreateText(value, fontSize, weight, foreground, pixelsPerDip);
            while (text.WidthIncludingTrailingWhitespace > maximumWidth && fontSize > 28)
            {
                fontSize -= 2;
                text = CreateText(value, fontSize, weight, foreground, pixelsPerDip);
            }

            text.MaxTextWidth = maximumWidth;
            text.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(text, new Point((textureWidth - text.Width) / 2, top));
        }

        private static FormattedText CreateText(
            string value,
            double fontSize,
            FontWeight weight,
            Brush foreground,
            double pixelsPerDip)
        {
            var text = new FormattedText(
                string.IsNullOrWhiteSpace(value) ? "LYRIC HOVER" : value,
                CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Microsoft YaHei UI"),
                    FontStyles.Normal,
                weight,
                FontStretches.Normal),
                fontSize,
                foreground,
                pixelsPerDip)
            {
                TextAlignment = TextAlignment.Left
            };
            // Tighten only the line box to the typeface's own vector-outline bounds.  This
            // removes inherited leading from the source-height measurement without applying
            // any X/Y transform to a glyph.
            var glyphBounds = text.BuildGeometry(new Point()).Bounds;
            if (glyphBounds.Width > 0 && glyphBounds.Height > 0)
            {
                text.LineHeight = text.WidthIncludingTrailingWhitespace * glyphBounds.Height / glyphBounds.Width;
            }
            return text;
        }

        private static void WriteTextureDiagnosticIfRequested(
            RenderTargetBitmap bitmap,
            string displayName,
            string date,
            double pixelsPerDip,
            int width,
            int height,
            double plaqueAspect)
        {
            var target = Environment.GetEnvironmentVariable("LYRICHOVER_BADGE_TEXTURE_DEBUG_PATH");
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(target))
            {
                encoder.Save(stream);
            }

            var silver = new SolidColorBrush(Color.FromRgb(142, 149, 156));
            silver.Freeze();
            var nameText = CreateText(displayName, 0.205 * height, FontWeights.SemiBold, silver, pixelsPerDip);
            var dateText = CreateText(date, 0.115 * height, FontWeights.Normal, silver, pixelsPerDip);
            var namePixels = MeasureSilverPixels(bitmap, 0, (int)(height * 0.55));
            var datePixels = MeasureSilverPixels(bitmap, (int)(height * 0.55), height);
            File.WriteAllText(
                Path.ChangeExtension(target, ".txt"),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "name.source={0:F6};name.bitmap={1:F6};date.source={2:F6};date.bitmap={3:F6};plaque.aspect={4:F8};texture.aspect={5:F8};dpi={6:F4}",
                    TextAspect(nameText), TextAspect(namePixels), TextAspect(dateText), TextAspect(datePixels), plaqueAspect, width / (double)height, pixelsPerDip));
        }

        private static TextBounds MeasureSilverPixels(RenderTargetBitmap bitmap, int top, int bottom)
        {
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            var minX = bitmap.PixelWidth;
            var minY = bitmap.PixelHeight;
            var maxX = -1;
            var maxY = -1;
            for (var y = top; y < bottom; y++)
            {
                for (var x = 0; x < bitmap.PixelWidth; x++)
                {
                    var offset = (y * bitmap.PixelWidth + x) * 4;
                    // Pbgra32 is B,G,R,A.  The navy base never reaches this muted-silver band.
                    if (pixels[offset + 2] < 65 || pixels[offset + 1] < 65 || pixels[offset] < 65)
                    {
                        continue;
                    }
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
            }
            return maxX < minX ? new TextBounds(0, 0) : new TextBounds(maxX - minX + 1, maxY - minY + 1);
        }

        private static double TextAspect(FormattedText text) => text.WidthIncludingTrailingWhitespace / Math.Max(1.0, text.Height);
        private static double TextAspect(TextBounds bounds) => bounds.Width / Math.Max(1.0, bounds.Height);
        private readonly struct TextBounds { public TextBounds(int width, int height) { Width = width; Height = height; } public int Width { get; } public int Height { get; } }

        private static void AddStudioLights(Model3DGroup model)
        {
            model.Children.Add(new AmbientLight(Color.FromRgb(19, 18, 17)));
            model.Children.Add(new DirectionalLight(
                Color.FromRgb(194, 166, 111),
                new Vector3D(-0.58, -0.30, -1.0)));
            model.Children.Add(new DirectionalLight(
                Color.FromRgb(54, 69, 91),
                new Vector3D(0.76, 0.18, -0.76)));
            model.Children.Add(new DirectionalLight(
                Color.FromRgb(91, 64, 28),
                new Vector3D(0.12, 0.94, 0.50)));
        }
    }
}
