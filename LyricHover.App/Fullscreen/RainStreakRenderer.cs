using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal sealed class RainStreakRenderer : IFullscreenSongEffectRenderer
    {
        public const string EffectType = "rain-streaks";
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
        private readonly Canvas rootLayer;
        private readonly Canvas surface;
        private readonly List<RainParticle> particles = new List<RainParticle>();
        private readonly List<SvgVectorAsset> assets = new List<SvgVectorAsset>();
        private readonly List<Color> colors = new List<Color>();
        private SongEffectDescriptor descriptor;
        private SongEffectRenderContext context = new SongEffectRenderContext();
        private Random random = new Random(1);
        private DateTime lastFrameUtc;
        private DateTime nextSpawnUtc;
        private DateTime fadeStartedUtc;
        private bool renderingSubscribed;
        private bool staticMode;
        private bool disposed;

        public RainStreakRenderer(Canvas rootLayer)
        {
            this.rootLayer = rootLayer ?? throw new ArgumentNullException(nameof(rootLayer));
            surface = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            rootLayer.Children.Add(surface);
        }

        public bool IsActive { get; private set; }
        public bool IsRendering => renderingSubscribed;
        public int VisualCount => particles.Count;

        public void Activate(SongEffectDescriptor value, SongEffectRenderContext nextContext)
        {
            if (disposed || value?.Effect == null) return;
            var wasActive = IsActive;
            var wasStatic = staticMode;
            var trackChanged = !string.Equals(context.TrackKey, nextContext?.TrackKey, StringComparison.Ordinal);
            var descriptorChanged = !string.Equals(descriptor?.Id, value.Id, StringComparison.Ordinal);
            descriptor = value;
            if (descriptorChanged || assets.Count == 0 || colors.Count == 0) LoadAssets(value.Effect);
            UpdateContext(nextContext);
            IsActive = assets.Count > 0 && colors.Count > 0 && context.Bounds.Width > 0 && context.Bounds.Height > 0;
            surface.Opacity = IsActive ? 1 : 0;
            if (!IsActive)
            {
                ClearParticles();
                UnsubscribeRendering();
                return;
            }
            if (trackChanged || descriptorChanged)
            {
                ClearParticles();
                random = new Random(StableSeed(context.TrackKey + "\u001f" + value.Id));
            }
            fadeStartedUtc = DateTime.MinValue;
            staticMode = context.ReduceMotion || context.Quality == FullscreenRenderQuality.Static;
            if (staticMode)
            {
                if (!wasActive || !wasStatic || trackChanged || descriptorChanged) BuildStaticRain();
                UnsubscribeRendering();
            }
            else
            {
                if (wasStatic) ClearParticles();
                var target = SongEffectLayout.ResolveTargetCount(context.Bounds.Width, context.Bounds.Height, value.Effect);
                if (particles.Count == 0)
                    for (var index = 0; index < Math.Min(8, target); index++) AddRain(true);
                if (!wasActive || wasStatic || trackChanged || descriptorChanged)
                {
                    nextSpawnUtc = DateTime.UtcNow.AddMilliseconds(120);
                    lastFrameUtc = DateTime.UtcNow;
                }
                SubscribeRendering();
            }
        }

        public void UpdateContext(SongEffectRenderContext nextContext)
        {
            if (disposed || nextContext == null) return;
            context = nextContext;
            var empty = context.Bounds.IsEmpty;
            surface.Width = empty ? 0 : Math.Max(0, context.Bounds.Width);
            surface.Height = empty ? 0 : Math.Max(0, context.Bounds.Height);
            Canvas.SetLeft(surface, empty || !IsFinite(context.Bounds.Left) ? 0 : context.Bounds.Left);
            Canvas.SetTop(surface, empty || !IsFinite(context.Bounds.Top) ? 0 : context.Bounds.Top);
        }

        public void Deactivate()
        {
            if (disposed || (!IsActive && fadeStartedUtc != DateTime.MinValue)) return;
            IsActive = false;
            fadeStartedUtc = DateTime.UtcNow;
            lastFrameUtc = fadeStartedUtc;
            SubscribeRendering();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IsActive = false;
            UnsubscribeRendering();
            ClearParticles();
            rootLayer.Children.Remove(surface);
        }

        private void OnRendering(object sender, EventArgs args)
        {
            if (disposed) return;
            var now = DateTime.UtcNow;
            var interval = context.Quality == FullscreenRenderQuality.Balanced30Fps ? 1.0 / 30.0 : 1.0 / 60.0;
            var elapsed = Math.Max(0, Math.Min(0.1, (now - lastFrameUtc).TotalSeconds));
            if (elapsed < interval) return;
            lastFrameUtc = now;

            if (!IsActive)
            {
                var fade = (now - fadeStartedUtc).TotalMilliseconds / FadeDuration.TotalMilliseconds;
                surface.Opacity = Math.Max(0, 1 - fade);
                if (fade >= 1)
                {
                    ClearParticles();
                    UnsubscribeRendering();
                }
                return;
            }

            var target = SongEffectLayout.ResolveTargetCount(context.Bounds.Width, context.Bounds.Height, descriptor.Effect);
            if (particles.Count < target && now >= nextSpawnUtc)
            {
                AddRain(false);
                nextSpawnUtc = now.AddMilliseconds(70 + random.NextDouble() * 100);
            }
            for (var index = particles.Count - 1; index >= 0; index--)
            {
                var particle = particles[index];
                particle.X += particle.WindSpeed * elapsed;
                particle.Y += particle.FallSpeed * elapsed;
                particle.Translation.X = particle.X;
                particle.Translation.Y = particle.Y;
                var center = new Point(particle.X + particle.Width * 0.5, particle.Y + particle.Height * 0.5);
                var multiplier = context.CurrentLineSafeArea.Contains(center)
                    ? descriptor.Effect.CurrentLineOpacityMultiplier
                    : context.LyricsSafeArea.Contains(center) ? descriptor.Effect.LyricsOpacityMultiplier : 1;
                var targetOpacity = particle.BaseOpacity * multiplier;
                particle.Path.Opacity += (targetOpacity - particle.Path.Opacity) * Math.Min(1, elapsed * 9);
                if (particle.Y > context.Bounds.Height + particle.Height ||
                    particle.X < -particle.Width * 3 || particle.X > context.Bounds.Width + particle.Width * 3)
                {
                    surface.Children.Remove(particle.Path);
                    particles.RemoveAt(index);
                }
            }
        }

        private void AddRain(bool initial)
        {
            if (descriptor == null || assets.Count == 0 || colors.Count == 0) return;
            var effect = descriptor.Effect;
            var asset = assets[random.Next(assets.Count)];
            var height = effect.MinSize + random.NextDouble() * (effect.MaxSize - effect.MinSize);
            var width = Math.Max(2.5, height * asset.ViewBox.Width / asset.ViewBox.Height);
            var x = SongEffectLayout.ChooseSpawnX(random, context.Bounds.Width, context.CurrentLineSafeArea, width);
            var y = initial ? random.NextDouble() * Math.Max(1, context.Bounds.Height) - height : -height;
            var translation = new TranslateTransform(x, y);
            var brush = new SolidColorBrush(colors[random.Next(colors.Count)]);
            if (brush.CanFreeze) brush.Freeze();
            var path = new Path
            {
                Data = asset.Geometry,
                Fill = brush,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                Opacity = effect.MinOpacity + random.NextDouble() * (effect.MaxOpacity - effect.MinOpacity),
                RenderTransform = translation,
                IsHitTestVisible = false
            };
            var particle = new RainParticle
            {
                Path = path,
                Translation = translation,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                BaseOpacity = path.Opacity,
                FallSpeed = (context.Bounds.Height + height * 2) / (3.4 + random.NextDouble() * 3.2),
                WindSpeed = -26 + random.NextDouble() * 34
            };
            surface.Children.Add(path);
            particles.Add(particle);
        }

        private void BuildStaticRain()
        {
            ClearParticles();
            for (var index = 0; index < 3; index++)
            {
                AddRain(true);
                var particle = particles.LastOrDefault();
                if (particle == null) continue;
                particle.X = index == 1 ? Math.Max(0, context.Bounds.Width - particle.Width - 42) : 28 + index * 48;
                particle.Y = 54 + context.Bounds.Height * index * 0.24;
                particle.Translation.X = particle.X;
                particle.Translation.Y = particle.Y;
                particle.Path.Opacity = Math.Min(0.14, particle.BaseOpacity);
            }
        }

        private void LoadAssets(SongEffectDefinition effect)
        {
            assets.Clear();
            colors.Clear();
            foreach (var assetName in effect.Assets ?? Enumerable.Empty<string>())
            {
                var asset = SvgVectorAssetLoader.Load(assetName);
                if (asset != null) assets.Add(asset);
            }
            foreach (var value in effect.Colors ?? Enumerable.Empty<string>())
            {
                try
                {
                    if (ColorConverter.ConvertFromString(value) is Color color) colors.Add(color);
                }
                catch { }
            }
        }

        private void ClearParticles()
        {
            particles.Clear();
            surface.Children.Clear();
        }

        private void SubscribeRendering()
        {
            if (renderingSubscribed) return;
            CompositionTarget.Rendering += OnRendering;
            renderingSubscribed = true;
        }

        private void UnsubscribeRendering()
        {
            if (!renderingSubscribed) return;
            CompositionTarget.Rendering -= OnRendering;
            renderingSubscribed = false;
        }

        private static int StableSeed(string value)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var character in value ?? string.Empty) hash = (hash ^ character) * 16777619;
                return hash;
            }
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private sealed class RainParticle
        {
            public Path Path;
            public TranslateTransform Translation;
            public double X;
            public double Y;
            public double Width;
            public double Height;
            public double BaseOpacity;
            public double FallSpeed;
            public double WindSpeed;
        }
    }
}
