using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AppleMusicDesktopLyrics.App
{
    public sealed class AppleMusicOcrLyricsReader
    {
        private const int MaxRecognizedLines = 3;
        private readonly string tesseractPath;

        public AppleMusicOcrLyricsReader()
            : this(FindTesseractPath())
        {
        }

        public AppleMusicOcrLyricsReader(string tesseractPath)
        {
            this.tesseractPath = tesseractPath ?? string.Empty;
        }

        public bool IsAvailable => File.Exists(tesseractPath);

        public Task<string> TryReadCurrentLyricAsync()
        {
            return Task.Run(() => TryReadCurrentLyric());
        }

        private string TryReadCurrentLyric()
        {
            if (!IsAvailable)
            {
                return string.Empty;
            }

            var window = FindAppleMusicWindow();
            if (window == IntPtr.Zero || !GetWindowRect(window, out var rect))
            {
                return string.Empty;
            }

            var windowWidth = rect.Right - rect.Left;
            var windowHeight = rect.Bottom - rect.Top;
            if (windowWidth < 640 || windowHeight < 360)
            {
                return string.Empty;
            }

            var crop = new Rectangle(
                rect.Left + (int)Math.Round(windowWidth * 0.72),
                rect.Top + (int)Math.Round(windowHeight * 0.03),
                Math.Max(220, (int)Math.Round(windowWidth * 0.27)),
                Math.Max(180, (int)Math.Round(windowHeight * 0.62)));

            using (var captured = CaptureScreen(crop))
            using (var processed = BuildHighContrastWhiteTextImage(captured))
            {
                return Recognize(processed);
            }
        }

        private static Bitmap CaptureScreen(Rectangle bounds)
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }

        private static Bitmap BuildHighContrastWhiteTextImage(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    var isBrightText = pixel.R > 170 && pixel.G > 170 && pixel.B > 170;
                    result.SetPixel(x, y, isBrightText ? Color.White : Color.Black);
                }
            }

            return result;
        }

        private string Recognize(Bitmap bitmap)
        {
            var tempBase = Path.Combine(Path.GetTempPath(), "applemusic_lyrics_ocr_" + Guid.NewGuid().ToString("N"));
            var imagePath = tempBase + ".png";
            var textPath = tempBase + ".txt";

            try
            {
                bitmap.Save(imagePath, ImageFormat.Png);
                var startInfo = new ProcessStartInfo
                {
                    FileName = tesseractPath,
                    Arguments = "\"" + imagePath + "\" \"" + tempBase + "\" --psm 6 -l eng",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    if (!process.WaitForExit(2500))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        return string.Empty;
                    }
                }

                if (!File.Exists(textPath))
                {
                    return string.Empty;
                }

                return CleanRecognizedText(File.ReadAllText(textPath));
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                TryDelete(imagePath);
                TryDelete(textPath);
            }
        }

        private static string CleanRecognizedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lines = text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Where(line => !LooksLikeOverlayNoise(line))
                .Take(MaxRecognizedLines)
                .ToList();

            return string.Join(" ", lines).Trim();
        }

        private static bool LooksLikeOverlayNoise(string line)
        {
            var noiseMarkers = new[]
            {
                "Codex",
                "插件",
                "不可用",
                "noderpl",
                "请求元数据",
                "电脑控制",
                "排查"
            };

            return noiseMarkers.Any(marker => line.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static IntPtr FindAppleMusicWindow()
        {
            return Process.GetProcessesByName("AppleMusic")
                .Select(process => process.MainWindowHandle)
                .FirstOrDefault(handle => handle != IntPtr.Zero);
        }

        private static string FindTesseractPath()
        {
            var candidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe")
            };

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            candidates.AddRange(path
                .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(folder => Path.Combine(folder, "tesseract.exe")));

            return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
