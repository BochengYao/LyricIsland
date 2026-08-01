using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using LyricsIsland.Core.Media;
using Windows.Media.Control;

namespace LyricsIsland.App.Media
{
    public sealed class SmTcMediaSessionService : IMediaSessionService
    {
        private GlobalSystemMediaTransportControlsSessionManager manager;
        private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> sessions =
            new Dictionary<string, GlobalSystemMediaTransportControlsSession>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> lastActivityBySessionId =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ArtworkCacheEntry> artworkBySessionId =
            new Dictionary<string, ArtworkCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly object sessionsSync = new object();
        private readonly SemaphoreSlim refreshGate = new SemaphoreSlim(1, 1);
        private readonly Dispatcher ownerDispatcher;
        private int scheduledRefreshVersion;

        public event EventHandler SessionsChanged;
        public IReadOnlyList<MediaSessionSnapshot> Sessions { get; private set; } =
            new List<MediaSessionSnapshot>().AsReadOnly();
        public string WindowsCurrentSessionId { get; private set; } = string.Empty;

        public SmTcMediaSessionService()
        {
            ownerDispatcher = Dispatcher.CurrentDispatcher;
        }

        public Task InitializeAsync()
        {
            return RunOnOwnerThreadAsync(InitializeCoreAsync);
        }

        private async Task InitializeCoreAsync()
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            manager.SessionsChanged += Manager_Changed;
            manager.CurrentSessionChanged += Manager_Changed;
            await RefreshCoreAsync();
        }

        public Task RefreshAsync()
        {
            return RunOnOwnerThreadAsync(RefreshCoreAsync);
        }

        private async Task RefreshCoreAsync()
        {
            await refreshGate.WaitAsync();
            try
            {
                if (manager == null) return;

                var current = manager.GetCurrentSession();
                WindowsCurrentSessionId = current?.SourceAppUserModelId ?? string.Empty;
                var snapshots = new List<MediaSessionSnapshot>();
                var liveSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sessionsToAttach = new List<GlobalSystemMediaTransportControlsSession>();
                var sessionsToDetach = new List<GlobalSystemMediaTransportControlsSession>();
                var liveSessions = manager.GetSessions().ToList();
                foreach (var session in liveSessions)
                {
                    var id = session.SourceAppUserModelId ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    liveSessionIds.Add(id);
                    lock (sessionsSync)
                    {
                        if (!lastActivityBySessionId.ContainsKey(id))
                        {
                            lastActivityBySessionId[id] = DateTimeOffset.UtcNow;
                        }

                        GlobalSystemMediaTransportControlsSession existing;
                        if (!sessions.TryGetValue(id, out existing) || !ReferenceEquals(existing, session))
                        {
                            if (existing != null)
                            {
                                sessionsToDetach.Add(existing);
                            }
                            sessionsToAttach.Add(session);
                        }
                        sessions[id] = session;
                    }
                }

                lock (sessionsSync)
                {
                    foreach (var staleId in sessions.Keys.Where(id => !liveSessionIds.Contains(id)).ToList())
                    {
                        sessionsToDetach.Add(sessions[staleId]);
                        sessions.Remove(staleId);
                        lastActivityBySessionId.Remove(staleId);
                        artworkBySessionId.Remove(staleId);
                    }
                }

                foreach (var stale in sessionsToDetach.Distinct()) DetachSession(stale);
                foreach (var added in sessionsToAttach.Distinct()) AttachSession(added);
                foreach (var session in liveSessions)
                {
                    if (!string.IsNullOrWhiteSpace(session.SourceAppUserModelId))
                    {
                        snapshots.Add(await ReadSnapshotAsync(session));
                    }
                }

                Sessions = snapshots.AsReadOnly();
                SessionsChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                refreshGate.Release();
            }
        }

        public MediaPlaybackStatus? GetPlaybackStatus(string id)
        {
            return ownerDispatcher.CheckAccess()
                ? GetPlaybackStatusCore(id)
                : ownerDispatcher.Invoke(() => GetPlaybackStatusCore(id));
        }

