using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyricHover.App.Fullscreen
{
    /// <summary>
    /// Draws the album palette into one small reusable bitmap. WPF only has to
    /// composite a single upscaled surface, leaving the full-resolution render
    /// budget to the lyric text.
    /// </summary>
    internal sealed class LowResolutionAmbientBackgroundControl : Grid, IDisposable
    {
        private const int BlobCount = 5;
        private static readonly Point[] BlobOrigins =
        {
            new Point(-0.08, -0.06), new Point(0.82, -0.08),
            new Point(-0.10, 0.78), new Point(0.84, 0.76),
            new Point(0.46, 0.42)
        };
        private static readonly double[] BaseRadiiX = { 0.66, 0.58, 0.70, 0.60, 0.78 };
        private static readonly double[] BaseRadiiY = { 0.58, 0.68, 0.62, 0.72, 0.56 };

        private readonly Image surface;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Color[] fromAccents = new Color[BlobCount];
        private readonly Color[] targetAccents = new Color[BlobCount];
        private readonly Color[] frameAccents = new Color[BlobCount];
        private readonly double[] centersX = new double[BlobCount];
        private readonly double[] centersY = new double[BlobCount];
        private readonly double[] radiiX = new double[BlobCount];
        private readonly double[] radiiY = new double[BlobCount];
        private readonly double[] rotationCos = new double[BlobCount];
        private readonly double[] rotationSin = new double[BlobCount];
        private WriteableBitmap bitmap;
        private byte[] pixels = Array.Empty<byte>();
        private double[] rowWarp = Array.Empty<double>();
        private double[] columnWarp = Array.Empty<double>();
        private int pixelWidth;
        private int pixelHeight;
        private TimeSpan lastFrame;
        private TimeSpan transitionStarted;
        private TimeSpan transitionDuration = TimeSpan.Zero;
        private Color fromBase;
        private Color targetBase;
        private bool transitionActive;
        private bool rendering;
        private bool isPlaying;
        private AudioEnergySnapshot audio = new AudioEnergySnapshot();
        private TimeSpan lastAudioEnvelopeTime;
        private double smoothedBass;
        private double smoothedLoudness;
        private double smoothedTransient;
        private OverlayPlacementSettings settings = new OverlayPlacementSettings();

        public LowResolutionAmbientBackgroundControl()
        {
            ClipToBounds = true;
            Background = new SolidColorBrush(AlbumThemePalette.Default.BaseColor);
            surface = new Image
            {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(surface, BitmapScalingMode.HighQuality);
            Children.Add(surface);

            // This overlay darkens the reading side without applying an opacity mask
            // to the moving lyric subtree.
            Children.Add(new Border
            {
                IsHitTestVisible = false,
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(12, 0, 0, 0), 0),
                        new GradientStop(Color.FromArgb(72, 0, 0, 0), 0.66),
                        new GradientStop(Color.FromArgb(132, 0, 0, 0), 1)
                    },
                    new Point(0, 0), new Point(1, 0))
            });

            SetPaletteImmediately(AlbumThemePalette.Default);
            Loaded += OnLoaded;
            Unloaded += (sender, args) => Stop();
            SizeChanged += (sender, args) =>
            {
                EnsureSurface();
                RenderFrame(clock.Elapsed);
            };
        }

        public void Configure(OverlayPlacementSettings value)
        {
            settings = (value ?? new OverlayPlacementSettings()).DeepClone();
            if (!IsLoaded) return;
            EnsureSurface();
            RenderFrame(clock.Elapsed);
            if (ShouldContinuouslyAnimate() || transitionActive) Start();
            else Stop();
        }

        public void SetPlayback(bool playing) => isPlaying = playing;
        public void SetAudio(AudioEnergySnapshot value) => audio = value ?? new AudioEnergySnapshot();

        public void TransitionTo(AlbumThemePalette palette)
        {
            palette = palette ?? AlbumThemePalette.Default;
            var now = clock.Elapsed;
            CaptureCurrentPalette(now);
            targetBase = palette.BaseColor;
            for (var index = 0; index < BlobCount; index++)
                targetAccents[index] = ReadAccent(palette, index);
            transitionStarted = now;
            transitionDuration = TimeSpan.FromMilliseconds(settings.FullscreenReduceMotion ? 120 : 1500);
            transitionActive = true;
            if (IsLoaded) Start();
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            EnsureSurface();
            RenderFrame(clock.Elapsed);
            if (ShouldContinuouslyAnimate() || transitionActive) Start();
        }

        private bool ShouldContinuouslyAnimate()
        {
            return !settings.FullscreenReduceMotion &&
                settings.FullscreenMotionMode != FullscreenMotionMode.Static &&
                settings.FullscreenRenderQuality != FullscreenRenderQuality.Static;
        }

        private void EnsureSurface()
        {
            if (ActualWidth <= 1 || ActualHeight <= 1) return;
            // About one tenth of the logical surface, bounded for 4K/mixed-DPI use.
            var width = Math.Max(128, Math.Min(320, (int)Math.Ceiling(ActualWidth / 10.0)));
            var height = Math.Max(80, Math.Min(200, (int)Math.Ceiling(ActualHeight / 10.0)));
            if (bitmap != null && width == pixelWidth && height == pixelHeight) return;
            pixelWidth = width;
            pixelHeight = height;
            pixels = new byte[pixelWidth * pixelHeight * 4];
            rowWarp = new double[pixelHeight];
            columnWarp = new double[pixelWidth];
            bitmap = new WriteableBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Bgra32, null);
            surface.Source = bitmap;
        }

        private void Start()
        {
            if (rendering) return;
            rendering = true;
            lastFrame = TimeSpan.Zero;
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
            RenderFrame(now);
            if (!transitionActive && !ShouldContinuouslyAnimate()) Stop();
        }

        private void RenderFrame(TimeSpan now)
        {
            EnsureSurface();
            if (bitmap == null || pixels.Length == 0) return;

            var paletteProgress = GetTransitionProgress(now);
            var baseColor = Blend(fromBase, targetBase, paletteProgress);
            for (var index = 0; index < BlobCount; index++)
                frameAccents[index] = Blend(fromAccents[index], targetAccents[index], paletteProgress);

            if (transitionActive && paletteProgress >= 1)
            {
                transitionActive = false;
                fromBase = targetBase;
                Array.Copy(targetAccents, fromAccents, BlobCount);
            }

            var moving = ShouldContinuouslyAnimate();
            var time = moving ? now.TotalSeconds : 0;
            var activeAudio = moving && settings.FullscreenMotionMode == FullscreenMotionMode.AudioReactive && audio.IsAvailable;
            var intensity = settings.FullscreenMotionIntensity == FullscreenMotionIntensity.Low ? 0.65 :
                settings.FullscreenMotionIntensity == FullscreenMotionIntensity.High ? 1.35 : 1.0;
            var pauseFactor = isPlaying ? 1.0 : 0.18;
            UpdateAudioEnvelope(now, activeAudio, pauseFactor);
            var bass = smoothedBass * 0.045 * intensity;
            // Loudness changes the overall level only slightly. Bass and transients are
            // expressed primarily as spatial motion below, avoiding a hard full-frame
            // flash while keeping the audio-reactive mode visibly alive.
            var brightness = smoothedLoudness * 0.018 * intensity;
            var audioDrift = CalculateAudioDrift(
                activeAudio, smoothedBass, smoothedTransient, intensity);
            var audioPhase = activeAudio
                ? (smoothedLoudness * 0.64 + smoothedTransient * 0.38) * intensity
                : 0;

            // The mesh moves through a gently warped coordinate field. The warp is
            // calculated once per row/column, not once per blob/pixel, keeping the
            // 60fps path inexpensive while avoiding recognizable oval boundaries.
            var ambientMotion = !moving ? 0 :
                settings.FullscreenMotionIntensity == FullscreenMotionIntensity.Low ? 0.88 :
                settings.FullscreenMotionIntensity == FullscreenMotionIntensity.High ? 1.34 : 1.12;
            for (var y = 0; y < pixelHeight; y++)
            {
                var normalizedY = (y + 0.5) / pixelHeight;
                rowWarp[y] = ambientMotion * (
                    Math.Sin(normalizedY * 5.4 + time * 0.285) * 0.040 +
                    Math.Sin(normalizedY * 2.2 - time * 0.172) * 0.020);
            }
            for (var x = 0; x < pixelWidth; x++)
            {
                var normalizedX = (x + 0.5) / pixelWidth;
                columnWarp[x] = ambientMotion * (
                    Math.Sin(normalizedX * 5.0 - time * 0.262) * 0.038 +
                    Math.Sin(normalizedX * 2.4 + time * 0.164) * 0.019);
            }

            for (var index = 0; index < BlobCount; index++)
            {
                var phase = index * 1.47;
                var speed = 0.178 + index * 0.018;
                centersX[index] = BlobOrigins[index].X + ambientMotion * (
                    Math.Sin(time * speed + phase + audioPhase * (index % 2 == 0 ? 1 : -0.82)) * 0.220 +
                    Math.Sin(time * speed * 0.47 + phase * 0.63) * 0.078) +
                    Math.Sin(phase * 1.31 + smoothedTransient * 1.8) * audioDrift;
                centersY[index] = BlobOrigins[index].Y + ambientMotion * (
                    Math.Cos(time * speed * 0.83 + phase + audioPhase * (index % 2 == 0 ? -0.76 : 0.94)) * 0.194 +
                    Math.Sin(time * speed * 0.41 + phase * 0.78) * 0.070) +
                    Math.Cos(phase * 1.17 + smoothedBass * 1.5) * audioDrift * 0.86;
                var shapeX = 1 + ambientMotion * Math.Sin(time * 0.126 + phase) * 0.092 + bass;
                var shapeY = 1 + ambientMotion * Math.Cos(time * 0.112 + phase) * 0.086 + bass * 0.82;
                radiiX[index] = BaseRadiiX[index] * shapeX;
                radiiY[index] = BaseRadiiY[index] * shapeY;
                var angle = phase * 0.31 + ambientMotion * (
                    Math.Sin(time * 0.074 + phase) * 0.58 +
                    time * (index % 2 == 0 ? 0.012 : -0.010));
                rotationCos[index] = Math.Cos(angle);
                rotationSin[index] = Math.Sin(angle);
            }

            var offset = 0;
            for (var y = 0; y < pixelHeight; y++)
            {
                var normalizedY = (y + 0.5) / pixelHeight;
                for (var x = 0; x < pixelWidth; x++)
                {
                    var normalizedX = (x + 0.5) / pixelWidth;
                    var warpedX = normalizedX + rowWarp[y];
                    var warpedY = normalizedY + columnWarp[x];
                    const double baseWeight = 0.68;
                    var totalWeight = baseWeight;
                    var red = baseColor.R * baseWeight;
                    var green = baseColor.G * baseWeight;
                    var blue = baseColor.B * baseWeight;
                    for (var index = 0; index < BlobCount; index++)
                    {
                        var dx = warpedX - centersX[index];
                        var dy = warpedY - centersY[index];
                        var rotatedX = dx * rotationCos[index] + dy * rotationSin[index];
                        var rotatedY = -dx * rotationSin[index] + dy * rotationCos[index];
                        var nx = rotatedX / radiiX[index];
                        var ny = rotatedY / radiiY[index];
                        var distanceSquared = nx * nx + ny * ny;
                        // An unbounded rational falloff has no ellipse edge to reveal
                        // during motion. Normalized weighted mixing also removes the
                        // order-dependent colour planes produced by repeated lerps.
                        var denominator = 1 + distanceSquared * 2.65;
                        var weight = 0.92 / (denominator * denominator);
                        var accent = frameAccents[index];
                        red += accent.R * weight;
                        green += accent.G * weight;
                        blue += accent.B * weight;
                        totalWeight += weight;
                    }

                    var edgeX = Math.Abs(normalizedX - 0.5) * 2;
                    var edgeY = Math.Abs(normalizedY - 0.5) * 2;
                    var vignette = 1 - Math.Max(edgeX * edgeX, edgeY * edgeY) * 0.075;
                    var light = (1 + brightness) * vignette;
                    pixels[offset++] = ClampByte(blue / totalWeight * light);
                    pixels[offset++] = ClampByte(green / totalWeight * light);
                    pixels[offset++] = ClampByte(red / totalWeight * light);
                    pixels[offset++] = 255;
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, pixelWidth, pixelHeight), pixels, pixelWidth * 4, 0);
        }

        private void UpdateAudioEnvelope(TimeSpan now, bool activeAudio, double pauseFactor)
        {
            var elapsed = lastAudioEnvelopeTime == TimeSpan.Zero
                ? 1.0 / 60.0
                : Math.Max(1.0 / 240.0, Math.Min(0.10, (now - lastAudioEnvelopeTime).TotalSeconds));
            lastAudioEnvelopeTime = now;
            var targetBass = activeAudio ? audio.Bass * pauseFactor : 0;
            var targetLoudness = activeAudio ? audio.Loudness * pauseFactor : 0;
            var targetTransient = activeAudio ? audio.Transient * pauseFactor : 0;
            smoothedBass = AdvanceEnvelope(smoothedBass, targetBass, 10.0, 2.2, elapsed);
            smoothedLoudness = AdvanceEnvelope(smoothedLoudness, targetLoudness, 8.0, 1.8, elapsed);
            smoothedTransient = AdvanceEnvelope(smoothedTransient, targetTransient, 12.0, 3.4, elapsed);
        }

        private static double AdvanceEnvelope(double current, double target, double attack, double release, double elapsedSeconds)
        {
            target = Math.Max(0, Math.Min(1, target));
            var rate = target > current ? attack : release;
            var amount = 1 - Math.Exp(-rate * Math.Max(0, elapsedSeconds));
            return current + (target - current) * amount;
        }

        private static double CalculateAudioDrift(
            bool activeAudio,
            double bass,
            double transient,
            double intensity)
        {
            if (!activeAudio) return 0;
            bass = Math.Max(0, Math.Min(1, bass));
            transient = Math.Max(0, Math.Min(1, transient));
            intensity = Math.Max(0, intensity);
            return (bass * 0.095 + transient * 0.065) * intensity;
        }

        private void CaptureCurrentPalette(TimeSpan now)
        {
            var progress = GetTransitionProgress(now);
            fromBase = Blend(fromBase, targetBase, progress);
            for (var index = 0; index < BlobCount; index++)
                fromAccents[index] = Blend(fromAccents[index], targetAccents[index], progress);
        }

        private double GetTransitionProgress(TimeSpan now)
        {
            if (!transitionActive || transitionDuration <= TimeSpan.Zero) return 1;
            var linear = Math.Max(0, Math.Min(1,
                (now - transitionStarted).TotalMilliseconds / transitionDuration.TotalMilliseconds));
            return linear * linear * (3 - 2 * linear);
        }

        private void SetPaletteImmediately(AlbumThemePalette palette)
        {
            fromBase = targetBase = palette.BaseColor;
            for (var index = 0; index < BlobCount; index++)
                fromAccents[index] = targetAccents[index] = ReadAccent(palette, index);
        }

        private static Color ReadAccent(AlbumThemePalette palette, int index)
        {
            return palette.AccentColors == null || palette.AccentColors.Count == 0
                ? palette.BaseColor
                : palette.AccentColors[index % palette.AccentColors.Count];
        }

        private static Color Blend(Color from, Color to, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromRgb(
                ClampByte(from.R + (to.R - from.R) * amount),
                ClampByte(from.G + (to.G - from.G) * amount),
                ClampByte(from.B + (to.B - from.B) * amount));
        }

        private static byte ClampByte(double value) =>
            (byte)Math.Max(0, Math.Min(255, Math.Round(value)));

        public void Dispose() => Stop();
    }
}
