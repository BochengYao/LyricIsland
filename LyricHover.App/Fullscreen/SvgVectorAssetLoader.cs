using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace LyricHover.App.Fullscreen
{
    internal sealed class SvgVectorAsset
    {
        public SvgVectorAsset(Geometry geometry, Rect viewBox)
        {
            Geometry = geometry;
            ViewBox = viewBox;
        }

        public Geometry Geometry { get; }
        public Rect ViewBox { get; }
    }

    internal static class SvgVectorAssetLoader
    {
        private static readonly Dictionary<string, SvgVectorAsset> Cache =
            new Dictionary<string, SvgVectorAsset>(StringComparer.OrdinalIgnoreCase);

        public static SvgVectorAsset Load(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName) || assetName.Contains("..") ||
                assetName.IndexOfAny(new[] { '/', '\\' }) >= 0) return null;
            lock (Cache)
            {
                if (Cache.TryGetValue(assetName, out var cached)) return cached;
                SvgVectorAsset loaded = null;
                try
                {
                    using (var stream = SongEffectCatalog.OpenEmbeddedResource("Assets.SongEffects." + assetName))
                        loaded = stream == null ? null : Parse(stream);
                }
                catch { }
                Cache[assetName] = loaded;
                return loaded;
            }
        }

        internal static SvgVectorAsset Parse(Stream stream)
        {
            if (stream == null) return null;
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            XDocument document;
            using (var reader = XmlReader.Create(stream, settings)) document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root;
            if (root == null || !root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase)) return null;
            if (root.Descendants().Any(element => IsForbiddenElement(element.Name.LocalName))) return null;
            if (root.DescendantsAndSelf().Any(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName.Equals("transform", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)))) return null;

            var viewBox = ParseViewBox((string)root.Attribute("viewBox"));
            if (viewBox.IsEmpty || viewBox.Width <= 0 || viewBox.Height <= 0) return null;
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var path in root.Descendants().Where(element =>
                element.Name.LocalName.Equals("path", StringComparison.OrdinalIgnoreCase)))
            {
                var data = (string)path.Attribute("d");
                if (string.IsNullOrWhiteSpace(data)) continue;
                var geometry = Geometry.Parse(data);
                if (geometry.CanFreeze) geometry.Freeze();
                group.Children.Add(geometry);
            }
            if (group.Children.Count == 0) return null;
            if (group.CanFreeze) group.Freeze();
            return new SvgVectorAsset(group, viewBox);
        }

        private static bool IsForbiddenElement(string localName)
        {
            return localName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                localName.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                localName.Equals("filter", StringComparison.OrdinalIgnoreCase) ||
                localName.Equals("image", StringComparison.OrdinalIgnoreCase) ||
                localName.Equals("use", StringComparison.OrdinalIgnoreCase) ||
                localName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase);
        }

        private static Rect ParseViewBox(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Rect.Empty;
            var values = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 4) return Rect.Empty;
            var numbers = new double[4];
            for (var index = 0; index < values.Length; index++)
                if (!double.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index]))
                    return Rect.Empty;
            return numbers[2] > 0 && numbers[3] > 0
                ? new Rect(numbers[0], numbers[1], numbers[2], numbers[3])
                : Rect.Empty;
        }
    }
}