        private MediaPlaybackStatus? GetPlaybackStatusCore(string id)
        {
            GlobalSystemMediaTransportControlsSession session;
            lock (sessionsSync)
            {
                sessions.TryGetValue(id ?? string.Empty, out session);
            }

            if (session == null)
            {
                return null;
            }

            try
            {
                return MapStatus(session.GetPlaybackInfo().PlaybackStatus);
            }
            catch
            {
                return null;
            }
        }

        public Task<bool> TryPlayAsync(string id) => InvokeAsync(id, session => session.TryPlayAsync().AsTask());
        public Task<bool> TryPauseAsync(string id) => InvokeAsync(id, session => session.TryPauseAsync().AsTask());
        public Task<bool> TrySkipPreviousAsync(string id) => InvokeAsync(id, session => session.TrySkipPreviousAsync().AsTask());
        public Task<bool> TrySkipNextAsync(string id) => InvokeAsync(id, session => session.TrySkipNextAsync().AsTask());

        private Task<bool> InvokeAsync(
            string id,
            Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
        {
            return RunOnOwnerThreadAsync(() => InvokeCoreAsync(id, action));
        }

        private async Task<bool> InvokeCoreAsync(
            string id,
            Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
        {
            GlobalSystemMediaTransportControlsSession session;
            lock (sessionsSync)
            {
                sessions.TryGetValue(id ?? string.Empty, out session);
            }
            return session != null && await action(session);
        }

        private async Task<MediaSessionSnapshot> ReadSnapshotAsync(
            GlobalSystemMediaTransportControlsSession session)
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var controls = playback.Controls;
            var profile = PlayerProfileCatalog.Resolve(session.SourceAppUserModelId);
            var playbackStatus = MapStatus(playback.PlaybackStatus);
            var sampledAt = DateTimeOffset.UtcNow;
            var duration = TimelineMetadataResolver.ResolveDuration(
                timeline.EndTime,
                timeline.MaxSeekTime);
            var reportedPosition = timeline.Position;
            var positionUpdatedAt = timeline.LastUpdatedTime;
            var hasReliableTimeline = TimelineMetadataResolver.HasReliableTimeline(
                duration,
                timeline.Position);
            var compensatedPosition = TimelineSampleCompensator.Compensate(
                reportedPosition,
                positionUpdatedAt,
                sampledAt,
                playbackStatus,
                duration);

            return new MediaSessionSnapshot
            {
                SessionId = session.SourceAppUserModelId ?? string.Empty,
                SourceAppUserModelId = session.SourceAppUserModelId ?? string.Empty,
                PlayerDisplayName = profile.DisplayName,
                Title = properties.Title ?? string.Empty,
                Artist = properties.Artist ?? string.Empty,
                Album = properties.AlbumTitle ?? string.Empty,
                ArtworkBytes = await ReadArtworkAsync(
                    session.SourceAppUserModelId,
                    (properties.Title ?? string.Empty) + "\u001f" +
                    (properties.Artist ?? string.Empty) + "\u001f" +
                    (properties.AlbumTitle ?? string.Empty),
                    properties.Thumbnail),
                Position = compensatedPosition,
                Duration = duration,
                HasReliableTimeline = hasReliableTimeline,
                PlaybackStatus = playbackStatus,
                Controls = new MediaControlCapabilities
                {
                    CanPlay = controls.IsPlayEnabled,
                    CanPause = controls.IsPauseEnabled,
                    CanSkipPrevious = controls.IsPreviousEnabled,
                    CanSkipNext = controls.IsNextEnabled
                },
                LastActivityUtc = GetLastActivity(session.SourceAppUserModelId)
            };
        }

        private DateTimeOffset GetLastActivity(string sessionId)
        {
            lock (sessionsSync)
            {
                DateTimeOffset activity;
                return lastActivityBySessionId.TryGetValue(sessionId ?? string.Empty, out activity)
                    ? activity
                    : DateTimeOffset.UtcNow;
            }
        }

        private async Task<byte[]> ReadArtworkAsync(
            string sessionId,
            string signature,
            Windows.Storage.Streams.IRandomAccessStreamReference reference)
        {
            lock (sessionsSync)
            {
                ArtworkCacheEntry cached;
                if (artworkBySessionId.TryGetValue(sessionId ?? string.Empty, out cached) &&
                    string.Equals(cached.Signature, signature, StringComparison.Ordinal))
                {
                    return cached.Bytes;
                }
            }

            if (reference == null) return null;
            byte[] bytes;
            using (var stream = await reference.OpenReadAsync().AsTask())
            using (var input = stream.AsStreamForRead())
            using (var output = new MemoryStream())
            {
                await input.CopyToAsync(output);
                bytes = output.ToArray();
            }

            lock (sessionsSync)
            {
                artworkBySessionId[sessionId ?? string.Empty] = new ArtworkCacheEntry(signature, bytes);
            }
            return bytes;
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
            session.TimelinePropertiesChanged += Session_TimelineChanged;
        }

        private void DetachSession(GlobalSystemMediaTransportControlsSession session)
        {
            session.MediaPropertiesChanged -= Session_Changed;
            session.PlaybackInfoChanged -= Session_Changed;
            session.TimelinePropertiesChanged -= Session_TimelineChanged;
        }

        private void DetachSessions()
        {
            List<GlobalSystemMediaTransportControlsSession> snapshot;
            lock (sessionsSync)
            {
                snapshot = sessions.Values.ToList();
            }
            foreach (var session in snapshot)
            {
                DetachSession(session);
            }
        }

        private async void Manager_Changed(
            GlobalSystemMediaTransportControlsSessionManager sender,
            object args)
        {
            try
            {
                await RunOnOwnerThreadAsync(async () =>
                {
                    var currentId = manager?.GetCurrentSession()?.SourceAppUserModelId;
                    if (!string.IsNullOrWhiteSpace(currentId))
                    {
                        lock (sessionsSync)
                        {
                            lastActivityBySessionId[currentId] = DateTimeOffset.UtcNow;
                        }
                    }
                    await RefreshCoreAsync();
                });
            }
            catch { }
        }

        private async void Session_Changed(
            GlobalSystemMediaTransportControlsSession sender,
            object args)
        {
            try
            {
                await RunOnOwnerThreadAsync(async () =>
                {
                    var sessionId = sender?.SourceAppUserModelId;
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        lock (sessionsSync)
                        {
                            lastActivityBySessionId[sessionId] = DateTimeOffset.UtcNow;
                        }
                    }
                    await ScheduleRefreshAsync();
                });
            }
            catch { }
        }

