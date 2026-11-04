using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LyricHover.App.Fullscreen
{
    internal sealed class ArtworkEnhancer
    {
        private const int MaximumCacheEntries = 10;
        private readonly object sync = new object();
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);

        public byte[] Enhance(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return bytes;
            var key = ComputeHash(bytes);
            lock (sync)
            {
                if (cache.TryGetValue(key, out var hit))
                {
                    hit.LastUsedUtc = DateTime.UtcNow;
                    return hit.Bytes;
                }
            }

            var enhanced = EnhanceCore(bytes);
            lock (sync)
            {
                cache[key] = new CacheEntry { Bytes = enhanced, LastUsedUtc = DateTime.UtcNow };
                if (cache.Count > MaximumCacheEntries)
                    cache.Remove(cache.OrderBy(pair => pair.Value.LastUsedUtc).First().Key);
            }
            return enhanced;
        }

        private static byte[] EnhanceCore(byte[] bytes)
        {
            try
            {
                using (var input = new MemoryStream(bytes, false))
                using (var source = new Bitmap(input))
                {
                    var longest = Math.Max(source.Width, source.Height);
                    if (longest >= 900) return bytes;

                    var scale = Math.Min(4d, 1024d / Math.Max(1, longest));
                    var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    var height = Math.Max(1, (int)Math.Round(source.Height * scale));
                    using (var enlarged = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                    {
                        enlarged.SetResolution(96, 96);
                        using (var graphics = Graphics.FromImage(enlarged))
                        {
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                        }
                        ApplyLightSharpen(enlarged);
                        using (var output = new MemoryStream())
                        {
                            enlarged.Save(output, ImageFormat.Png);
                            return output.ToArray();
                        }
                    }
                }
            }
            catch
            {
                return bytes;
            }
        }

        private static void ApplyLightSharpen(Bitmap bitmap)
        {
            if (bitmap.Width < 3 || bitmap.Height < 3) return;
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var stride = Math.Abs(data.Stride);
                var source = new byte[stride * bitmap.Height];
                var target = new byte[source.Length];
                Marshal.Copy(data.Scan0, source, 0, source.Length);
                Buffer.BlockCopy(source, 0, target, 0, source.Length);

                for (var y = 1; y < bitmap.Height - 1; y++)
                {
                    for (var x = 1; x < bitmap.Width - 1; x++)
                    {
                        var offset = y * stride + x * 4;
                        for (var channel = 0; channel < 3; channel++)
                        {
                            var value = source[offset + channel] * 1.8
                                - source[offset - 4 + channel] * 0.2
                                - source[offset + 4 + channel] * 0.2
                                - source[offset - stride + channel] * 0.2
                                - source[offset + stride + channel] * 0.2;
                            target[offset + channel] = (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
                        }
                    }
                }
                Marshal.Copy(target, 0, data.Scan0, target.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static string ComputeHash(byte[] value)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(value));
        }

        private sealed class CacheEntry
        {
            public byte[] Bytes;
            public DateTime LastUsedUtc;
        }
    }
}
