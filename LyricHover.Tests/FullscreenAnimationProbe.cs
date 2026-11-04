using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LyricHover.App;
using LyricHover.App.Fullscreen;
using LyricHover.Core;
using LyricHover.Core.Media;

namespace LyricHover.Tests
{
    internal static class FullscreenAnimationProbe
    {
        private static readonly byte[] ProbeArtwork = BuildProbeArtwork();

        public static int Run()
        {
            var settings = new OverlayPlacementSettings
            {
                FullscreenMotionMode = FullscreenMotionMode.Ambient,
                FullscreenRenderQuality = FullscreenRenderQuality.High60Fps,
                FullscreenReduceMotion = false
            };
            settings.Normalize();

            var lyrics = BuildLyrics();
            var playingSession = BuildSession(MediaPlaybackStatus.Playing);
            var pausedSession = BuildSession(MediaPlaybackStatus.Paused);
            var window = new FullscreenLyricsWindow(settings)
            {
                WindowState = WindowState.Normal,
                Width = 1280,
                Height = 800,
                Left = 48,
                Top = 48,
                ShowInTaskbar = false
            };

            var runtime = Stopwatch.StartNew();
            var renderSamples = new List<double>(360);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            var stablePrepared = false;
            var stableCaptured = false;
            var transitionStarted = false;
            var transitionCaptured = false;
            var settledCaptured = false;
            var closed = false;
            var exitCode = 1;
            var outputDirectory = Path.Combine(GetSolutionRoot(), "output", "fullscreen-animation-probe");
            Directory.CreateDirectory(outputDirectory);

            EventHandler rendering = (sender, args) =>
            {
                var elapsed = runtime.Elapsed.TotalSeconds;
                if (elapsed >= 0.75 && elapsed <= 4.65) renderSamples.Add(elapsed);
            };
            CompositionTarget.Rendering += rendering;

            window.Closed += (sender, args) =>
            {
                if (closed) return;
                closed = true;
                CompositionTarget.Rendering -= rendering;
                timer.Stop();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            };

            timer.Tick += (sender, args) =>
            {
                var elapsed = runtime.Elapsed.TotalSeconds;
                if (elapsed < 4.70)
                {
                    var playbackPosition = TimeSpan.FromSeconds(Math.Max(0, elapsed - 0.30));
                    window.Present(CreatePresentation(playingSession, lyrics, playbackPosition));
                    return;
                }

                if (!stablePrepared)
                {
                    stablePrepared = true;
                    window.Present(CreatePresentation(pausedSession, lyrics, TimeSpan.FromSeconds(2.82)));
                    return;
                }

                if (!stableCaptured && elapsed >= 5.00)
                {
                    stableCaptured = true;
                    Capture(window, Path.Combine(outputDirectory, "01-before.png"));
                    return;
                }

                if (!transitionStarted && elapsed >= 5.10)
                {
                    transitionStarted = true;
                    // Jump to the next lyric to exercise scale, opacity and centered
                    // scroll together without playback-clock uncertainty.
                    window.Present(CreatePresentation(pausedSession, lyrics, TimeSpan.FromSeconds(3.50)));
                    return;
                }

                if (transitionStarted && !transitionCaptured && elapsed >= 5.17)
                {
                    transitionCaptured = true;
                    Capture(window, Path.Combine(outputDirectory, "02-transition-70ms.png"));
                    return;
                }

                if (transitionStarted && !settledCaptured && elapsed >= 5.42)
                {
                    settledCaptured = true;
                    Capture(window, Path.Combine(outputDirectory, "03-settled.png"));
                    CaptureBackgroundAt(window, TimeSpan.FromSeconds(18),
                        Path.Combine(outputDirectory, "04-background-flow-18s.png"));
                    var metrics = Analyze(renderSamples);
                    Console.WriteLine(
                        "ANIMATION_PROBE frames={0} median_ms={1:F2} p95_ms={2:F2} max_ms={3:F2} jank_frames={4}",
                        metrics.FrameCount, metrics.MedianMilliseconds, metrics.P95Milliseconds,
                        metrics.MaxMilliseconds, metrics.JankFrames);
                    Console.WriteLine("ANIMATION_PROBE_OUTPUT " + outputDirectory);
                    exitCode = metrics.FrameCount >= 100 && metrics.P95Milliseconds <= 40 &&
                        metrics.JankFrames <= Math.Max(3, metrics.FrameCount / 20) ? 0 : 1;
                    timer.Stop();
                    window.Close();
                }
            };

            window.Present(CreatePresentation(playingSession, lyrics, TimeSpan.Zero));
            window.Show();
            timer.Start();
            Dispatcher.Run();
            window.Dispose();
            return exitCode;
        }