        private async void Session_TimelineChanged(
            GlobalSystemMediaTransportControlsSession sender,
            object args)
        {
            try
            {
                await RunOnOwnerThreadAsync(ScheduleRefreshAsync);
            }
            catch { }
        }

        private async Task ScheduleRefreshAsync()
        {
            var version = Interlocked.Increment(ref scheduledRefreshVersion);
            await Task.Delay(40);
            if (version == Volatile.Read(ref scheduledRefreshVersion))
            {
                await RefreshCoreAsync();
            }
        }

        public void Dispose()
        {
            if (!ownerDispatcher.CheckAccess())
            {
                ownerDispatcher.Invoke(DisposeCore);
                return;
            }

            DisposeCore();
        }

        private void DisposeCore()
        {
            if (manager == null) return;
            manager.SessionsChanged -= Manager_Changed;
            manager.CurrentSessionChanged -= Manager_Changed;
            Interlocked.Increment(ref scheduledRefreshVersion);
            DetachSessions();
            manager = null;
            lock (sessionsSync)
            {
                sessions.Clear();
            }
            lastActivityBySessionId.Clear();
            artworkBySessionId.Clear();
        }

        private Task RunOnOwnerThreadAsync(Func<Task> action)
        {
            return ownerDispatcher.CheckAccess()
                ? action()
                : ownerDispatcher.InvokeAsync(action).Task.Unwrap();
        }

        private Task<T> RunOnOwnerThreadAsync<T>(Func<Task<T>> action)
        {
            return ownerDispatcher.CheckAccess()
                ? action()
                : ownerDispatcher.InvokeAsync(action).Task.Unwrap();
        }

        private sealed class ArtworkCacheEntry
        {
            public ArtworkCacheEntry(string signature, byte[] bytes)
            {
                Signature = signature ?? string.Empty;
                Bytes = bytes;
            }

            public string Signature { get; }
            public byte[] Bytes { get; }
        }
    }
}
