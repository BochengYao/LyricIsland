using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal sealed class EdgeFireworksRenderer : IFullscreenSongEffectRenderer
    {
        public const string EffectType = "edge-fireworks";
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
        private readonly Canvas rootLayer;
        private readonly Canvas surface;
        private readonly List<FireworkBurst> bursts = new List<FireworkBurst>();
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

        public EdgeFireworksRenderer(Canvas rootLayer)
        {
            this.rootLayer = rootLayer ?? throw new ArgumentNullException(nameof(rootLayer));
            surface = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            rootLayer.Children.Add(surface);
        }

        public bool IsActive { get; private set; }
        public bool IsRendering => renderingSubscribed;
        public int VisualCount => bursts.Count;

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
                ClearBursts();
                UnsubscribeRendering();
                return;
            }
            if (trackChanged || descriptorChanged)
            {
                ClearBursts();
                random = new Random(StableSeed(context.TrackKey + "\u001f" + value.Id));
            }
            fadeStartedUtc = DateTime.MinValue;
            staticMode = context.ReduceMotion || context.Quality == FullscreenRenderQuality.Static;
            if (staticMode)
            {
                if (!wasActive || !wasStatic || trackChanged || descriptorChanged) BuildStaticBursts();
                UnsubscribeRendering();
            }
            else
            {
                if (wasStatic) ClearBursts();
                if (bursts.Count == 0)
                {
                    AddBurst(true);
                    AddBurst(true);
                }
                if (!wasActive || wasStatic || trackChanged || descriptorChanged)
                {
                    nextSpawnUtc = DateTime.UtcNow.AddSeconds(0.55 + random.NextDouble() * 0.55);
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
            ClearBursts();
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
                    ClearBursts();
                    UnsubscribeRendering();
                }
                return;
            }

            var target = SongEffectLayout.ResolveTargetCount(context.Bounds.Width, context.Bounds.Height, descriptor.Effect);
            if (bursts.Count < target && now >= nextSpawnUtc)
            {
                AddBurst(false);
                nextSpawnUtc = now.AddSeconds(0.70 + random.NextDouble() * 0.90);
            }
            for (var index = bursts.Count - 1; index >= 0; index--)
            {
                var burst = bursts[index];
                burst.Age += elapsed;
                var progress = Math.Min(1, burst.Age / burst.Duration);
                UpdateBurst(burst, progress);
                if (progress >= 1)
                {
                    surface.Children.Remove(burst.Visual);
                    bursts.RemoveAt(index);
                }
            }
        }

        private void AddBurst(bool initial)
        {
            if (descriptor == null || assets.Count == 0 || colors.Count == 0) return;
            var effect = descriptor.Effect;
            var size = effect.MinSize + random.NextDouble() * (effect.MaxSize - effect.MinSize);
            var center = ChooseBurstCenter(size);
            var visual = new Canvas
            {
                Width = size,
                Height = size,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(visual, center.X - size * 0.5);
            Canvas.SetTop(visual, center.Y - size * 0.5);
            const int outerSparkCount = 16;
            const int innerSparkCount = 8;
            var sparkCount = outerSparkCount + innerSparkCount;
            var sparks = new List<FireworkSpark>(sparkCount);
            var asset = assets[random.Next(assets.Count)];
            var ringRotation = random.NextDouble() * Math.PI * 2;
            for (var sparkIndex = 0; sparkIndex < sparkCount; sparkIndex++)
            {
                var outerRing = sparkIndex < outerSparkCount;
                var ringIndex = outerRing ? sparkIndex : sparkIndex - outerSparkCount;
                var ringCount = outerRing ? outerSparkCount : innerSparkCount;
                var angle = ringRotation + Math.PI * 2 * ringIndex / ringCount +
                    (outerRing ? 0 : Math.PI / ringCount);
                var sparkLength = Math.Max(outerRing ? 10 : 7, size * (outerRing ? 0.082 : 0.055));
                var sparkWidth = Math.Max(2.2, sparkLength * asset.ViewBox.Width / asset.ViewBox.Height);
                var brush = new SolidColorBrush(colors[(sparkIndex + random.Next(colors.Count)) % colors.Count]);
                if (brush.CanFreeze) brush.Freeze();
                var rotation = new RotateTransform(angle * 180 / Math.PI + 90);
                var path = new Path
                {
                    Data = asset.Geometry,
                    Fill = brush,
                    Width = sparkWidth,
                    Height = sparkLength,
                    Stretch = Stretch.Fill,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = rotation,
                    IsHitTestVisible = false
                };
                visual.Children.Add(path);
                sparks.Add(new FireworkSpark
                {
                    Path = path,
                    Angle = angle,
                    Width = sparkWidth,
                    Height = sparkLength,
                    DistanceScale = outerRing ? 1 : 0.58
                });
            }
            var centerGlow = new Ellipse
            {
                Width = Math.Max(6, size * 0.042),
                Height = Math.Max(6, size * 0.042),
                Fill = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(centerGlow, size * 0.5 - centerGlow.Width * 0.5);
            Canvas.SetTop(centerGlow, size * 0.5 - centerGlow.Height * 0.5);
            visual.Children.Add(centerGlow);
            var centerMultiplier = context.CurrentLineSafeArea.Contains(center)
                ? effect.CurrentLineOpacityMultiplier
                : context.LyricsSafeArea.Contains(center) ? effect.LyricsOpacityMultiplier : 1;
            var burst = new FireworkBurst
            {
                Visual = visual,
                Sparks = sparks,
                Size = size,
                Duration = 4.20 + random.NextDouble() * 0.90,
                Age = initial ? 0.58 + random.NextDouble() * 0.20 : 0,
                BaseOpacity = (effect.MinOpacity + random.NextDouble() *
                    (effect.MaxOpacity - effect.MinOpacity)) * centerMultiplier
            };
            surface.Children.Add(visual);
            bursts.Add(burst);
            UpdateBurst(burst, burst.Age / burst.Duration);
        }

        private Point ChooseBurstCenter(double size)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var left = bursts.Count % 2 == 0;
                var x = context.Bounds.Width * (left
                    ? 0.37 + random.NextDouble() * 0.08
                    : 0.82 + random.NextDouble() * 0.10);
                var upper = left || random.NextDouble() < 0.82;
                var y = context.Bounds.Height * (upper
                    ? 0.08 + random.NextDouble() * 0.16
                    : 0.68 + random.NextDouble() * 0.13);
                var center = new Point(x, y);
                if (!context.CurrentLineSafeArea.Contains(center)) return center;
            }
            return new Point(size * 0.65, size * 0.65);
        }

        private static void UpdateBurst(FireworkBurst burst, double progress)
        {
            progress = Math.Max(0, Math.Min(1, progress));
            var eased = 1 - Math.Pow(1 - progress, 2.4);
            var radius = burst.Size * 0.39 * eased;
            var fade = progress < 0.34 ? progress / 0.34 : 1 - (progress - 0.34) / 0.66;
            burst.Visual.Opacity = burst.BaseOpacity * Math.Max(0, fade);
            var center = burst.Size * 0.5;
            foreach (var spark in burst.Sparks)
            {
                var distance = radius * spark.DistanceScale;
                Canvas.SetLeft(spark.Path, center + Math.Cos(spark.Angle) * distance - spark.Width * 0.5);
                Canvas.SetTop(spark.Path, center + Math.Sin(spark.Angle) * distance - spark.Height * 0.5);
            }
        }

        private void BuildStaticBursts()
        {
            ClearBursts();
            for (var index = 0; index < 2; index++)
            {
                AddBurst(false);
                var burst = bursts.LastOrDefault();
                if (burst == null) continue;
                burst.Age = burst.Duration * (0.48 + index * 0.10);
                burst.BaseOpacity = Math.Min(0.18, burst.BaseOpacity);
                UpdateBurst(burst, burst.Age / burst.Duration);
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

        private void ClearBursts()
        {
            bursts.Clear();
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

        private sealed class FireworkBurst
        {
            public Canvas Visual;
            public List<FireworkSpark> Sparks;
            public double Size;
            public double Duration;
            public double Age;
            public double BaseOpacity;
        }

        private sealed class FireworkSpark
        {
            public Path Path;
            public double Angle;
            public double Width;
            public double Height;
            public double DistanceScale;
        }
    }
}
