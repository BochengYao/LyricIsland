using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal sealed class AmbientVectorDriftRenderer : IFullscreenSongEffectRenderer
    {
        public const string CosmicEffectType = "cosmic-drift";
        public const string NeonEffectType = "neon-speed-lines";
        public const string BatEffectType = "bat-swarm";
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
        private readonly Canvas rootLayer;
        private readonly Canvas surface;
        private readonly List<DriftParticle> particles = new List<DriftParticle>();
        private readonly List<SvgVectorAsset> assets = new List<SvgVectorAsset>();
        private readonly List<Color> colors = new List<Color>();
        private SongEffectDescriptor descriptor;
        private SongEffectRenderContext context = new SongEffectRenderContext();
        private DriftProfile profile;
        private Random random = new Random(1);
        private DateTime lastFrameUtc;
        private DateTime nextSpawnUtc;
        private DateTime fadeStartedUtc;
        private bool renderingSubscribed;
        private bool staticMode;
        private bool disposed;

        public AmbientVectorDriftRenderer(Canvas rootLayer)
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
            profile = ResolveProfile(value.Effect.Type);
            if (descriptorChanged || assets.Count == 0 || colors.Count == 0) LoadAssets(value.Effect);
            UpdateContext(nextContext);
            IsActive = profile != DriftProfile.None && assets.Count > 0 && colors.Count > 0 &&
                context.Bounds.Width > 0 && context.Bounds.Height > 0;
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
                if (!wasActive || !wasStatic || trackChanged || descriptorChanged) BuildStaticParticles();
                UnsubscribeRendering();
            }
            else
            {
                if (wasStatic) ClearParticles();
                var target = SongEffectLayout.ResolveTargetCount(context.Bounds.Width, context.Bounds.Height, value.Effect);
                if (particles.Count == 0)
                    for (var index = 0; index < Math.Min(3, target); index++) AddParticle(true);
                if (!wasActive || wasStatic || trackChanged || descriptorChanged)
                {
                    nextSpawnUtc = DateTime.UtcNow.AddSeconds(SpawnDelay());
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
                AddParticle(false);
                nextSpawnUtc = now.AddSeconds(SpawnDelay());
            }
            for (var index = particles.Count - 1; index >= 0; index--)
            {
                var particle = particles[index];
                particle.Phase += particle.PhaseSpeed * elapsed;
                particle.X += particle.VelocityX * elapsed;
                particle.Y += particle.VelocityY * elapsed;
                var y = particle.Y + Math.Sin(particle.Phase) * particle.WaveAmplitude;
                particle.Translation.X = particle.X;
                particle.Translation.Y = y;
                particle.Rotation.Angle += particle.RotationSpeed * elapsed;

                if (profile == DriftProfile.Bat)
                {
                    particle.Scale.ScaleY = 0.58 + Math.Abs(Math.Sin(particle.Phase * 2.4)) * 0.48;
                    particle.Rotation.Angle = Math.Sin(particle.Phase) * 8;
                }
                else if (profile == DriftProfile.Cosmic)
                {
                    var pulse = 0.86 + Math.Sin(particle.Phase * 0.8 + particle.PulsePhase) * 0.14;
                    particle.Scale.ScaleX = pulse;
                    particle.Scale.ScaleY = pulse;
                }

                var center = new Point(particle.X + particle.Width * 0.5, y + particle.Height * 0.5);
                var multiplier = context.CurrentLineSafeArea.Contains(center)
                    ? descriptor.Effect.CurrentLineOpacityMultiplier
                    : context.LyricsSafeArea.Contains(center) ? descriptor.Effect.LyricsOpacityMultiplier : 1;
                var targetOpacity = particle.BaseOpacity * multiplier;
                particle.Path.Opacity += (targetOpacity - particle.Path.Opacity) * Math.Min(1, elapsed * 7);

                var beyond = particle.VelocityX >= 0
                    ? particle.X > context.Bounds.Width + particle.Width * 2
                    : particle.X < -particle.Width * 2;
                if (beyond || y < -particle.Height * 3 || y > context.Bounds.Height + particle.Height * 3)
                {
                    surface.Children.Remove(particle.Path);
                    particles.RemoveAt(index);
                }
            }
        }

        private void AddParticle(bool initial)
        {
            if (descriptor == null || assets.Count == 0 || colors.Count == 0) return;
            var effect = descriptor.Effect;
            var asset = assets[random.Next(assets.Count)];
            var longest = effect.MinSize + random.NextDouble() * (effect.MaxSize - effect.MinSize);
            var assetScale = longest / Math.Max(asset.ViewBox.Width, asset.ViewBox.Height);
            var width = Math.Max(6, asset.ViewBox.Width * assetScale);
            var height = Math.Max(4, asset.ViewBox.Height * assetScale);
            var direction = random.NextDouble() < 0.62 ? 1 : -1;
            var x = initial
                ? random.NextDouble() * Math.Max(1, context.Bounds.Width - width)
                : direction > 0 ? -width * 1.5 : context.Bounds.Width + width * 0.5;
            var y = PaperStormLayout.ChooseSpawnY(random, context.Bounds.Height,
                context.CurrentLineSafeArea, Math.Max(width, height));
            var rotation = new RotateTransform(profile == DriftProfile.Neon ? -5 + random.NextDouble() * 10 :
                -24 + random.NextDouble() * 48);
            var scale = new ScaleTransform(1, 1);
            var translation = new TranslateTransform(x, y);
            var transforms = new TransformGroup();
            transforms.Children.Add(scale);
            transforms.Children.Add(rotation);
            transforms.Children.Add(translation);
            var brush = new SolidColorBrush(colors[random.Next(colors.Count)]);
            if (brush.CanFreeze) brush.Freeze();
            var path = new Path
            {
                Data = asset.Geometry,
                Fill = brush,
                Stroke = profile == DriftProfile.Neon ? brush : null,
                StrokeThickness = profile == DriftProfile.Neon ? 1.5 : 0,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                Opacity = effect.MinOpacity + random.NextDouble() * (effect.MaxOpacity - effect.MinOpacity),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = transforms,
                IsHitTestVisible = false
            };
            var travelSeconds = profile == DriftProfile.Neon ? 5.5 + random.NextDouble() * 4.5 :
                profile == DriftProfile.Bat ? 9 + random.NextDouble() * 7 : 18 + random.NextDouble() * 12;
            var horizontalSpeed = (context.Bounds.Width + width * 3) / travelSeconds * direction;
            var particle = new DriftParticle
            {
                Path = path,
                Rotation = rotation,
                Scale = scale,
                Translation = translation,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                BaseOpacity = path.Opacity,
                VelocityX = horizontalSpeed,
                VelocityY = profile == DriftProfile.Cosmic ? -5 + random.NextDouble() * 10 :
                    -3 + random.NextDouble() * 6,
                WaveAmplitude = profile == DriftProfile.Neon ? 2 + random.NextDouble() * 5 :
                    profile == DriftProfile.Bat ? 12 + random.NextDouble() * 28 : 5 + random.NextDouble() * 13,
                PhaseSpeed = profile == DriftProfile.Bat ? 2.1 + random.NextDouble() * 1.8 :
                    0.45 + random.NextDouble() * 0.75,
                RotationSpeed = profile == DriftProfile.Cosmic ? -8 + random.NextDouble() * 16 : 0,
                Phase = random.NextDouble() * Math.PI * 2,
                PulsePhase = random.NextDouble() * Math.PI * 2
            };
            surface.Children.Add(path);
            particles.Add(particle);
        }

        private void BuildStaticParticles()
        {
            ClearParticles();
            for (var index = 0; index < 3; index++)
            {
                AddParticle(true);
                var particle = particles.LastOrDefault();
                if (particle == null) continue;
                particle.X = index == 1 ? Math.Max(0, context.Bounds.Width - particle.Width - 34) : 26 + index * 52;
                particle.Y = index == 0 ? 42 : context.Bounds.Height * (0.22 + index * 0.25);
                particle.Translation.X = particle.X;
                particle.Translation.Y = particle.Y;
                particle.Path.Opacity = Math.Min(0.16, particle.BaseOpacity);
            }
        }

        private double SpawnDelay()
        {
            if (profile == DriftProfile.Neon) return 0.45 + random.NextDouble() * 0.55;
            if (profile == DriftProfile.Bat) return 0.75 + random.NextDouble() * 0.75;
            return 0.85 + random.NextDouble() * 0.85;
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

        internal static bool Supports(string effectType)
        {
            return string.Equals(effectType, CosmicEffectType, StringComparison.Ordinal) ||
                string.Equals(effectType, NeonEffectType, StringComparison.Ordinal) ||
                string.Equals(effectType, BatEffectType, StringComparison.Ordinal);
        }

        private static DriftProfile ResolveProfile(string effectType)
        {
            if (string.Equals(effectType, CosmicEffectType, StringComparison.Ordinal)) return DriftProfile.Cosmic;
            if (string.Equals(effectType, NeonEffectType, StringComparison.Ordinal)) return DriftProfile.Neon;
            if (string.Equals(effectType, BatEffectType, StringComparison.Ordinal)) return DriftProfile.Bat;
            return DriftProfile.None;
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

        private enum DriftProfile
        {
            None,
            Cosmic,
            Neon,
            Bat
        }

        private sealed class DriftParticle
        {
            public Path Path;
            public RotateTransform Rotation;
            public ScaleTransform Scale;
            public TranslateTransform Translation;
            public double X;
            public double Y;
            public double Width;
            public double Height;
            public double BaseOpacity;
            public double VelocityX;
            public double VelocityY;
            public double WaveAmplitude;
            public double PhaseSpeed;
            public double RotationSpeed;
            public double Phase;
            public double PulsePhase;
        }
    }
}
