using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LyricsIsland.Core
{
    public sealed class NetEaseLyricsClient : ILyricsClient
    {
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly Func<Uri, Task<string>> fetchJsonAsync;

        public NetEaseLyricsClient()
            : this(uri => SharedHttpClient.GetStringAsync(uri))
        {
        }

        public NetEaseLyricsClient(Func<Uri, string> fetchJson)
            : this(uri => Task.FromResult(fetchJson(uri)))
        {
        }

        public NetEaseLyricsClient(Func<Uri, Task<string>> fetchJsonAsync)
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
                var songId = ExtractBestSongId(searchJson, cleanedTrack);
                if (songId <= 0)
                {
                    return string.Empty;
                }

                var lyricJson = await fetchJsonAsync(BuildLyricRequestUri(songId)).ConfigureAwait(false);
                return ExtractLyricsPackage(lyricJson);
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
        }

        private static int ExtractBestSongId(string json, TrackIdentity track)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("result", out var result) ||
                    !result.TryGetProperty("songs", out var songs) ||
                    songs.ValueKind != JsonValueKind.Array)
                {
                    return 0;
                }

                var bestId = 0;
                var bestScore = int.MinValue;

                foreach (var song in songs.EnumerateArray())
                {
                    if (song.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                    {
                        var title = ReadString(song, "name");
                        var artist = ReadArtists(song);
                        var album = ReadAlbum(song);
                        var duration = TimeSpan.FromMilliseconds(ReadInt(song, "duration"));
                        var score = LyricsCandidateMatcher.Score(track, title, artist, album, duration);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestId = id.GetInt32();
                        }
                    }
                }

                if (bestId > 0 && bestScore >= 45)
                {
                    return bestId;
                }
            }

            return 0;
        }

        private static string ExtractLyricsPackage(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.TryGetProperty("lrc", out var lrc) &&
                    lrc.TryGetProperty("lyric", out var lyric) &&
                    lyric.ValueKind == JsonValueKind.String)
                {
                    var original = lyric.GetString() ?? string.Empty;
                    if (document.RootElement.TryGetProperty("tlyric", out var tlyric) &&
                        tlyric.TryGetProperty("lyric", out var translation) &&
                        translation.ValueKind == JsonValueKind.String)
                    {
                        var translated = translation.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            return original + Environment.NewLine + LyricsPackageParser.TranslationSeparator + Environment.NewLine + translated;
                        }
                    }

                    return original;
                }
            }

            return string.Empty;
        }

        private static string ReadArtists(JsonElement song)
        {
            if (!song.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var value = string.Empty;
            foreach (var artist in artists.EnumerateArray())
            {
                var name = ReadString(artist, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    value = string.IsNullOrWhiteSpace(value) ? name : value + " " + name;
                }
            }

            return value;
        }

        private static string ReadAlbum(JsonElement song)
        {
            if (song.TryGetProperty("album", out var album))
            {
                return ReadString(album, "name");
            }

            return string.Empty;
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

        private static Uri BuildSearchRequestUri(TrackIdentity track)
        {
            var keywords = (track.Title + " " + track.Artist).Trim();
            var url = "https://music.163.com/api/search/get/web" +
                "?csrf_token=" +
                "&s=" + Uri.EscapeDataString(keywords) +
                "&type=1" +
                "&offset=0" +
                "&limit=5";
            return new Uri(url);
        }

        private static Uri BuildLyricRequestUri(int songId)
        {
            return new Uri("https://music.163.com/api/song/lyric?id=" + songId + "&lv=1&kv=1&tv=-1");
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 LyricsIsland/0.1");
            client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
            return client;
        }
    }
}
