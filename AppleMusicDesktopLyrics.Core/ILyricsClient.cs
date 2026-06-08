using System.Threading.Tasks;

namespace AppleMusicDesktopLyrics.Core
{
    public interface ILyricsClient
    {
        Task<string> GetSyncedLyricsAsync(TrackIdentity track);
    }
}
