using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LyricHover.Core;

namespace LyricHover.Core.TranslationContractTests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                PreservesLegacyPackageCompatibility();
                MatchesOnlyTheRequestedTranslationLanguage();
                AlignsSlightlyDelayedSourceTranslations();
                DoesNotReuseSparseSourceTranslations();
                SeparatesTargetLanguageCaches();
                FiltersUnavailableSourceTranslations();
                FindsLocalizedTitleThroughArtistFallback();
                FindsLocalizedTitleThroughQqArtistFallback();
                RejectsAmbiguousLocalizedTitleFallback();
                Console.WriteLine("Core translation contract tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void PreservesLegacyPackageCompatibility()
        {
            var legacy = LyricsPackageParser.CreatePackage("[00:01.00]hello", "[00:01.00]你好");
            Assert(legacy.Contains(LyricsPackageParser.TranslationSeparator), "Legacy separator missing.");
            Assert(LyricsPackageParser.GetTranslationLanguage(legacy) == LyricsTranslationLanguage.SourceDefault,
                "Legacy package language changed.");

            var localized = LyricsPackageParser.CreatePackage(
                "[00:01.00]hello",
                "[00:01.00]hello",
                LyricsTranslationLanguage.English);
            Assert(LyricsPackageParser.GetTranslationLanguage(localized) == LyricsTranslationLanguage.English,
                "Localized package language missing.");
        }

        private static void MatchesOnlyTheRequestedTranslationLanguage()
        {
            var package = LyricsPackageParser.CreatePackage(
                "[00:01.00]hello\n[00:04.00]world",
                "[00:01.00]你好\n[00:04.00]世界",
                LyricsTranslationLanguage.SimplifiedChinese);
            var lyrics = LyricsPackageParser.Parse(package);

            var chinese = LyricsDisplaySelector.Select(
                lyrics,
                TimeSpan.FromSeconds(2),
                TimeSpan.Zero,
                false,
                true,
                LyricsTranslationLanguage.SimplifiedChinese);
            Assert(chinese.Count == 2 && chinese[1].Text == "你好", "Chinese translation was not selected.");

            var english = LyricsDisplaySelector.Select(
                lyrics,
                TimeSpan.FromSeconds(2),
                TimeSpan.Zero,
                false,
                true,
                LyricsTranslationLanguage.English);
            Assert(english.Count == 1 && english[0].Text == "hello", "Mismatched translation was displayed.");
        }

        private static void SeparatesTargetLanguageCaches()
        {
            var root = Path.Combine(Path.GetTempPath(), "LyricHover.TranslationContract." + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LyricsCache(root);
                var track = new TrackIdentity("Song", "Artist", TimeSpan.FromSeconds(180));
                cache.Write(track, LyricsTranslationLanguage.SimplifiedChinese, "zh-hans");
                cache.Write(track, LyricsTranslationLanguage.English, "english");

                Assert(cache.GetPath(track, LyricsTranslationLanguage.SimplifiedChinese) !=
                    cache.GetPath(track, LyricsTranslationLanguage.English), "Target-language cache paths collided.");
                Assert(cache.TryRead(track, LyricsTranslationLanguage.SimplifiedChinese, out var chinese) && chinese == "zh-hans",
                    "Chinese cache was not isolated.");
                Assert(cache.TryRead(track, LyricsTranslationLanguage.English, out var english) && english == "english",
                    "English cache was not isolated.");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void AlignsSlightlyDelayedSourceTranslations()
        {
            var lyrics = LyricsPackageParser.Parse(LyricsPackageParser.CreatePackage(
                "[00:01.00]hello\n[00:04.00]world",
                "[00:02.20]你好\n[00:05.20]世界",
                LyricsTranslationLanguage.SimplifiedChinese));

            var lines = LyricsDisplaySelector.Select(
                lyrics,
                TimeSpan.FromSeconds(1.5),
                TimeSpan.Zero,
                false,
                true);

            Assert(lines.Count == 2 && lines[1].Text == "你好",
                "A small source timestamp delay left the translation row blank.");
        }

        private static void DoesNotReuseSparseSourceTranslations()
        {
            var lyrics = LyricsPackageParser.Parse(LyricsPackageParser.CreatePackage(
                "[00:01.00]first\n[00:02.00]second",
                "[00:01.00]第一句",
                LyricsTranslationLanguage.SimplifiedChinese));

            var lines = LyricsDisplaySelector.Select(
                lyrics,
                TimeSpan.FromSeconds(2.2),
                TimeSpan.Zero,
                false,
                true);

            Assert(lines.Count == 1 && lines[0].Text == "second",
                "A sparse translation was reused for a neighboring lyric.");
        }

        private static void FiltersUnavailableSourceTranslations()
        {
            var sourcePackage = LyricsPackageParser.CreatePackage(
                "[00:01.00]hello",
                "[00:01.00]你好",
                LyricsTranslationLanguage.SimplifiedChinese);
            var client = new CompositeLyricsClient(new ILyricsClient[] { new FakeLyricsClient(sourcePackage) });
            var english = client.GetSyncedLyricsAsync(
                new TrackIdentity("Song", "Artist", TimeSpan.Zero),
                LyricsTranslationLanguage.English).GetAwaiter().GetResult();
            Assert(!LyricsPackageParser.HasTranslation(english), "Unavailable English translation was not removed.");

            var chinese = client.GetSyncedLyricsAsync(
                new TrackIdentity("Song", "Artist", TimeSpan.Zero),
                LyricsTranslationLanguage.SimplifiedChinese).GetAwaiter().GetResult();
            Assert(LyricsPackageParser.HasTranslationForLanguage(chinese, LyricsTranslationLanguage.SimplifiedChinese),
                "Available Chinese translation was removed.");
        }

        private static void FindsLocalizedTitleThroughArtistFallback()
        {
            var requests = new List<Uri>();
            var client = new NetEaseLyricsClient(uri =>
            {
                requests.Add(uri);
                if (uri.AbsolutePath.EndsWith("/api/search/get/web", StringComparison.OrdinalIgnoreCase))
                {
                    var query = Uri.UnescapeDataString(uri.Query);
                    return query.Contains("s=蔡依林")
                        ? "{\"result\":{\"songs\":[{\"id\":321,\"name\":\"Lovefool\",\"artists\":[{\"name\":\"蔡依林\"}],\"duration\":251000}]}}"
                        : "{\"result\":{\"songs\":[]}}";
                }

                return "{\"lrc\":{\"lyric\":\"[00:01.00]Lovefool\"}}";
            });

            var result = client.GetSyncedLyricsAsync(
                    new TrackIdentity("爱情傻瓜", "蔡依林", TimeSpan.FromSeconds(251)))
                .GetAwaiter()
                .GetResult();

            Assert(result.Contains("Lovefool"), "Localized title did not use the artist fallback candidate.");
            Assert(requests.Count == 3, "Localized title lookup did not perform search, fallback search, and lyric fetch.");
            Assert(Uri.UnescapeDataString(requests[1].Query).Contains("s=蔡依林"),
                "Localized title fallback did not search by artist.");
        }

        private static void RejectsAmbiguousLocalizedTitleFallback()
        {
            var requests = new List<Uri>();
            var client = new NetEaseLyricsClient(uri =>
            {
                requests.Add(uri);
                return "{\"result\":{\"songs\":[" +
                    "{\"id\":321,\"name\":\"Lovefool\",\"artists\":[{\"name\":\"蔡依林\"}],\"duration\":251000}," +
                    "{\"id\":322,\"name\":\"Another Song\",\"artists\":[{\"name\":\"蔡依林\"}],\"duration\":251000}]}}";
            });

            var result = client.GetSyncedLyricsAsync(
                    new TrackIdentity("爱情傻瓜", "蔡依林", TimeSpan.FromSeconds(251)))
                .GetAwaiter()
                .GetResult();

            Assert(string.IsNullOrEmpty(result) && requests.Count == 2,
                "Ambiguous artist-and-duration candidates must not fetch unrelated lyrics.");
        }

        private static void FindsLocalizedTitleThroughQqArtistFallback()
        {
            var requests = new List<Uri>();
            var client = new QQMusicLyricsClient(uri =>
            {
                requests.Add(uri);
                if (uri.AbsolutePath.EndsWith("/client_search_cp", StringComparison.OrdinalIgnoreCase))
                {
                    var query = Uri.UnescapeDataString(uri.Query);
                    return query.Contains("w=蔡依林")
                        ? "{\"data\":{\"song\":{\"list\":[{\"id\":123,\"mid\":\"lovefool\",\"title\":\"Lovefool\",\"singer\":[{\"name\":\"蔡依林\"}],\"interval\":251}]}}}"
                        : "{\"data\":{\"song\":{\"list\":[]}}}";
                }

                var lyric = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[00:01.00]Lovefool"));
                return "{\"req_0\":{\"code\":0,\"data\":{\"lyric\":\"" + lyric + "\",\"trans\":\"\"}}}";
            });

            var result = client.GetSyncedLyricsAsync(
                    new TrackIdentity("爱情傻瓜", "蔡依林", TimeSpan.FromSeconds(251)))
                .GetAwaiter()
                .GetResult();

            Assert(result.Contains("Lovefool"), "QQ Music localized title fallback did not fetch lyrics.");
            Assert(requests.Count == 3 && Uri.UnescapeDataString(requests[1].Query).Contains("w=蔡依林"),
                "QQ Music localized title fallback did not use the artist query.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeLyricsClient : ILyricsClient
        {
            private readonly string lyrics;

            public FakeLyricsClient(string lyrics)
            {
                this.lyrics = lyrics;
            }

            public Task<string> GetSyncedLyricsAsync(TrackIdentity track)
            {
                return Task.FromResult(lyrics);
            }
        }
    }
}
