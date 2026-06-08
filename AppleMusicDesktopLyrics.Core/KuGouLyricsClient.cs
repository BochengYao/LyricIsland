using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AppleMusicDesktopLyrics.Core
{
    public sealed class KuGouLyricsClient : ILyricsClient
    {
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly Func<Uri, Task<string>> fetchJsonAsync;

        public KuGouLyricsClient()
            : this(uri => SharedHttpClient.GetStringAsync(uri))
        {
        }

        public KuGouLyricsClient(Func<Uri, string> fetchJson)
            : this(uri => Task.FromResult(fetchJson(uri)))
        {
        }

        public KuGouLyricsClient(Func<Uri, Task<string>> fetchJsonAsync)
        {
            this.fetchJsonAsync = fetchJsonAsync ?? throw new ArgumentNullException(nameof(fetchJsonAsync));
        }

        public async Task<string> GetSyncedLyricsAsync(TrackIdentity track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            var cleanedTrack = TrackIdentityCleaner.Clean(track);
            try
            {
                var searchJson = await fetchJsonAsync(BuildSearchRequestUri(cleanedTrack)).ConfigureAwait(false);
                var candidate = ExtractBestCandidate(searchJson, cleanedTrack);
                if (candidate == null)
                {
                    return string.Empty;
                }

                var downloadJson = await fetchJsonAsync(BuildDownloadRequestUri(candidate)).ConfigureAwait(false);
                return ExtractLyric(downloadJson);
            }
            catch (HttpRequestException)
            {
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                return string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

        private static LyricCandidate ExtractBestCandidate(string json, TrackIdentity track)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
                    candidates.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                LyricCandidate best = null;
                var bestDistance = int.MaxValue;
                var bestScore = int.MinValue;
                var expectedDuration = track.Duration > TimeSpan.Zero ? (int)track.Duration.TotalMilliseconds : 0;
                var hasMetadataScores = false;

                foreach (var candidate in candidates.EnumerateArray())
                {
                    var id = ReadString(candidate, "id");
                    var accessKey = ReadString(candidate, "accesskey");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(accessKey))
                    {
                        continue;
                    }

                    var candidateDuration = ReadInt(candidate, "duration");
                    var duration = candidateDuration > 0 ? TimeSpan.FromMilliseconds(candidateDuration) : TimeSpan.Zero;
                    var title = ReadString(candidate, "song");
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = ReadString(candidate, "name");
                    }

                    var artist = ReadString(candidate, "singer");
                    var album = ReadString(candidate, "album");
                    var hasMetadata = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(artist);
                    var score = hasMetadata ? LyricsCandidateMatcher.Score(track, title, artist, album, duration) : int.MinValue;
                    if (hasMetadata)
                    {
                        hasMetadataScores = true;
                    }

                    var distance = expectedDuration > 0 && candidateDuration > 0
                        ? Math.Abs(candidateDuration - expectedDuration)
                        : 0;

                    if (hasMetadata && score > bestScore)
                    {
                        best = new LyricCandidate(id, accessKey);
                        bestScore = score;
                        bestDistance = distance;
                    }
                    else if (!hasMetadataScores && (best == null || distance < bestDistance))
                    {
                        best = new LyricCandidate(id, accessKey);
                        bestDistance = distance;
                    }
                }

                if (hasMetadataScores && bestScore < 45)
                {
                    return null;
                }

                return best;
            }
        }

        private static string ExtractLyric(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("content", out var content) ||
                    content.ValueKind != JsonValueKind.String)
                {
                    return string.Empty;
                }

                var base64 = content.GetString();
                if (string.IsNullOrWhiteSpace(base64))
                {
                    return string.Empty;
                }

                var bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString();
                }

                if (property.ValueKind == JsonValueKind.Number)
                {
                    return property.GetRawText();
                }
            }

            return string.Empty;
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var value))
            {
                return value;
            }

            return 0;
        }

        private static Uri BuildSearchRequestUri(TrackIdentity track)
        {
            var keywords = (track.Title + " " + track.Artist).Trim();
            var url = "https://lyrics.kugou.com/search" +
                "?ver=1" +
                "&man=yes" +
                "&client=pc" +
                "&keyword=" + Uri.EscapeDataString(keywords) +
                "&duration=" + Math.Max(0, (int)track.Duration.TotalMilliseconds);
            return new Uri(url);
        }

        private static Uri BuildDownloadRequestUri(LyricCandidate candidate)
        {
            var url = "https://lyrics.kugou.com/download" +
                "?ver=1" +
                "&client=pc" +
                "&fmt=lrc" +
                "&charset=utf8" +
                "&id=" + Uri.EscapeDataString(candidate.Id) +
                "&accesskey=" + Uri.EscapeDataString(candidate.AccessKey);
            return new Uri(url);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 AppleMusicDesktopLyrics/0.1");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.kugou.com/");
            return client;
        }

        private sealed class LyricCandidate
        {
            public LyricCandidate(string id, string accessKey)
            {
                Id = id;
                AccessKey = accessKey;
            }

            public string Id { get; }

            public string AccessKey { get; }
        }
    }
}
