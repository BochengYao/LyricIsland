using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal static class SongEffectLayout
    {
        public static int ResolveTargetCount(double width, double height, SongEffectDefinition effect)
        {
            var pixels = Math.Max(0, width) * Math.Max(0, height);
            if (pixels >= 6000000) return effect.Count4k;
            if (pixels >= 2800000) return effect.Count1440p;
            return effect.Count1080p;
        }

        public static double ChooseSpawnX(Random random, double width, Rect currentLineSafeArea, double size)
        {
            if (random == null || width <= size) return 0;
            var maximum = Math.Max(0, width - size);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                double x;
                if (random.NextDouble() < 0.64)
                {
                    var left = random.NextDouble() < 0.5;
                    x = left ? random.NextDouble() * maximum * 0.28 : maximum * (0.72 + random.NextDouble() * 0.28);
                }
                else x = random.NextDouble() * maximum;
                var center = x + size * 0.5;
                if (currentLineSafeArea.IsEmpty || center < currentLineSafeArea.Left || center > currentLineSafeArea.Right)
                    return x;
            }
            return random.NextDouble() < 0.5 ? maximum * 0.08 : maximum * 0.88;
        }
    }

    internal sealed class MapleLeafFallRenderer : IFullscreenSongEffectRenderer
    {
        public const string EffectType = "maple-leaf-fall";
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
        private readonly Canvas rootLayer;
        private readonly Canvas surface;
        private readonly List<LeafParticle> particles = new List<LeafParticle>();
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

        public MapleLeafFallRenderer(Canvas rootLayer)
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
            var wasStaticMode = staticMode;
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
                if (!wasActive || !wasStaticMode || trackChanged || descriptorChanged) BuildStaticLeaves();
                UnsubscribeRendering();
            }
            else
            {
                if (wasStaticMode) ClearParticles();
                if (particles.Count == 0)
                {
                    AddLeaf(true);
                    AddLeaf(true);
                }
                if (!wasActive || wasStaticMode || trackChanged || descriptorChanged)
                {
                    nextSpawnUtc = DateTime.UtcNow.AddSeconds(1.2 + random.NextDouble());
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
                AddLeaf(false);
                nextSpawnUtc = now.AddSeconds(1.2 + random.NextDouble());
            }
            for (var index = particles.Count - 1; index >= 0; index--)
            {
                var particle = particles[index];
                particle.Y += particle.FallSpeed * elapsed;
                particle.Phase += elapsed * particle.SwaySpeed;
                particle.Rotation.Angle += particle.RotationSpeed * elapsed;
                particle.Translation.X = particle.BaseX + Math.Sin(particle.Phase) * particle.SwayAmplitude;
                particle.Translation.Y = particle.Y;
                var center = new Point(particle.Translation.X + particle.Size * 0.5, particle.Y + particle.Size * 0.5);
                var multiplier = context.CurrentLineSafeArea.Contains(center)
                    ? descriptor.Effect.CurrentLineOpacityMultiplier
                    : context.LyricsSafeArea.Contains(center) ? descriptor.Effect.LyricsOpacityMultiplier : 1;
                var targetOpacity = particle.BaseOpacity * multiplier;
                particle.Path.Opacity += (targetOpacity - particle.Path.Opacity) * Math.Min(1, elapsed * 5);
                if (particle.Y > context.Bounds.Height + particle.Size)
                {
                    surface.Children.Remove(particle.Path);
                    particles.RemoveAt(index);
                }
            }
        }

        private void AddLeaf(bool initial)
        {
            if (assets.Count == 0 || colors.Count == 0 || descriptor == null) return;
            var effect = descriptor.Effect;
            var size = effect.MinSize + random.NextDouble() * (effect.MaxSize - effect.MinSize);
            var x = SongEffectLayout.ChooseSpawnX(random, context.Bounds.Width, context.CurrentLineSafeArea, size);
            var y = initial ? random.NextDouble() * Math.Max(1, context.Bounds.Height * 0.55) - size : -size;
            var asset = assets[random.Next(assets.Count)];
            var color = colors[random.Next(colors.Count)];
            // Keep the stem visually legible. A full random rotation makes a small
            // maple silhouette read as a generic star, especially below 50 DIPs.
            var rotation = new RotateTransform(-52 + random.NextDouble() * 104);
            var translation = new TranslateTransform(x, y);
            var transforms = new TransformGroup();
            transforms.Children.Add(rotation);
            transforms.Children.Add(translation);
            var path = new Path
            {
                Data = asset.Geometry,
                Fill = new SolidColorBrush(color),
                Width = size,
                Height = size * asset.ViewBox.Height / asset.ViewBox.Width,
                Stretch = Stretch.Uniform,
                Opacity = effect.MinOpacity + random.NextDouble() * (effect.MaxOpacity - effect.MinOpacity),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = transforms,
                IsHitTestVisible = false,
                SnapsToDevicePixels = false
            };
            if (path.Fill.CanFreeze) path.Fill.Freeze();
            var travelSeconds = 20 + random.NextDouble() * 15;
            var particle = new LeafParticle
            {
                Path = path,
                Rotation = rotation,
                Translation = translation,
                BaseX = x,
                Y = y,
                Size = size,
                BaseOpacity = path.Opacity,
                FallSpeed = (context.Bounds.Height + size * 2) / travelSeconds,
                RotationSpeed = (random.NextDouble() - 0.5) * 20,
                SwayAmplitude = 5 + random.NextDouble() * 13,
                SwaySpeed = 0.45 + random.NextDouble() * 0.65,
                Phase = random.NextDouble() * Math.PI * 2
            };
            surface.Children.Add(path);
            particles.Add(particle);
        }

        private void BuildStaticLeaves()
        {
            ClearParticles();
            for (var index = 0; index < 3; index++)
            {
                AddLeaf(true);
                var particle = particles.LastOrDefault();
                if (particle == null) continue;
                particle.BaseX = index == 1
                    ? Math.Max(0, context.Bounds.Width - particle.Size - 30)
                    : 24 + index * 36;
                particle.Y = index == 0 ? 46 : context.Bounds.Height * (0.22 + index * 0.24);
                particle.Translation.X = particle.BaseX;
                particle.Translation.Y = particle.Y;
                particle.Path.Opacity = Math.Min(0.18, particle.BaseOpacity);
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

        private sealed class LeafParticle
        {
            public Path Path;
            public RotateTransform Rotation;
            public TranslateTransform Translation;
            public double BaseX;
            public double Y;
            public double Size;
            public double BaseOpacity;
            public double FallSpeed;
            public double RotationSpeed;
            public double SwayAmplitude;
            public double SwaySpeed;
            public double Phase;
        }
    }
}
