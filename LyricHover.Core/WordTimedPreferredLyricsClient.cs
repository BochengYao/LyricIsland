using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LyricHover.Core
{
    public sealed class WordTimedPreferredLyricsClient : ILyricsClient
    {
        private readonly IReadOnlyList<ILyricsClient> wordTimedClients;
        private readonly ILyricsClient fallbackClient;

        public WordTimedPreferredLyricsClient(IReadOnlyList<ILyricsClient> wordTimedClients, ILyricsClient fallbackClient)
        {
            this.wordTimedClients = wordTimedClients ?? throw new ArgumentNullException(nameof(wordTimedClients));
            this.fallbackClient = fallbackClient ?? throw new ArgumentNullException(nameof(fallbackClient));
        }

        public async Task<string> GetSyncedLyricsAsync(TrackIdentity track)
        {
            var wordTimedLyrics = string.Empty;
            foreach (var client in wordTimedClients)
            {
                try
                {
                    var lyrics = await client.GetSyncedLyricsAsync(track).ConfigureAwait(false);
                    if (LyricsPackageParser.HasWordTiming(lyrics))
                    {
                        wordTimedLyrics = lyrics;
                        break;
                    }
                }
                catch { }
            }

            var fallbackLyrics = await fallbackClient.GetSyncedLyricsAsync(track).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(wordTimedLyrics))
            {
                return fallbackLyrics;
            }

            if (LyricsPackageParser.HasTranslation(wordTimedLyrics) ||
                !LyricsPackageParser.HasTranslation(fallbackLyrics))
            {
                return wordTimedLyrics;
            }

            return LyricsPackageParser.CreatePackage(
                LyricsPackageParser.GetOriginalLyrics(wordTimedLyrics),
                LyricsPackageParser.GetTranslationLyrics(fallbackLyrics),
                LyricsPackageParser.GetTranslationLanguage(fallbackLyrics));
        }
    }
}
