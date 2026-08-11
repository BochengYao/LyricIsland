using System;
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
                SeparatesTargetLanguageCaches();
                FiltersUnavailableSourceTranslations();
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
