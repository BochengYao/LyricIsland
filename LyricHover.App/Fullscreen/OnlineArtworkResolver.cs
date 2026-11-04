using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LyricHover.Core;
using LyricHover.Core.Media;

namespace LyricHover.App.Fullscreen
{
    /// <summary>
    /// Finds a larger cover through the same catalogue endpoints already used by
    /// the lyrics providers. A remote image is never trusted by metadata alone:
    /// it must also be a higher-resolution perceptual match for the SMTC image.
    /// </summary>
    internal sealed class OnlineArtworkResolver : IDisposable
    {
        private const int MaximumDownloadBytes = 12 * 1024 * 1024;
        private const int MaximumCacheEntries = 32;
        private readonly HttpClient client;
        private readonly object sync = new object();
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private bool disposed;

        public OnlineArtworkResolver()
        {
            client = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 LyricHover/3.3");
        }

        public async Task<byte[]> ResolveAsync(MediaSessionSnapshot session, byte[] smtcArtwork)
        {
            if (disposed || session == null || smtcArtwork == null || smtcArtwork.Length == 0 ||
                string.IsNullOrWhiteSpace(session.Title))
            {
                return null;
            }

            var track = new TrackIdentity(session.Title, session.Artist, session.Duration, session.Album);
            var cacheKey = Normalize(session.Title) + "|" + Normalize(session.Artist) + "|" +
                Normalize(session.Album) + "|" + ArtworkFingerprint.ComputeContentHash(smtcArtwork);
            lock (sync)
            {
                if (cache.TryGetValue(cacheKey, out var hit))
                {
                    hit.LastUsedUtc = DateTime.UtcNow;
                    return hit.Bytes;
                }
            }

            byte[] resolved = null;
            try
            {
                var searches = new[] { SafeSearchAsync(() => SearchQqAsync(track)), SafeSearchAsync(() => SearchNetEaseAsync(track)) };
                var candidateGroups = await Task.WhenAll(searches).ConfigureAwait(false);
                foreach (var candidate in candidateGroups.SelectMany(value => value)
                    .OrderByDescending(value => value.MetadataScore))
                {
                    byte[] bytes;
                    try { bytes = await DownloadAsync(candidate.Uri, candidate.Referrer).ConfigureAwait(false); }
                    catch (HttpRequestException) { continue; }
                    catch (IOException) { continue; }
                    catch (TaskCanceledException) { continue; }
                    catch (ArgumentException) { continue; }
                    catch (ObjectDisposedException) { continue; }
                    if (ArtworkFingerprint.IsSuitableUpgrade(smtcArtwork, bytes))
                    {
                        resolved = bytes;
                        break;
                    }
                }
            }
            catch (HttpRequestException) { }
            catch (IOException) { }
            catch (TaskCanceledException) { }
            catch (JsonException) { }
            catch (ArgumentException) { }
            catch (ObjectDisposedException) { }

            lock (sync)
            {
                cache[cacheKey] = new CacheEntry { Bytes = resolved, LastUsedUtc = DateTime.UtcNow };
                if (cache.Count > MaximumCacheEntries)
                    cache.Remove(cache.OrderBy(pair => pair.Value.LastUsedUtc).First().Key);
            }
            return resolved;
        }

        private static async Task<IReadOnlyList<Candidate>> SafeSearchAsync(
            Func<Task<IReadOnlyList<Candidate>>> search)
        {
            try { return await search().ConfigureAwait(false); }
            catch (HttpRequestException) { return Array.Empty<Candidate>(); }
            catch (IOException) { return Array.Empty<Candidate>(); }
            catch (TaskCanceledException) { return Array.Empty<Candidate>(); }
            catch (JsonException) { return Array.Empty<Candidate>(); }
            catch (ArgumentException) { return Array.Empty<Candidate>(); }
            catch (ObjectDisposedException) { return Array.Empty<Candidate>(); }
        }

