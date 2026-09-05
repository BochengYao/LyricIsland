using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LyricsIsland.Core
{
    public sealed class LrcLibClient : ILyricsClient
    {
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly Func<Uri, Task<string>> fetchJsonAsync;

        public LrcLibClient()
            : this(uri => SharedHttpClient.GetStringAsync(uri))
        {
        }

        public LrcLibClient(Func<Uri, string> fetchJson)
            : this(uri => Task.FromResult(fetchJson(uri)))
        {
        }

        public LrcLibClient(Func<Uri, Task<string>> fetchJsonAsync)
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
            if (!string.IsNullOrWhiteSpace(cleanedTrack.Album))
            {
                var albumMatch = await TryGetLyricsAsync(BuildSearchRequestUri(cleanedTrack, true), cleanedTrack).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(albumMatch))
                {
                    return albumMatch;
                }
            }

            return await TryGetLyricsAsync(BuildSearchRequestUri(cleanedTrack, false), cleanedTrack).ConfigureAwait(false);
        }

        private async Task<string> TryGetLyricsAsync(Uri uri, TrackIdentity track)
        {
            try
            {
                var json = await fetchJsonAsync(uri).ConfigureAwait(false);
                return ExtractFromSearchResults(json, track);
            }
            catch (HttpRequestException ex) when (IsNotFoundOrBadRequest(ex))
            {
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                return string.Empty;
            }
        }

        private static string ExtractFromSearchResults(string json, TrackIdentity track)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return string.Empty;
                }

                var bestLyrics = string.Empty;
                var bestScore = int.MinValue;

                foreach (var result in document.RootElement.EnumerateArray())
                {
                    var lyrics = ExtractSyncedLyrics(result);
                    if (!string.IsNullOrWhiteSpace(lyrics))
                    {
                        var title = ReadString(result, "trackName");
                        var artist = ReadString(result, "artistName");
                        var album = ReadString(result, "albumName");
                        var duration = TimeSpan.FromSeconds(ReadInt(result, "duration"));
                        var score = LyricsCandidateMatcher.Score(track, title, artist, album, duration);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestLyrics = lyrics;
                        }
                    }
                }

                if (bestScore >= 45)
                {
                    return bestLyrics;
                }
            }

            return string.Empty;
        }

        private static string ExtractSyncedLyrics(JsonElement element)
        {
            if (element.TryGetProperty("syncedLyrics", out var syncedLyrics) &&
                syncedLyrics.ValueKind == JsonValueKind.String)
            {
                return syncedLyrics.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static bool IsNotFoundOrBadRequest(HttpRequestException ex)
        {
            return ex.Message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("400", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
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

        private static Uri BuildSearchRequestUri(TrackIdentity track, bool includeAlbum)
        {
            var url = "https://lrclib.net/api/search" +
                "?track_name=" + Uri.EscapeDataString(track.Title) +
                "&artist_name=" + Uri.EscapeDataString(track.Artist);
            if (includeAlbum && !string.IsNullOrWhiteSpace(track.Album))
            {
                url += "&album_name=" + Uri.EscapeDataString(track.Album);
            }

            return new Uri(url);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(6);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LyricsIsland/0.1");
            return client;
        }
    }
}
