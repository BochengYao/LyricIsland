using System.Threading.Tasks;

namespace LyricsIsland.Core
{
    public interface ILyricsClient
    {
        Task<string> GetSyncedLyricsAsync(TrackIdentity track);
    }
}
