using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal static class PaperStormLayout
    {
        public static double ChooseSpawnY(Random random, double height, Rect currentLineSafeArea, double size)
        {
            if (random == null || height <= size) return 0;
            var maximum = Math.Max(0, height - size);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                // The MV's paper tornado fills the frame, but the lyric focal line
                // remains protected. Slightly favor the upper and lower thirds.
                double y;
                if (random.NextDouble() < 0.68)
                {
                    var upper = random.NextDouble() < 0.5;
                    y = upper ? random.NextDouble() * maximum * 0.34 : maximum * (0.66 + random.NextDouble() * 0.34);
                }
                else y = random.NextDouble() * maximum;
                var center = y + size * 0.5;
                if (currentLineSafeArea.IsEmpty || center < currentLineSafeArea.Top || center > currentLineSafeArea.Bottom)
                    return y;
            }
            return random.NextDouble() < 0.5 ? maximum * 0.08 : maximum * 0.88;
        }
    }

    internal sealed class FlyingPaperStormRenderer : IFullscreenSongEffectRenderer
    {
        public const string EffectType = "flying-paper-storm";
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
        private readonly Canvas rootLayer;
        private readonly Canvas surface;
        private readonly List<PaperParticle> particles = new List<PaperParticle>();
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

        public FlyingPaperStormRenderer(Canvas rootLayer)
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
                if (!wasActive || !wasStaticMode || trackChanged || descriptorChanged) BuildStaticPapers();
                UnsubscribeRendering();
            }
            else
            {
                if (wasStaticMode) ClearParticles();
                if (particles.Count == 0)
                {
                    for (var index = 0; index < 8; index++) AddPaper(true);
                }
                if (!wasActive || wasStaticMode || trackChanged || descriptorChanged)
                {
                    nextSpawnUtc = DateTime.UtcNow.AddSeconds(0.28 + random.NextDouble() * 0.24);
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
                AddPaper(false);
                nextSpawnUtc = now.AddSeconds(0.24 + random.NextDouble() * 0.34);
            }
            for (var index = particles.Count - 1; index >= 0; index--)
            {
                var particle = particles[index];
                var gust = 0.76 + (Math.Sin(particle.Phase * 0.72 + particle.GustPhase) + 1) * 0.28;
                particle.X += particle.HorizontalSpeed * particle.Direction * gust * elapsed;
                particle.Phase += particle.OrbitSpeed * elapsed;
                particle.BaseY += particle.VerticalDrift * elapsed;
                var y = particle.BaseY + Math.Sin(particle.Phase) * particle.VerticalAmplitude +
                    Math.Sin(particle.Phase * 2.37 + particle.GustPhase) * particle.VerticalAmplitude * 0.24;
                particle.Rotation.Angle += particle.RotationSpeed * gust * elapsed +
                    Math.Sin(particle.Phase * 1.84) * 18 * elapsed;
                var flipX = Math.Cos(particle.Phase * 1.16 + particle.FlipPhase);
                if (Math.Abs(flipX) < 0.10) flipX = flipX < 0 ? -0.10 : 0.10;
                particle.Flip.ScaleX = flipX;
                particle.Flip.ScaleY = 0.55 + Math.Cos(particle.Phase * 0.67 + particle.GustPhase) * 0.34;
                particle.Skew.AngleX = Math.Sin(particle.Phase * 0.82 + particle.FlipPhase) * 18;
                particle.Skew.AngleY = Math.Cos(particle.Phase * 1.11 + particle.GustPhase) * 12;
                particle.Curl.ScaleX = 0.86 + Math.Sin(particle.Phase * 1.43 + particle.GustPhase) * 0.14;
                particle.Curl.ScaleY = 0.90 + Math.Cos(particle.Phase * 1.67 + particle.FlipPhase) * 0.10;
                particle.Translation.X = particle.X;
                particle.Translation.Y = y;
                var center = new Point(particle.X + particle.Width * 0.5, y + particle.Height * 0.5);
                var multiplier = context.CurrentLineSafeArea.Contains(center)
                    ? descriptor.Effect.CurrentLineOpacityMultiplier
                    : context.LyricsSafeArea.Contains(center) ? descriptor.Effect.LyricsOpacityMultiplier : 1;
                var targetOpacity = particle.BaseOpacity * multiplier;
                particle.Visual.Opacity += (targetOpacity - particle.Visual.Opacity) * Math.Min(1, elapsed * 7);
                var beyondRight = particle.Direction > 0 && particle.X > context.Bounds.Width + particle.Width * 1.5;
                var beyondLeft = particle.Direction < 0 && particle.X < -particle.Width * 2.5;
                if (beyondRight || beyondLeft)
                {
                    surface.Children.Remove(particle.Visual);
                    particles.RemoveAt(index);
                }
            }
        }

        private void AddPaper(bool initial)
        {
            if (assets.Count == 0 || colors.Count == 0 || descriptor == null) return;
            var effect = descriptor.Effect;
            var asset = assets[random.Next(assets.Count)];
            var longestSide = effect.MinSize + random.NextDouble() * (effect.MaxSize - effect.MinSize);
            var scale = longestSide / Math.Max(asset.ViewBox.Width, asset.ViewBox.Height);
            var width = Math.Max(8, asset.ViewBox.Width * scale);
            var height = Math.Max(8, asset.ViewBox.Height * scale);
            var direction = random.NextDouble() < 0.72 ? 1 : -1;
            var x = initial
                ? -width + random.NextDouble() * (context.Bounds.Width + width * 2)
                : direction > 0 ? -width * 1.5 : context.Bounds.Width + width * 0.5;
            var y = PaperStormLayout.ChooseSpawnY(random, context.Bounds.Height, context.CurrentLineSafeArea,
                Math.Max(width, height));
            var color = colors[random.Next(colors.Count)];
            var visual = BuildPaperVisual(asset, color, width, height, random);
            var rotation = new RotateTransform(-35 + random.NextDouble() * 70);
            var skew = new SkewTransform();
            var flip = new ScaleTransform(1, 1);
            var curl = new ScaleTransform(1, 1);
            var translation = new TranslateTransform(x, y);
            var transforms = new TransformGroup();
            transforms.Children.Add(curl);
            transforms.Children.Add(skew);
            transforms.Children.Add(flip);
            transforms.Children.Add(rotation);
            transforms.Children.Add(translation);
            // Off-centre origins make the leading and trailing edges react
            // differently, which reads as flexible paper rather than a card.
            visual.RenderTransformOrigin = new Point(0.28 + random.NextDouble() * 0.44,
                0.30 + random.NextDouble() * 0.40);
            visual.RenderTransform = transforms;
            visual.Opacity = effect.MinOpacity + random.NextDouble() * (effect.MaxOpacity - effect.MinOpacity);

            var travelSeconds = 8.5 + random.NextDouble() * 7.5;
            var particle = new PaperParticle
            {
                Visual = visual,
                Rotation = rotation,
                Skew = skew,
                Flip = flip,
                Curl = curl,
                Translation = translation,
                X = x,
                BaseY = y,
                Width = width,
                Height = height,
                Direction = direction,
                BaseOpacity = visual.Opacity,
                HorizontalSpeed = (context.Bounds.Width + width * 3) / travelSeconds,
                VerticalAmplitude = 20 + random.NextDouble() * Math.Min(120, context.Bounds.Height * 0.16),
                VerticalDrift = (random.NextDouble() - 0.44) * 15,
                RotationSpeed = (random.NextDouble() - 0.5) * 105,
                OrbitSpeed = 0.55 + random.NextDouble() * 1.15,
                Phase = random.NextDouble() * Math.PI * 2,
                FlipPhase = random.NextDouble() * Math.PI * 2,
                GustPhase = random.NextDouble() * Math.PI * 2
            };
            surface.Children.Add(visual);
            particles.Add(particle);
        }

        private static Canvas BuildPaperVisual(
            SvgVectorAsset asset,
            Color color,
            double width,
            double height,
            Random random)
        {
            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false,
                SnapsToDevicePixels = false
            };
            var fill = new SolidColorBrush(color);
            if (fill.CanFreeze) fill.Freeze();
            canvas.Children.Add(new Path
            {
                Data = asset.Geometry,
                Fill = fill,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            });
            var curlShade = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            var shadeCenter = 0.34 + random.NextDouble() * 0.34;
            curlShade.GradientStops.Add(new GradientStop(Colors.Transparent, Math.Max(0, shadeCenter - 0.24)));
            curlShade.GradientStops.Add(new GradientStop(Color.FromArgb(38, 40, 40, 43), shadeCenter));
            curlShade.GradientStops.Add(new GradientStop(Color.FromArgb(8, 255, 255, 255), Math.Min(1, shadeCenter + 0.22)));
            if (curlShade.CanFreeze) curlShade.Freeze();
            canvas.Children.Add(new Path
            {
                Data = asset.Geometry,
                Fill = curlShade,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            });
            var ink = new SolidColorBrush(Color.FromArgb(72, 43, 43, 45));
            if (ink.CanFreeze) ink.Freeze();
            var lineCount = random.NextDouble() < 0.22 ? 0 : 2 + random.Next(4);
            var lineStart = 0.18 + random.NextDouble() * 0.08;
            for (var line = 0; line < lineCount; line++)
            {
                var mark = new Rectangle
                {
                    Width = width * (0.34 + random.NextDouble() * 0.38),
                    Height = Math.Max(0.8, height * 0.018),
                    Fill = ink,
                    RadiusX = 0.5,
                    RadiusY = 0.5,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(mark, width * lineStart);
                Canvas.SetTop(mark, height * (0.22 + line * 0.115));
                canvas.Children.Add(mark);
            }
            return canvas;
        }

        private void BuildStaticPapers()
        {
            ClearParticles();
            for (var index = 0; index < 3; index++)
            {
                AddPaper(true);
                var particle = particles.LastOrDefault();
                if (particle == null) continue;
                particle.X = index == 1
                    ? Math.Max(0, context.Bounds.Width - particle.Width - 34)
                    : 28 + index * 48;
                particle.BaseY = index == 0 ? 54 : context.Bounds.Height * (0.20 + index * 0.27);
                particle.Translation.X = particle.X;
                particle.Translation.Y = particle.BaseY;
                particle.Visual.Opacity = Math.Min(0.16, particle.BaseOpacity);
                particle.Flip.ScaleX = index == 1 ? -0.66 : 0.72;
                particle.Flip.ScaleY = 0.72;
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

        private sealed class PaperParticle
        {
            public Canvas Visual;
            public RotateTransform Rotation;
            public SkewTransform Skew;
            public ScaleTransform Flip;
            public ScaleTransform Curl;
            public TranslateTransform Translation;
            public double X;
            public double BaseY;
            public double Width;
            public double Height;
            public double Direction;
            public double BaseOpacity;
            public double HorizontalSpeed;
            public double VerticalAmplitude;
            public double VerticalDrift;
            public double RotationSpeed;
            public double OrbitSpeed;
            public double Phase;
            public double FlipPhase;
            public double GustPhase;
        }
    }
}
