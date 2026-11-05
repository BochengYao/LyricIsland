using System;
using System.Windows;
using System.Windows.Controls;
using LyricHover.Core.Media;

namespace LyricHover.App.Fullscreen
{
    internal sealed class SongEffectRenderContext
    {
        public Rect Bounds { get; set; }
        public Rect LyricsSafeArea { get; set; }
        public Rect CurrentLineSafeArea { get; set; }
        public FullscreenRenderQuality Quality { get; set; }
        public bool ReduceMotion { get; set; }
        public string TrackKey { get; set; }
    }

    internal interface IFullscreenSongEffectRenderer : IDisposable
    {
        bool IsActive { get; }
        bool IsRendering { get; }
        int VisualCount { get; }
        void Activate(SongEffectDescriptor descriptor, SongEffectRenderContext context);
        void UpdateContext(SongEffectRenderContext context);
        void Deactivate();
    }

    internal sealed class FullscreenSongEffectHost : IDisposable
    {
        private readonly Canvas layer;
        private readonly SongEffectCatalog catalog;
        private IFullscreenSongEffectRenderer renderer;
        private string rendererType = string.Empty;
        private SongEffectDescriptor activeDescriptor;
        private bool disposed;

        public FullscreenSongEffectHost(Canvas layer)
            : this(layer, SongEffectCatalog.LoadDefault())
        {
        }

        internal FullscreenSongEffectHost(Canvas layer, SongEffectCatalog catalog)
        {
            this.layer = layer ?? throw new ArgumentNullException(nameof(layer));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        internal string ActiveEffectId => renderer?.IsActive == true ? activeDescriptor?.Id ?? string.Empty : string.Empty;
        internal bool IsRendering => renderer?.IsRendering == true;
        internal int VisualCount => renderer?.VisualCount ?? 0;

        public void Update(MediaSessionSnapshot session, OverlayPlacementSettings settings, SongEffectRenderContext context)
        {
            if (disposed) return;
            var playable = session?.PlaybackStatus == MediaPlaybackStatus.Playing ||
                session?.PlaybackStatus == MediaPlaybackStatus.Paused;
            var descriptor = settings?.FullscreenSongEasterEggsEnabled == true && playable
                ? catalog.Resolve(session)
                : null;
            context = context ?? new SongEffectRenderContext();
            context.Quality = settings?.FullscreenRenderQuality ?? FullscreenRenderQuality.High60Fps;
            context.ReduceMotion = settings?.FullscreenReduceMotion == true;
            if (descriptor == null)
            {
                renderer?.Deactivate();
                return;
            }

            if (renderer == null || !string.Equals(rendererType, descriptor.Effect.Type, StringComparison.Ordinal))
            {
                renderer?.Dispose();
                renderer = CreateRenderer(descriptor.Effect.Type);
                rendererType = descriptor.Effect.Type ?? string.Empty;
            }
            if (renderer == null) return;
            activeDescriptor = descriptor;
            renderer.Activate(descriptor, context);
        }

        public void UpdateSafeAreas(SongEffectRenderContext context)
        {
            if (disposed || renderer == null || context == null) return;
            renderer.UpdateContext(context);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            renderer?.Dispose();
            renderer = null;
            activeDescriptor = null;
            layer.Children.Clear();
        }

        private IFullscreenSongEffectRenderer CreateRenderer(string effectType)
        {
            if (string.Equals(effectType, MapleLeafFallRenderer.EffectType, StringComparison.Ordinal))
                return new MapleLeafFallRenderer(layer);
            if (string.Equals(effectType, FlyingPaperStormRenderer.EffectType, StringComparison.Ordinal))
                return new FlyingPaperStormRenderer(layer);
            return null;
        }

        internal static bool IsRegisteredEffectType(string effectType)
        {
            return string.Equals(effectType, MapleLeafFallRenderer.EffectType, StringComparison.Ordinal) ||
                string.Equals(effectType, FlyingPaperStormRenderer.EffectType, StringComparison.Ordinal);
        }
    }
}
