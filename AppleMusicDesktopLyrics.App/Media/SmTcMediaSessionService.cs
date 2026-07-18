using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using AppleMusicDesktopLyrics.Core.Media;
using Windows.Media.Control;

namespace AppleMusicDesktopLyrics.App.Media
{
    public sealed class SmTcMediaSessionService : IMediaSessionService
    {
        private GlobalSystemMediaTransportControlsSessionManager manager;
        private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> sessions =
            new Dictionary<string, GlobalSystemMediaTransportControlsSession>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler SessionsChanged;
        public IReadOnlyList<MediaSessionSnapshot> Sessions { get; private set; } =
            new List<MediaSessionSnapshot>().AsReadOnly();
        public string WindowsCurrentSessionId { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            manager.SessionsChanged += Manager_Changed;
            manager.CurrentSessionChanged += Manager_Changed;
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (manager == null) return;

            var current = manager.GetCurrentSession();
            WindowsCurrentSessionId = current?.SourceAppUserModelId ?? string.Empty;
            DetachSessions();
            sessions.Clear();
            var snapshots = new List<MediaSessionSnapshot>();
            foreach (var session in manager.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id)) continue;
                AttachSession(session);
                sessions[id] = session;
                snapshots.Add(await ReadSnapshotAsync(session));
            }

            Sessions = snapshots.AsReadOnly();
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task<bool> TryPlayAsync(string id) => InvokeAsync(id, session => session.TryPlayAsync().AsTask());
        public Task<bool> TryPauseAsync(string id) => InvokeAsync(id, session => session.TryPauseAsync().AsTask());
        public Task<bool> TrySkipPreviousAsync(string id) => InvokeAsync(id, session => session.TrySkipPreviousAsync().AsTask());
        public Task<bool> TrySkipNextAsync(string id) => InvokeAsync(id, session => session.TrySkipNextAsync().AsTask());

        private async Task<bool> InvokeAsync(
            string id,
            Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
        {
            GlobalSystemMediaTransportControlsSession session;
            return sessions.TryGetValue(id ?? string.Empty, out session) && await action(session);
        }

        private async Task<MediaSessionSnapshot> ReadSnapshotAsync(
            GlobalSystemMediaTransportControlsSession session)
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var controls = playback.Controls;
            var profile = PlayerProfileCatalog.Resolve(session.SourceAppUserModelId);

            return new MediaSessionSnapshot
            {
                SessionId = session.SourceAppUserModelId ?? string.Empty,
                SourceAppUserModelId = session.SourceAppUserModelId ?? string.Empty,
                PlayerDisplayName = profile.DisplayName,
                Title = properties.Title ?? string.Empty,
                Artist = properties.Artist ?? string.Empty,
                Album = properties.AlbumTitle ?? string.Empty,
                ArtworkBytes = await ReadArtworkAsync(properties.Thumbnail),
                Position = timeline.Position,
                Duration = timeline.EndTime,
                HasReliableTimeline = timeline.EndTime > TimeSpan.Zero && timeline.Position >= TimeSpan.Zero,
                PlaybackStatus = MapStatus(playback.PlaybackStatus),
                Controls = new MediaControlCapabilities
                {
                    CanPlay = controls.IsPlayEnabled,
                    CanPause = controls.IsPauseEnabled,
                    CanSkipPrevious = controls.IsPreviousEnabled,
                    CanSkipNext = controls.IsNextEnabled
                },
                LastActivityUtc = DateTimeOffset.UtcNow
            };
        }

        private static async Task<byte[]> ReadArtworkAsync(Windows.Storage.Streams.IRandomAccessStreamReference reference)
        {
            if (reference == null) return null;
            using (var stream = await reference.OpenReadAsync())
            using (var input = stream.AsStreamForRead())
            using (var output = new MemoryStream())
            {
                await input.CopyToAsync(output);
                return output.ToArray();
            }
        }

        private static MediaPlaybackStatus MapStatus(
            GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
        {
            switch (status)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing:
                    return MediaPlaybackStatus.Playing;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused:
                    return MediaPlaybackStatus.Paused;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped:
                    return MediaPlaybackStatus.Stopped;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened:
                    return MediaPlaybackStatus.Opened;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing:
                    return MediaPlaybackStatus.Changing;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed:
                    return MediaPlaybackStatus.Closed;
                default:
                    return MediaPlaybackStatus.Unknown;
            }
        }

        private void AttachSession(GlobalSystemMediaTransportControlsSession session)
        {
            session.MediaPropertiesChanged += Session_Changed;
            session.PlaybackInfoChanged += Session_Changed;
            session.TimelinePropertiesChanged += Session_Changed;
        }

        private void DetachSessions()
        {
            foreach (var session in sessions.Values)
            {
                session.MediaPropertiesChanged -= Session_Changed;
                session.PlaybackInfoChanged -= Session_Changed;
                session.TimelinePropertiesChanged -= Session_Changed;
            }
        }

        private async void Manager_Changed(
            GlobalSystemMediaTransportControlsSessionManager sender,
            object args)
        {
            try { await RefreshAsync(); }
            catch { }
        }

        private async void Session_Changed(
            GlobalSystemMediaTransportControlsSession sender,
            object args)
        {
            try { await RefreshAsync(); }
            catch { }
        }

        public void Dispose()
        {
            if (manager == null) return;
            manager.SessionsChanged -= Manager_Changed;
            manager.CurrentSessionChanged -= Manager_Changed;
            DetachSessions();
            manager = null;
            sessions.Clear();
        }
    }
}