        private async Task<IReadOnlyList<Candidate>> SearchQqAsync(TrackIdentity track)
        {
            var uri = new Uri("https://c.y.qq.com/soso/fcgi-bin/client_search_cp?format=json&p=1&n=8&w=" +
                Uri.EscapeDataString((track.Title + " " + track.Artist).Trim()) + "&cr=1&new_json=1");
            var json = await GetStringAsync(uri, "https://y.qq.com/").ConfigureAwait(false);
            var result = new List<Candidate>();
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("song", out var song) ||
                    !song.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (var item in list.EnumerateArray())
                {
                    var title = ReadString(item, "title");
                    if (string.IsNullOrWhiteSpace(title)) title = ReadString(item, "name");
                    var artist = ReadQqArtists(item);
                    var album = item.TryGetProperty("album", out var albumNode) ? ReadString(albumNode, "name") : string.Empty;
                    var albumMid = item.TryGetProperty("album", out albumNode) ? ReadString(albumNode, "mid") : string.Empty;
                    var seconds = ReadInt(item, "interval");
                    var score = LyricsCandidateMatcher.Score(track, title, artist, album,
                        seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero);
                    if (score < 55 || string.IsNullOrWhiteSpace(albumMid)) continue;
                    result.Add(new Candidate(
                        new Uri("https://y.gtimg.cn/music/photo_new/T002R1200x1200M000" +
                            Uri.EscapeDataString(albumMid) + ".jpg?max_age=2592000"), score, "https://y.qq.com/"));
                }
            }
            return result.OrderByDescending(value => value.MetadataScore).Take(3).ToList();
        }

        private async Task<IReadOnlyList<Candidate>> SearchNetEaseAsync(TrackIdentity track)
        {
            var uri = new Uri("https://music.163.com/api/search/get/web?csrf_token=&s=" +
                Uri.EscapeDataString((track.Title + " " + track.Artist).Trim()) + "&type=1&offset=0&limit=8");
            var json = await GetStringAsync(uri, "https://music.163.com/").ConfigureAwait(false);
            var albums = new List<NetEaseAlbumCandidate>();
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("result", out var root) ||
                    !root.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
                    return new List<Candidate>();

                foreach (var song in songs.EnumerateArray())
                {
                    var title = ReadString(song, "name");
                    var artist = ReadNetEaseArtists(song);
                    var album = song.TryGetProperty("album", out var albumNode) ? ReadString(albumNode, "name") : string.Empty;
                    var albumId = song.TryGetProperty("album", out albumNode) ? ReadLong(albumNode, "id") : 0;
                    var duration = ReadInt(song, "duration");
                    var score = LyricsCandidateMatcher.Score(track, title, artist, album,
                        duration > 0 ? TimeSpan.FromMilliseconds(duration) : TimeSpan.Zero);
                    if (score >= 55 && albumId > 0) albums.Add(new NetEaseAlbumCandidate(albumId, score));
                }
            }

