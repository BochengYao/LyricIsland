using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LyricHover.Core
{
    public sealed class QQMusicLyricsClient : ILyricsClient
    {
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly Func<Uri, Task<string>> fetchJsonAsync;

        public QQMusicLyricsClient()
            : this(uri => SharedHttpClient.GetStringAsync(uri))
        {
        }

        public QQMusicLyricsClient(Func<Uri, string> fetchJson)
            : this(uri => Task.FromResult(fetchJson(uri)))
        {
        }

        public QQMusicLyricsClient(Func<Uri, Task<string>> fetchJsonAsync)
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
                var song = ExtractBestSong(searchJson, cleanedTrack);
                if (song == null)
                {
                    return string.Empty;
                }

                var playLyricJson = await fetchJsonAsync(BuildPlayLyricRequestUri(song)).ConfigureAwait(false);
                var package = ExtractPlayLyricsPackage(playLyricJson);
                if (!string.IsNullOrWhiteSpace(package))
                {
                    return package;
                }

                if (string.IsNullOrWhiteSpace(song.Mid))
                {
                    return string.Empty;
                }

                var lyricJson = await fetchJsonAsync(BuildLyricRequestUri(song.Mid)).ConfigureAwait(false);
                return ExtractLegacyLyricsPackage(lyricJson);
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

        private static SongCandidate ExtractBestSong(string json, TrackIdentity track)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("song", out var song) ||
                    !song.TryGetProperty("list", out var list) ||
                    list.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var bestScore = int.MinValue;
                SongCandidate best = null;

                foreach (var item in list.EnumerateArray())
                {
                    var id = ReadInt(item, "id");
                    var mid = ReadString(item, "mid");
                    if (string.IsNullOrWhiteSpace(mid))
                    {
                        mid = ReadString(item, "songmid");
                    }

                    if (id <= 0 && string.IsNullOrWhiteSpace(mid))
                    {
                        continue;
                    }

                    var title = ReadString(item, "title");
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = ReadString(item, "name");
                    }

                    var artist = ReadSingers(item);
                    var album = ReadAlbum(item);
                    var itemSeconds = ReadInt(item, "interval");
                    var duration = itemSeconds > 0 ? TimeSpan.FromSeconds(itemSeconds) : TimeSpan.Zero;
                    var score = LyricsCandidateMatcher.Score(track, title, artist, album, duration);

                    if (best == null || score > bestScore)
                    {
                        best = new SongCandidate(id, mid);
                        bestScore = score;
                    }
                }

                return bestScore >= 45 ? best : null;
            }
        }

        private static string ExtractLegacyLyricsPackage(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("lyric", out var lyric) ||
                    lyric.ValueKind != JsonValueKind.String)
                {
                    return string.Empty;
                }

                var original = WebUtility.HtmlDecode(lyric.GetString() ?? string.Empty);
                if (document.RootElement.TryGetProperty("trans", out var trans) &&
                    trans.ValueKind == JsonValueKind.String)
                {
                    var translated = WebUtility.HtmlDecode(trans.GetString() ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        return LyricsPackageParser.CreatePackage(
                            original,
                            translated,
                            LyricsTranslationLanguage.SimplifiedChinese);
                    }
                }

                return original;
            }
        }

        private static string ExtractPlayLyricsPackage(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("req_0", out var request) ||
                    !request.TryGetProperty("code", out var code) ||
                    code.ValueKind != JsonValueKind.Number ||
                    code.GetInt32() != 0 ||
                    !request.TryGetProperty("data", out var data))
                {
                    return string.Empty;
                }

                var original = DecodeMaybeBase64(ReadString(data, "lyric"));
                if (string.IsNullOrWhiteSpace(original))
                {
                    return string.Empty;
                }

                var translated = DecodeMaybeBase64(ReadString(data, "trans"));
                if (!string.IsNullOrWhiteSpace(translated))
                {
                        return LyricsPackageParser.CreatePackage(
                            original,
                            translated,
                            LyricsTranslationLanguage.SimplifiedChinese);
                }

                return original;
            }
        }

        private static string DecodeMaybeBase64(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException)
            {
                return WebUtility.HtmlDecode(value);
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

        private static string ReadSingers(JsonElement item)
        {
            if (!item.TryGetProperty("singer", out var singers) || singers.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var value = string.Empty;
            foreach (var singer in singers.EnumerateArray())
            {
                var name = ReadString(singer, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    value = string.IsNullOrWhiteSpace(value) ? name : value + " " + name;
                }
            }

            return value;
        }

        private static string ReadAlbum(JsonElement item)
        {
            if (item.TryGetProperty("album", out var album))
            {
                return ReadString(album, "name");
            }

            return string.Empty;
        }

        private static Uri BuildSearchRequestUri(TrackIdentity track)
        {
            var keywords = (track.Title + " " + track.Artist).Trim();
            var url = "https://c.y.qq.com/soso/fcgi-bin/client_search_cp" +
                "?format=json" +
                "&p=1" +
                "&n=8" +
                "&w=" + Uri.EscapeDataString(keywords) +
                "&cr=1" +
                "&new_json=1";
            return new Uri(url);
        }

        private static Uri BuildLyricRequestUri(string songMid)
        {
            var url = "https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg" +
                "?songmid=" + Uri.EscapeDataString(songMid) +
                "&format=json" +
                "&nobase64=1" +
                "&g_tk=5381" +
                "&loginUin=0" +
                "&hostUin=0" +
                "&inCharset=utf8" +
                "&outCharset=utf-8" +
                "&notice=0" +
                "&platform=yqq.json" +
                "&needNewCode=0";
            return new Uri(url);
        }

        private static Uri BuildPlayLyricRequestUri(SongCandidate song)
        {
            var idProperty = song.Id > 0
                ? "\"songId\":" + song.Id
                : "\"songMid\":\"" + EscapeJson(song.Mid) + "\"";
            var data = "{" +
                "\"comm\":{\"ct\":11,\"cv\":12090008,\"uin\":0}," +
                "\"req_0\":{" +
                "\"module\":\"music.musichallSong.PlayLyricInfo\"," +
                "\"method\":\"GetPlayLyricInfo\"," +
                "\"param\":{" +
                idProperty + "," +
                "\"crypt\":0," +
                "\"lrc_t\":0," +
                "\"qrc\":0," +
                "\"qrc_t\":0," +
                "\"trans\":1," +
                "\"trans_t\":0," +
                "\"roma\":0," +
                "\"roma_t\":0," +
                "\"type\":1," +
                "\"ct\":11," +
                "\"cv\":12090008" +
                "}}}";
            return new Uri("https://u.y.qq.com/cgi-bin/musicu.fcg?data=" + Uri.EscapeDataString(data));
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QQMusic 12.9.0.8 LyricHover/0.1");
            client.DefaultRequestHeaders.Referrer = new Uri("https://y.qq.com/");
            return client;
        }

        private sealed class SongCandidate
        {
            public SongCandidate(int id, string mid)
            {
                Id = id;
                Mid = mid ?? string.Empty;
            }

            public int Id { get; }

            public string Mid { get; }
        }
    }
}
