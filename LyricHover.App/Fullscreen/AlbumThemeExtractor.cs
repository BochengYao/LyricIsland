using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MediaColor = System.Windows.Media.Color;

namespace LyricHover.App.Fullscreen
{
    internal sealed class AlbumThemePalette
    {
        public MediaColor BaseColor { get; set; }
        public IReadOnlyList<MediaColor> AccentColors { get; set; }

        public static AlbumThemePalette Default => new AlbumThemePalette
        {
            BaseColor = MediaColor.FromRgb(13, 15, 22),
            AccentColors = new[]
            {
                MediaColor.FromRgb(38, 43, 67),
                MediaColor.FromRgb(59, 45, 73),
                MediaColor.FromRgb(29, 51, 59),
                MediaColor.FromRgb(70, 48, 49)
            }
        };
    }

    internal sealed class AlbumThemeExtractor
    {
        private const int MaximumCacheEntries = 48;
        private readonly object sync = new object();
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);

        public Task<AlbumThemePalette> ExtractAsync(string trackIdentity, byte[] artwork)
        {
            if (artwork == null || artwork.Length == 0) return Task.FromResult(AlbumThemePalette.Default);
            var key = (trackIdentity ?? string.Empty) + ":" + ComputeHash(artwork);
            lock (sync)
            {
                if (cache.TryGetValue(key, out var hit))
                {
                    hit.LastUsedUtc = DateTime.UtcNow;
                    return Task.FromResult(hit.Palette);
                }
            }

            return Task.Run(() =>
            {
                var palette = Extract(artwork);
                lock (sync)
                {
                    cache[key] = new CacheEntry { Palette = palette, LastUsedUtc = DateTime.UtcNow };
                    if (cache.Count > MaximumCacheEntries)
                    {
                        var oldest = cache.OrderBy(pair => pair.Value.LastUsedUtc).First().Key;
                        cache.Remove(oldest);
                    }
                }
                return palette;
            });
        }

        private static AlbumThemePalette Extract(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var source = new Bitmap(stream))
                using (var sample = new Bitmap(source, new Size(48, 48)))
                {
                    var buckets = new Dictionary<int, ColorBucket>();
                    double saturationTotal = 0;
                    var samples = 0;
                    for (var y = 0; y < sample.Height; y += 2)
                    for (var x = 0; x < sample.Width; x += 2)
                    {
                        var color = sample.GetPixel(x, y);
                        var brightness = color.GetBrightness();
                        var saturation = color.GetSaturation();
                        saturationTotal += saturation;
                        samples++;
                        if (brightness < 0.025 || brightness > 0.97) continue;
                        var key = (color.R / 32 << 10) | (color.G / 32 << 5) | (color.B / 32);
                        if (!buckets.TryGetValue(key, out var bucket)) bucket = new ColorBucket();
                        var weight = 0.4 + saturation * 0.75 + (1 - Math.Abs(brightness - 0.48)) * 0.35;
                        bucket.Add(color, weight);
                        buckets[key] = bucket;
                    }

                    var neutral = samples == 0 || saturationTotal / samples < 0.075;
                    var candidates = buckets.Values.OrderByDescending(bucket => bucket.Weight).ToList();
                    var selected = new List<MediaColor>();
                    foreach (var bucket in candidates)
                    {
                        var color = Tone(bucket.Average, neutral);
                        if (selected.All(existing => Distance(existing, color) > 34)) selected.Add(color);
                        if (selected.Count == 4) break;
                    }
                    if (selected.Count == 0) return AlbumThemePalette.Default;
                    while (selected.Count < 4) selected.Add(Shade(selected[selected.Count % selected.Count], selected.Count));
                    var average = selected.Aggregate(new[] { 0d, 0d, 0d }, (sum, c) =>
                    {
                        sum[0] += c.R; sum[1] += c.G; sum[2] += c.B; return sum;
                    });
                    return new AlbumThemePalette
                    {
                        BaseColor = MediaColor.FromRgb(
                            (byte)Math.Max(7, average[0] / selected.Count * 0.20),
                            (byte)Math.Max(8, average[1] / selected.Count * 0.20),
                            (byte)Math.Max(11, average[2] / selected.Count * 0.22)),
                        AccentColors = selected.AsReadOnly()
                    };
                }
            }
            catch
            {
                return AlbumThemePalette.Default;
            }
        }

        private static MediaColor Tone(Color source, bool neutral)
        {
            var gray = (source.R * 0.299 + source.G * 0.587 + source.B * 0.114);
            var saturationScale = neutral ? 0.08 : 0.72;
            byte Channel(byte value) => (byte)Math.Max(18, Math.Min(205, gray + (value - gray) * saturationScale));
            return MediaColor.FromRgb(Channel(source.R), Channel(source.G), Channel(source.B));
        }

        private static MediaColor Shade(MediaColor source, int index)
        {
            var factor = 0.72 + index * 0.08;
            return MediaColor.FromRgb((byte)(source.R * factor), (byte)(source.G * factor), (byte)(source.B * factor));
        }

        private static double Distance(MediaColor a, MediaColor b)
        {
            var dr = a.R - b.R; var dg = a.G - b.G; var db = a.B - b.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static string ComputeHash(byte[] value)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(value));
        }

        private sealed class CacheEntry { public AlbumThemePalette Palette; public DateTime LastUsedUtc; }
        private struct ColorBucket
        {
            public double R, G, B, Weight;
            public void Add(Color value, double weight) { R += value.R * weight; G += value.G * weight; B += value.B * weight; Weight += weight; }
            public Color Average => Weight <= 0 ? Color.Black : Color.FromArgb((int)(R / Weight), (int)(G / Weight), (int)(B / Weight));
        }
    }
}
