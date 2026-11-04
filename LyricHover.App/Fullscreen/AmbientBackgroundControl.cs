using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LyricHover.App.Fullscreen
{
    internal sealed class AmbientBackgroundControl : Grid, IDisposable
    {
        private readonly SolidColorBrush baseBrush = new SolidColorBrush(Color.FromRgb(13, 15, 22));
        private readonly List<Blob> blobs = new List<Blob>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private TimeSpan lastFrame;
        private bool rendering;
        private bool isPlaying;
        private AudioEnergySnapshot audio = new AudioEnergySnapshot();
        private OverlayPlacementSettings settings = new OverlayPlacementSettings();

        public AmbientBackgroundControl()
        {
            IsHitTestVisible = false;
            Background = baseBrush;
            ClipToBounds = true;
            // Oversized, edge-transparent radial fields overlap outside the viewport.
            // Unlike blurred solid ellipses, they have no geometric edge for WPF to
            // clip, so movement does not expose a visible oval or diagonal seam.
            var positions = new[]
            {
                new Point(-0.42, -0.48), new Point(0.24, -0.52),
                new Point(-0.36, 0.20), new Point(0.30, 0.18),
                new Point(-0.02, -0.08)
            };
            for (var i = 0; i < positions.Length; i++)
            {
                var color = AlbumThemePalette.Default.AccentColors[i % AlbumThemePalette.Default.AccentColors.Count];
                var center = new GradientStop(WithAlpha(color, 210), 0);
                var shoulder = new GradientStop(WithAlpha(color, 128), 0.42);
                var fade = new GradientStop(WithAlpha(color, 42), 0.73);
                var edge = new GradientStop(WithAlpha(color, 0), 1);
                var brush = new RadialGradientBrush(new GradientStopCollection { center, shoulder, fade, edge })
                {
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.48, 0.46),
                    RadiusX = 0.58,
                    RadiusY = 0.58
                };
                var transform = new TransformGroup();
                var scale = new ScaleTransform(1, 1, 0.5, 0.5);
                var rotate = new RotateTransform(0, 0.5, 0.5);
                var translate = new TranslateTransform();
                transform.Children.Add(scale); transform.Children.Add(rotate); transform.Children.Add(translate);
                var ellipse = new Ellipse
                {
                    Fill = brush,
                    Opacity = 0.72,
                    RenderTransform = transform,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                Children.Add(ellipse);
                blobs.Add(new Blob(ellipse, center, shoulder, fade, scale, rotate, translate, positions[i], i));
            }

            // A low-opacity full-frame veil blends intersections between fields and
            // suppresses gradient banding without blurring the entire visual tree.
            Children.Add(new Border
            {
                Background = new RadialGradientBrush(
                    Color.FromArgb(26, 255, 255, 255), Color.FromArgb(0, 255, 255, 255))
                {
                    Center = new Point(0.36, 0.32),
                    GradientOrigin = new Point(0.34, 0.30),
                    RadiusX = 0.92,
                    RadiusY = 0.92
                },
                Opacity = 0.22
            });

            Children.Add(new Border
            {
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(16, 0, 0, 0), 0),
                        new GradientStop(Color.FromArgb(92, 0, 0, 0), 0.70),
                        new GradientStop(Color.FromArgb(135, 0, 0, 0), 1)
                    },
                    new Point(0, 0), new Point(1, 0))
            });
            Loaded += (sender, args) => Start();
            Unloaded += (sender, args) => Stop();
            SizeChanged += (sender, args) => LayoutBlobs();
        }

        public void Configure(OverlayPlacementSettings value)
        {
            settings = (value ?? new OverlayPlacementSettings()).DeepClone();
            var staticQuality = settings.FullscreenRenderQuality == FullscreenRenderQuality.Static;
            if (settings.FullscreenReduceMotion || settings.FullscreenMotionMode == FullscreenMotionMode.Static || staticQuality) Stop();
            else if (IsLoaded) Start();
        }

        public void SetPlayback(bool playing) => isPlaying = playing;
        public void SetAudio(AudioEnergySnapshot value) => audio = value ?? new AudioEnergySnapshot();

        public void TransitionTo(AlbumThemePalette palette)
        {
            palette = palette ?? AlbumThemePalette.Default;
            var duration = new Duration(TimeSpan.FromMilliseconds(settings.FullscreenReduceMotion ? 120 : 1500));
            AnimateColor(baseBrush, palette.BaseColor, duration);
            for (var i = 0; i < blobs.Count; i++)
            {
                var color = palette.AccentColors[i % palette.AccentColors.Count];
                AnimateColor(blobs[i].Center, WithAlpha(color, 210), duration);
                AnimateColor(blobs[i].Shoulder, WithAlpha(color, 128), duration);
                AnimateColor(blobs[i].Fade, WithAlpha(color, 42), duration);
            }
        }

        private static void AnimateColor(SolidColorBrush brush, Color target, Duration duration)
        {
            var current = brush.Color;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = target;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(current, target, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private static void AnimateColor(GradientStop stop, Color target, Duration duration)
        {
            var current = stop.Color;
            stop.BeginAnimation(GradientStop.ColorProperty, null);
            stop.Color = target;
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(current, target, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

        private void LayoutBlobs()
        {
            var size = Math.Max(520, Math.Max(ActualWidth, ActualHeight) * 1.34);
            foreach (var blob in blobs)
            {
                blob.Element.Width = size * (blob.Index % 2 == 0 ? 1.12 : 0.98);
                blob.Element.Height = size * (blob.Index < 2 ? 0.98 : 1.10);
                blob.Element.Margin = new Thickness(blob.Origin.X * ActualWidth, blob.Origin.Y * ActualHeight, 0, 0);
            }
        }

        private void Start()
        {
            if (rendering) return;
            rendering = true;
            CompositionTarget.Rendering += OnRendering;
        }

        private void Stop()
        {
            if (!rendering) return;
            rendering = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs args)
        {
            var now = clock.Elapsed;
            var interval = settings.FullscreenRenderQuality == FullscreenRenderQuality.Balanced30Fps
                ? TimeSpan.FromMilliseconds(32) : TimeSpan.FromMilliseconds(15);
            if (now - lastFrame < interval) return;
            lastFrame = now;
            var t = now.TotalSeconds;
            var activeAudio = settings.FullscreenMotionMode == FullscreenMotionMode.AudioReactive && audio.IsAvailable;
            var intensity = settings.FullscreenMotionIntensity == FullscreenMotionIntensity.Low ? 0.65 :
                settings.FullscreenMotionIntensity == FullscreenMotionIntensity.High ? 1.35 : 1.0;
            var pauseFactor = isPlaying ? 1.0 : 0.18;
            var bassScale = activeAudio ? audio.Bass * 0.025 * intensity * pauseFactor : 0;
            var brightness = activeAudio ? audio.Loudness * 0.10 * intensity * pauseFactor : 0;
            var flash = activeAudio ? audio.Transient * 0.13 * intensity * pauseFactor : 0;
            for (var i = 0; i < blobs.Count; i++)
            {
                var blob = blobs[i];
                var phase = i * 1.47;
                var speed = 0.055 + i * 0.009;
                blob.Translate.X = Math.Sin(t * speed + phase) * ActualWidth * 0.075;
                blob.Translate.Y = Math.Cos(t * speed * 0.83 + phase) * ActualHeight * 0.065;
                blob.Rotate.Angle = Math.Sin(t * speed * 0.55 + phase) * 9;
                var breathing = 1 + Math.Sin(t * 0.12 + phase) * 0.025;
                blob.Scale.ScaleX = breathing + bassScale;
                blob.Scale.ScaleY = 1 + Math.Cos(t * 0.105 + phase) * 0.022 + bassScale * 0.82;
                blob.Element.Opacity = Math.Min(0.90, 0.68 + brightness + flash);
            }
        }

        public void Dispose() => Stop();

        private sealed class Blob
        {
            public Blob(Ellipse element, GradientStop center, GradientStop shoulder, GradientStop fade, ScaleTransform scale, RotateTransform rotate, TranslateTransform translate, Point origin, int index)
            { Element = element; Center = center; Shoulder = shoulder; Fade = fade; Scale = scale; Rotate = rotate; Translate = translate; Origin = origin; Index = index; }
            public Ellipse Element { get; }
            public GradientStop Center { get; }
            public GradientStop Shoulder { get; }
            public GradientStop Fade { get; }
            public ScaleTransform Scale { get; }
            public RotateTransform Rotate { get; }
            public TranslateTransform Translate { get; }
            public Point Origin { get; }
            public int Index { get; }
        }
    }
}
