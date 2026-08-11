using System.Threading.Tasks;

namespace LyricHover.Core
{
    public interface ILyricsClient
    {
        Task<string> GetSyncedLyricsAsync(TrackIdentity track);
    }

    public interface ITargetedLyricsClient : ILyricsClient
    {
        Task<string> GetSyncedLyricsAsync(
            TrackIdentity track,
            LyricsTranslationLanguage targetTranslationLanguage);
    }
}
