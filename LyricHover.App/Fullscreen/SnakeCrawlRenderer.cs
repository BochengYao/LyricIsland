using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal sealed class SnakeCrawlRenderer : IFullscreenSongEffectRenderer
    {
        public const string EffectType = "snake-crawl";
        private const int SegmentCount = 24;
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
        private readonly Canvas rootLayer;
        private readonly Canvas surface;
        private readonly List<SnakeParticle> particles = new List<SnakeParticle>();
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

        public SnakeCrawlRenderer(Canvas rootLayer)
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
                if (!wasActive || !wasStaticMode || trackChanged || descriptorChanged) BuildStaticSnakes();
                UnsubscribeRendering();
            }
            else
            {
                if (wasStaticMode) ClearParticles();
                var target = SongEffectLayout.ResolveTargetCount(context.Bounds.Width, context.Bounds.Height, value.Effect);
                if (particles.Count == 0)
                    for (var index = 0; index < Math.Min(2, target); index++) AddSnake(true);
                if (!wasActive || wasStaticMode || trackChanged || descriptorChanged)
                {
                    nextSpawnUtc = DateTime.UtcNow.AddSeconds(1.4 + random.NextDouble());
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
            foreach (var particle in particles)
            {
                particle.Visual.Width = surface.Width;
                particle.Visual.Height = surface.Height;
            }
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
                AddSnake(false);
                nextSpawnUtc = now.AddSeconds(1.8 + random.NextDouble() * 1.4);
            }
            for (var index = particles.Count - 1; index >= 0; index--)
            {
                var particle = particles[index];
                var pulse = 0.94 + Math.Sin(particle.Phase * 0.55 + particle.SpeedPhase) * 0.06;
                particle.HeadDistance += particle.Speed * particle.Direction * pulse * elapsed;
                particle.Phase += elapsed * particle.WaveSpeed;
                UpdateSnakeVisual(particle, elapsed);
                if (HasLeftBounds(particle))
                {
                    surface.Children.Remove(particle.Visual);
                    particles.RemoveAt(index);
                }
            }
        }

        private void AddSnake(bool initial)
        {
            if (descriptor == null || assets.Count == 0 || colors.Count == 0) return;
            var effect = descriptor.Effect;
            var asset = assets[random.Next(assets.Count)];
            var length = effect.MinSize + random.NextDouble() * (effect.MaxSize - effect.MinSize);
            var thickness = 8.5 + (length - effect.MinSize) /
                Math.Max(1, effect.MaxSize - effect.MinSize) * 4.0;
            var horizontal = random.NextDouble() < 0.74;
            var direction = random.NextDouble() < 0.68 ? 1 : -1;
            var travelSpan = horizontal ? context.Bounds.Width : context.Bounds.Height;
            var edgeOffset = 26 + random.NextDouble() * 52;
            var baseCross = horizontal
                ? (random.NextDouble() < 0.5 ? edgeOffset : context.Bounds.Height - edgeOffset)
                : (random.NextDouble() < 0.5 ? edgeOffset : context.Bounds.Width - edgeOffset);
            var headDistance = initial
                ? random.NextDouble() * Math.Max(1, travelSpan)
                : direction > 0 ? -thickness * 4 : travelSpan + thickness * 4;
            var color = colors[random.Next(colors.Count)];
            var particle = BuildSnakeVisual(asset, color, thickness);
            particle.Horizontal = horizontal;
            particle.Direction = direction;
            particle.Length = length;
            particle.HeadDistance = headDistance;
            particle.BaseCross = baseCross;
            particle.Amplitude = 11 + random.NextDouble() * 6;
            particle.Wavelength = 58 + random.NextDouble() * 26;
            particle.Phase = random.NextDouble() * Math.PI * 2;
            particle.SpeedPhase = random.NextDouble() * Math.PI * 2;
            particle.WaveSpeed = 0.55 + random.NextDouble() * 0.45;
            particle.Speed = (travelSpan + length * 2) / (15 + random.NextDouble() * 6);
            particle.BaseOpacity = effect.MinOpacity + random.NextDouble() * (effect.MaxOpacity - effect.MinOpacity);
            particle.Visual.Opacity = particle.BaseOpacity;
            particle.Visual.Width = context.Bounds.Width;
            particle.Visual.Height = context.Bounds.Height;
            UpdateSnakeVisual(particle, 0);
            surface.Children.Add(particle.Visual);
            particles.Add(particle);
        }

        private SnakeParticle BuildSnakeVisual(SvgVectorAsset asset, Color color, double thickness)
        {
            var geometry = new PathGeometry();
            var segment = new PolyLineSegment { IsStroked = true };
            for (var index = 0; index < SegmentCount * 2 - 1; index++)
                segment.Points.Add(new Point());
            // PolyLineSegment's value constructor copies the supplied collection.
            // Keep the live dependency-property collection so per-frame updates
            // invalidate the geometry that WPF actually renders.
            var points = segment.Points;
            var figure = new PathFigure { IsClosed = true, IsFilled = true };
            figure.Segments.Add(segment);
            geometry.Figures.Add(figure);

            var body = new SolidColorBrush(color);
            var outline = new SolidColorBrush(Color.FromArgb(220, 226, 226, 226));
            var pattern = new SolidColorBrush(Color.FromArgb(215, 242, 242, 242));
            if (body.CanFreeze) body.Freeze();
            if (outline.CanFreeze) outline.Freeze();
            if (pattern.CanFreeze) pattern.Freeze();

            var visual = new Canvas { IsHitTestVisible = false };
            visual.Children.Add(new Path
            {
                Data = geometry,
                Fill = body,
                Stroke = outline,
                StrokeThickness = 1.35,
                StrokeLineJoin = PenLineJoin.Miter,
                IsHitTestVisible = false
            });

            var scaleMarks = new List<ScaleMark>();
            for (var markIndex = 0; markIndex < 5; markIndex++)
            {
                var markWidth = thickness * 0.90;
                var markHeight = thickness * 0.58;
                var mark = new Polyline
                {
                    Width = markWidth,
                    Height = markHeight,
                    Points = new PointCollection
                    {
                        new Point(0, 0),
                        new Point(markWidth * 0.50, markHeight),
                        new Point(markWidth, 0)
                    },
                    Stroke = pattern,
                    StrokeThickness = Math.Max(0.85, thickness * 0.10),
                    StrokeLineJoin = PenLineJoin.Miter,
                    Stretch = Stretch.Fill,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    IsHitTestVisible = false
                };
                var rotation = new RotateTransform();
                mark.RenderTransform = rotation;
                visual.Children.Add(mark);
                scaleMarks.Add(new ScaleMark
                {
                    Visual = mark,
                    Rotation = rotation,
                    CenterIndex = 6 + markIndex * 3
                });
            }

            var headWidth = thickness * 3.45;
            var headHeight = thickness * 2.25;
            var head = new Canvas
            {
                Width = headWidth,
                Height = headHeight,
                RenderTransformOrigin = new Point(0.5, 0.5),
                IsHitTestVisible = false
            };
            head.Children.Add(new Path
            {
                Data = asset.Geometry,
                Fill = body,
                Stroke = outline,
                StrokeThickness = 1.1,
                Width = headWidth,
                Height = headHeight,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            });
            var facialStroke = new SolidColorBrush(Color.FromArgb(225, 246, 246, 246));
            if (facialStroke.CanFreeze) facialStroke.Freeze();
            head.Children.Add(new Line
            {
                X1 = headWidth * 0.54,
                Y1 = headHeight * 0.29,
                X2 = headWidth * 0.76,
                Y2 = headHeight * 0.39,
                Stroke = facialStroke,
                StrokeThickness = Math.Max(1, thickness * 0.12),
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                IsHitTestVisible = false
            });
            head.Children.Add(new Line
            {
                X1 = headWidth * 0.54,
                Y1 = headHeight * 0.71,
                X2 = headWidth * 0.76,
                Y2 = headHeight * 0.61,
                Stroke = facialStroke,
                StrokeThickness = Math.Max(1, thickness * 0.12),
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                IsHitTestVisible = false
            });
            for (var fork = 0; fork < 2; fork++)
            {
                head.Children.Add(new Line
                {
                    X1 = headWidth * 0.96,
                    Y1 = headHeight * 0.50,
                    X2 = headWidth * 1.20,
                    Y2 = headHeight * (fork == 0 ? 0.36 : 0.64),
                    Stroke = facialStroke,
                    StrokeThickness = Math.Max(0.9, thickness * 0.09),
                    StrokeStartLineCap = PenLineCap.Square,
                    StrokeEndLineCap = PenLineCap.Square,
                    IsHitTestVisible = false
                });
            }
            var headRotation = new RotateTransform();
            head.RenderTransform = headRotation;
            visual.Children.Add(head);

            return new SnakeParticle
            {
                Visual = visual,
                Figure = figure,
                BodyPoints = points,
                Centerline = new Point[SegmentCount],
                LeftEdge = new Point[SegmentCount],
                RightEdge = new Point[SegmentCount],
                ScaleMarks = scaleMarks,
                Head = head,
                HeadRotation = headRotation,
                HeadWidth = headWidth,
                HeadHeight = headHeight,
                Thickness = thickness
            };
        }

        private void UpdateSnakeVisual(SnakeParticle particle, double elapsed)
        {
            var step = particle.Length / (SegmentCount - 1);
            var safeMultiplier = 1.0;
            for (var index = 0; index < SegmentCount; index++)
            {
                var along = particle.HeadDistance - particle.Direction * (particle.Length - step * index);
                var wave = along / particle.Wavelength * Math.PI * 2 + particle.Phase;
                var cross = particle.BaseCross + Math.Sin(wave) * particle.Amplitude +
                    Math.Sin(wave * 0.47 + particle.SpeedPhase) * particle.Amplitude * 0.24;
                var point = particle.Horizontal ? new Point(along, cross) : new Point(cross, along);
                particle.Centerline[index] = point;
                if (index % 5 == 0 || index == SegmentCount - 1)
                {
                    if (context.CurrentLineSafeArea.Contains(point)) safeMultiplier = Math.Min(safeMultiplier,
                        descriptor.Effect.CurrentLineOpacityMultiplier);
                    else if (context.LyricsSafeArea.Contains(point)) safeMultiplier = Math.Min(safeMultiplier,
                        descriptor.Effect.LyricsOpacityMultiplier);
                }
            }
            for (var index = 0; index < SegmentCount; index++)
            {
                var previous = particle.Centerline[Math.Max(0, index - 1)];
                var next = particle.Centerline[Math.Min(SegmentCount - 1, index + 1)];
                var dx = next.X - previous.X;
                var dy = next.Y - previous.Y;
                var magnitude = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy));
                var progress = index / (double)(SegmentCount - 1);
                var taper = Math.Min(1, 0.10 + progress * 3.2);
                if (progress > 0.90) taper *= 1 - (progress - 0.90) * 0.55;
                var halfWidth = particle.Thickness * taper * 0.5 *
                    (0.96 + Math.Sin(progress * Math.PI * 7 + particle.Phase) * 0.04);
                var normalX = -dy / magnitude;
                var normalY = dx / magnitude;
                particle.LeftEdge[index] = new Point(
                    particle.Centerline[index].X + normalX * halfWidth,
                    particle.Centerline[index].Y + normalY * halfWidth);
                particle.RightEdge[index] = new Point(
                    particle.Centerline[index].X - normalX * halfWidth,
                    particle.Centerline[index].Y - normalY * halfWidth);
            }
            particle.Figure.StartPoint = particle.LeftEdge[0];
            var bodyPointIndex = 0;
            for (var index = 1; index < SegmentCount; index++)
                particle.BodyPoints[bodyPointIndex++] = particle.LeftEdge[index];
            for (var index = SegmentCount - 1; index >= 0; index--)
                particle.BodyPoints[bodyPointIndex++] = particle.RightEdge[index];

            foreach (var mark in particle.ScaleMarks)
            {
                var centerIndex = Math.Min(SegmentCount - 2, mark.CenterIndex);
                var point = particle.Centerline[centerIndex];
                var next = particle.Centerline[centerIndex + 1];
                mark.Rotation.Angle = Math.Atan2(next.Y - point.Y, next.X - point.X) * 180 / Math.PI;
                Canvas.SetLeft(mark.Visual, point.X - mark.Visual.Width * 0.5);
                Canvas.SetTop(mark.Visual, point.Y - mark.Visual.Height * 0.5);
            }
            var headPoint = particle.Centerline[SegmentCount - 1];
            var previousPoint = particle.Centerline[SegmentCount - 2];
            var angle = Math.Atan2(headPoint.Y - previousPoint.Y, headPoint.X - previousPoint.X) * 180 / Math.PI;
            particle.HeadRotation.Angle = angle;
            Canvas.SetLeft(particle.Head, headPoint.X - particle.HeadWidth * 0.50);
            Canvas.SetTop(particle.Head, headPoint.Y - particle.HeadHeight * 0.50);
            var targetOpacity = particle.BaseOpacity * safeMultiplier;
            particle.Visual.Opacity += (targetOpacity - particle.Visual.Opacity) * Math.Min(1, elapsed * 7);
        }

        private bool HasLeftBounds(SnakeParticle particle)
        {
            var span = particle.Horizontal ? context.Bounds.Width : context.Bounds.Height;
            var tail = particle.HeadDistance - particle.Direction * particle.Length;
            return particle.Direction > 0 ? tail > span + particle.HeadWidth * 2 : tail < -particle.HeadWidth * 2;
        }

        private void BuildStaticSnakes()
        {
            ClearParticles();
            for (var index = 0; index < 2; index++)
            {
                AddSnake(true);
                var particle = particles.LastOrDefault();
                if (particle == null) continue;
                particle.Horizontal = index == 0;
                particle.Direction = index == 0 ? 1 : -1;
                particle.BaseCross = index == 0
                    ? Math.Max(24, context.Bounds.Height - 72)
                    : Math.Max(24, context.Bounds.Width - 58);
                particle.HeadDistance = index == 0 ? context.Bounds.Width * 0.34 : context.Bounds.Height * 0.68;
                UpdateSnakeVisual(particle, 0);
                particle.Visual.Opacity = Math.Min(0.18, particle.BaseOpacity);
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

        private sealed class SnakeParticle
        {
            public Canvas Visual;
            public PathFigure Figure;
            public PointCollection BodyPoints;
            public Point[] Centerline;
            public Point[] LeftEdge;
            public Point[] RightEdge;
            public List<ScaleMark> ScaleMarks;
            public Canvas Head;
            public RotateTransform HeadRotation;
            public double HeadWidth;
            public double HeadHeight;
            public double Thickness;
            public bool Horizontal;
            public double Direction;
            public double Length;
            public double HeadDistance;
            public double BaseCross;
            public double Amplitude;
            public double Wavelength;
            public double Phase;
            public double SpeedPhase;
            public double WaveSpeed;
            public double Speed;
            public double BaseOpacity;
        }

        private sealed class ScaleMark
        {
            public Polyline Visual;
            public RotateTransform Rotation;
            public int CenterIndex;
        }
    }
}
