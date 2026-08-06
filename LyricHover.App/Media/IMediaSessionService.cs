using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LyricHover.Core.Media;

namespace LyricHover.App.Media
{
    public interface IMediaSessionService : IDisposable
    {
        event EventHandler SessionsChanged;
        IReadOnlyList<MediaSessionSnapshot> Sessions { get; }
        string WindowsCurrentSessionId { get; }
        Task InitializeAsync();
        Task RefreshAsync();
        MediaPlaybackStatus? GetPlaybackStatus(string sessionId);
        Task<bool> TryPlayAsync(string sessionId);
        Task<bool> TryPauseAsync(string sessionId);
        Task<bool> TrySkipPreviousAsync(string sessionId);
        Task<bool> TrySkipNextAsync(string sessionId);
    }
}