        private static TimedLyrics BuildLyrics()
        {
            var lines = new List<LyricLine>();
            for (var index = 0; index < 18; index++)
            {
                var timestamp = TimeSpan.FromMilliseconds(index * 700);
                var text = index % 3 == 0
                    ? "Smooth lyrics 混合文字第 " + index + " 行"
                    : index % 3 == 1
                        ? "动画必须保持清晰并且持续居中 " + index
                        : "No duplicate glyph layer during scale " + index;
                var words = BuildWords(text);
                lines.Add(new LyricLine(timestamp, text, words));
            }
            return new TimedLyrics(lines, "animation-probe", "LyricHover");
        }

        private static IReadOnlyList<LyricWord> BuildWords(string text)
        {
            var tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var words = new List<LyricWord>(tokens.Length);
            for (var index = 0; index < tokens.Length; index++)
                words.Add(new LyricWord(TimeSpan.FromMilliseconds(index * 120), TimeSpan.FromMilliseconds(150), tokens[index]));
            return words;
        }

        private static MediaSessionSnapshot BuildSession(MediaPlaybackStatus status)
        {
            return new MediaSessionSnapshot
            {
                SessionId = "fullscreen-animation-probe",
                Title = "Smooth Animation Probe",
                Artist = "LyricHover",
                Album = "Local Verification",
                Duration = TimeSpan.FromSeconds(13),
                PlaybackStatus = status,
                ArtworkBytes = ProbeArtwork
            };
        }

        private static byte[] BuildProbeArtwork()
        {
            using (var bitmap = new System.Drawing.Bitmap(512, 512))
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            using (var stream = new MemoryStream())
            {
                graphics.Clear(System.Drawing.Color.FromArgb(18, 22, 50));
                using (var magenta = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(178, 28, 112)))
                using (var orange = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 105, 42)))
                using (var blue = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(42, 88, 184)))
                using (var violet = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(92, 42, 142)))
                {
                    graphics.FillEllipse(magenta, -100, -80, 430, 430);
                    graphics.FillEllipse(orange, 250, -60, 360, 360);
                    graphics.FillEllipse(blue, -80, 260, 370, 370);
                    graphics.FillEllipse(violet, 220, 220, 390, 390);
                }
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static FullscreenLyricsPresentation CreatePresentation(
            MediaSessionSnapshot session,
            TimedLyrics lyrics,
            TimeSpan position)
        {
            return new FullscreenLyricsPresentation
            {
                Session = session,
                Lyrics = lyrics,
                Position = position,
                LyricsSearchFinished = true
            };
        }

        private static void Capture(FrameworkElement element, string path)
        {
            element.UpdateLayout();
            var width = Math.Max(1, (int)Math.Round(element.ActualWidth));
            var height = Math.Max(1, (int)Math.Round(element.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }

        private static void CaptureBackgroundAt(FullscreenLyricsWindow window, TimeSpan time, string path)
        {
            var background = (FrameworkElement)window.FindName("AmbientBackground");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            background.GetType().GetMethod("Stop", flags)?.Invoke(background, null);
            background.GetType().GetMethod("RenderFrame", flags)?.Invoke(background, new object[] { time });
            Capture(window, path);
        }

        private static ProbeMetrics Analyze(IReadOnlyList<double> samples)
        {
            var intervals = new List<double>(Math.Max(0, samples.Count - 1));
            for (var index = 1; index < samples.Count; index++)
                intervals.Add((samples[index] - samples[index - 1]) * 1000);
            intervals.Sort();
            if (intervals.Count == 0) return new ProbeMetrics();
            var median = Percentile(intervals, 0.50);
            var p95 = Percentile(intervals, 0.95);
            var threshold = Math.Max(40, median * 2.5);
            return new ProbeMetrics
            {
                FrameCount = intervals.Count,
                MedianMilliseconds = median,
                P95Milliseconds = p95,
                MaxMilliseconds = intervals[intervals.Count - 1],
                JankFrames = intervals.Count(value => value > threshold)
            };
        }

        private static double Percentile(IReadOnlyList<double> sorted, double percentile)
        {
            var index = (int)Math.Round((sorted.Count - 1) * percentile);
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
        }

        private static string GetSolutionRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LyricHover.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? Directory.GetCurrentDirectory();
        }

        private sealed class ProbeMetrics
        {
            public int FrameCount { get; set; }
            public double MedianMilliseconds { get; set; }
            public double P95Milliseconds { get; set; }
            public double MaxMilliseconds { get; set; }
            public int JankFrames { get; set; }
        }
    }
}
