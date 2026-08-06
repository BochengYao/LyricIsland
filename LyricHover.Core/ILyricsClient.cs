using System.Threading.Tasks;

namespace LyricHover.Core
{
    public interface ILyricsClient
    {
        Task<string> GetSyncedLyricsAsync(TrackIdentity track);
    }
}
