using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppleMusicDesktopLyrics.Core.Media;

namespace AppleMusicDesktopLyrics.App.Media
{
    public interface IMediaSessionService : IDisposable
    {
        event EventHandler SessionsChanged;
        IReadOnlyList<MediaSessionSnapshot> Sessions { get; }
        string WindowsCurrentSessionId { get; }
        Task InitializeAsync();
        Task RefreshAsync();
        Task<bool> TryPlayAsync(string sessionId);
        Task<bool> TryPauseAsync(string sessionId);
        Task<bool> TrySkipPreviousAsync(string sessionId);
        Task<bool> TrySkipNextAsync(string sessionId);
    }
}