            var result = new List<Candidate>();
            foreach (var album in albums.OrderByDescending(value => value.MetadataScore)
                .GroupBy(value => value.AlbumId).Select(group => group.First()).Take(2))
            {
                try
                {
                    var albumJson = await GetStringAsync(
                        new Uri("https://music.163.com/api/v1/album/" + album.AlbumId),
                        "https://music.163.com/").ConfigureAwait(false);
                    using (var albumDocument = JsonDocument.Parse(albumJson))
                    {
                        var picture = albumDocument.RootElement.TryGetProperty("album", out var albumNode)
                            ? ReadString(albumNode, "picUrl") : string.Empty;
                        if (!Uri.TryCreate(picture, UriKind.Absolute, out var pictureUri)) continue;
                        var separator = pictureUri.Query.Length == 0 ? "?" : "&";
                        result.Add(new Candidate(new Uri(pictureUri + separator + "param=1200y1200"),
                            album.MetadataScore, "https://music.163.com/"));
                    }
                }
                catch (HttpRequestException) { }
                catch (IOException) { }
                catch (TaskCanceledException) { }
                catch (JsonException) { }
            }
            return result;
        }

        private async Task<string> GetStringAsync(Uri uri, string referrer)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                request.Headers.Referrer = new Uri(referrer);
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task<byte[]> DownloadAsync(Uri uri, string referrer)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                request.Headers.Referrer = new Uri(referrer);
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.GetValueOrDefault() > MaximumDownloadBytes) return null;
                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
                    using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var target = new MemoryStream())
                    {
                        var buffer = new byte[32 * 1024];
                        while (true)
                        {
                            var read = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (read <= 0) break;
                            if (target.Length + read > MaximumDownloadBytes) return null;
                            target.Write(buffer, 0, read);
                        }
                        return target.ToArray();
                    }
                }
            }
        }

        private static string ReadQqArtists(JsonElement item)
        {
            if (!item.TryGetProperty("singer", out var artists) || artists.ValueKind != JsonValueKind.Array) return string.Empty;
            return string.Join(" ", artists.EnumerateArray().Select(value => ReadString(value, "name"))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string ReadNetEaseArtists(JsonElement item)
        {
            if (!item.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array) return string.Empty;
            return string.Join(" ", artists.EnumerateArray().Select(value => ReadString(value, "name"))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty : string.Empty;
        }

        private static int ReadInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number : 0;
        }

        private static long ReadLong(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? number : 0;
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        public void Dispose()
        {
            disposed = true;
            client.Dispose();
        }

        private sealed class Candidate
        {
            public Candidate(Uri uri, int metadataScore, string referrer)
            { Uri = uri; MetadataScore = metadataScore; Referrer = referrer; }
            public Uri Uri { get; }
            public int MetadataScore { get; }
            public string Referrer { get; }
        }

        private sealed class NetEaseAlbumCandidate
        {
            public NetEaseAlbumCandidate(long albumId, int metadataScore)
            { AlbumId = albumId; MetadataScore = metadataScore; }
            public long AlbumId { get; }
            public int MetadataScore { get; }
        }

        private sealed class CacheEntry
        {
            public byte[] Bytes;
            public DateTime LastUsedUtc;
        }
    }

    internal static class ArtworkFingerprint
    {
        public static bool IsSuitableUpgrade(byte[] sourceBytes, byte[] candidateBytes)
        {
            if (sourceBytes == null || candidateBytes == null || candidateBytes.Length == 0) return false;
            try
            {
                using (var sourceStream = new MemoryStream(sourceBytes, false))
                using (var candidateStream = new MemoryStream(candidateBytes, false))
                using (var source = new Bitmap(sourceStream))
                using (var candidate = new Bitmap(candidateStream))
                {
                    var sourceLongest = Math.Max(source.Width, source.Height);
                    var candidateLongest = Math.Max(candidate.Width, candidate.Height);
                    if (candidateLongest < 600 || candidateLongest < sourceLongest * 1.35) return false;
                    var sourceRatio = source.Width / (double)Math.Max(1, source.Height);
                    var candidateRatio = candidate.Width / (double)Math.Max(1, candidate.Height);
                    if (Math.Abs(sourceRatio - candidateRatio) > 0.08) return false;

                    var hashDistance = HammingDistance(DifferenceHash(source), DifferenceHash(candidate));
                    var histogram = HistogramIntersection(source, candidate);
                    return hashDistance <= 18 && histogram >= 0.54;
                }
            }
            catch (ArgumentException) { return false; }
            catch (OutOfMemoryException) { return false; }
        }

        public static string ComputeContentHash(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(bytes ?? Array.Empty<byte>()));
        }

        private static ulong DifferenceHash(Bitmap source)
        {
            using (var sample = new Bitmap(9, 8))
            using (var graphics = Graphics.FromImage(sample))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, 9, 8);
                ulong hash = 0;
                var bit = 0;
                for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++, bit++)
                    if (Luma(sample.GetPixel(x, y)) > Luma(sample.GetPixel(x + 1, y))) hash |= 1UL << bit;
                return hash;
            }
        }

        private static double HistogramIntersection(Bitmap source, Bitmap candidate)
        {
            var first = Histogram(source);
            var second = Histogram(candidate);
            var intersection = 0d;
            for (var i = 0; i < first.Length; i++) intersection += Math.Min(first[i], second[i]);
            return intersection;
        }

        private static double[] Histogram(Bitmap source)
        {
            using (var sample = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(sample))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.DrawImage(source, 0, 0, 32, 32);
                var values = new double[64];
                for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var color = sample.GetPixel(x, y);
                    values[(color.R >> 6) * 16 + (color.G >> 6) * 4 + (color.B >> 6)]++;
                }
                for (var i = 0; i < values.Length; i++) values[i] /= 1024d;
                return values;
            }
        }

        private static int HammingDistance(ulong first, ulong second)
        {
            var value = first ^ second;
            var count = 0;
            while (value != 0) { count++; value &= value - 1; }
            return count;
        }

        private static double Luma(Color value) => value.R * 0.299 + value.G * 0.587 + value.B * 0.114;
    }
}
