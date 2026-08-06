using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LyricHover.Core
{
    public sealed class CompositeLyricsClient : ILyricsClient
    {
        private readonly IReadOnlyList<ILyricsClient> clients;

        public CompositeLyricsClient(IReadOnlyList<ILyricsClient> clients)
        {
            this.clients = clients ?? throw new ArgumentNullException(nameof(clients));
        }

        public async Task<string> GetSyncedLyricsAsync(TrackIdentity track)
        {
            var directLyrics = await GetBestLyricsAsync(track).ConfigureAwait(false);
            if (LyricsPackageParser.HasTranslation(directLyrics))
            {
                return directLyrics;
            }

            var baseTitle = TrackTitleNormalizer.RemoveRemixQualifier(track?.Title);
            if (track == null ||
                string.IsNullOrWhiteSpace(baseTitle) ||
                baseTitle.Equals(track.Title, StringComparison.OrdinalIgnoreCase))
            {
                return directLyrics;
            }

            var baseTrack = new TrackIdentity(baseTitle, track.Artist, TimeSpan.Zero);
            var referenceLyrics = await GetBestLyricsAsync(baseTrack).ConfigureAwait(false);
            if (!LyricsPackageParser.HasTranslation(referenceLyrics))
            {
                return directLyrics;
            }

            return LyricsTranslationMerger.MergeMatchingLines(directLyrics, referenceLyrics);
        }

        private async Task<string> GetBestLyricsAsync(TrackIdentity track)
        {
            var firstLyrics = string.Empty;

            foreach (var client in clients)
            {
                try
                {
                    var lyrics = await client.GetSyncedLyricsAsync(track).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(lyrics))
                    {
                        if (LyricsPackageParser.HasTranslation(lyrics))
                        {
                            return lyrics;
                        }

                        if (string.IsNullOrWhiteSpace(firstLyrics))
                        {
                            firstLyrics = lyrics;
                        }
                    }
                }
                catch
                {
                    // A single source should not prevent later sources from being tried.
                }
            }

            return firstLyrics;
        }
    }
}
