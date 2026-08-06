using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LyricHover.Core
{
    public sealed class LyricsCache
    {
        public const int DefaultMaxMegabytes = 64;

        private readonly string rootDirectory;
        private long maxBytes;

        public LyricsCache(string rootDirectory)
            : this(rootDirectory, DefaultMaxMegabytes * 1024L * 1024L)
        {
        }

        public LyricsCache(string rootDirectory, long maxBytes)
        {
            this.rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
            SetMaxBytes(maxBytes);
        }

        public void SetMaxBytes(long value)
        {
            maxBytes = Math.Max(1L, value);
            PruneToLimit();
        }

        public string GetPath(TrackIdentity track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            var seconds = Math.Max(0, (int)Math.Round(track.Duration.TotalSeconds));
            var slug = Slugify(track.Artist + "-" + track.Title + "-" + seconds);
            return Path.Combine(rootDirectory, slug + ".lrc");
        }

        public bool TryRead(TrackIdentity track, out string lrc)
        {
            foreach (var path in GetReadCandidatePaths(track))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                lrc = File.ReadAllText(path, Encoding.UTF8);
                Touch(path);
                return true;
            }

            lrc = string.Empty;
            return false;
        }

        public void Write(TrackIdentity track, string lrc)
        {
            Directory.CreateDirectory(rootDirectory);
            var path = GetPath(track);
            File.WriteAllText(path, lrc ?? string.Empty, Encoding.UTF8);
            Touch(path);
            PruneToLimit();
        }

        private void PruneToLimit()
        {
            if (!Directory.Exists(rootDirectory))
            {
                return;
            }

            List<FileInfo> files;
            try
            {
                files = new DirectoryInfo(rootDirectory)
                    .GetFiles("*.lrc", SearchOption.TopDirectoryOnly)
                    .ToList();
            }
            catch
            {
                return;
            }

            var totalBytes = files.Sum(file => SafeLength(file));
            if (totalBytes <= maxBytes)
            {
                return;
            }

            foreach (var file in files.OrderBy(GetLastUseTimeUtc))
            {
                try
                {
                    var length = SafeLength(file);
                    file.Delete();
                    totalBytes -= length;
                }
                catch
                {
                }

                if (totalBytes <= maxBytes)
                {
                    break;
                }
            }
        }

        private static long SafeLength(FileInfo file)
        {
            try
            {
                file.Refresh();
                return file.Exists ? file.Length : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        private IEnumerable<string> GetReadCandidatePaths(TrackIdentity track)
        {
            yield return GetPath(track);

            var roundedSeconds = Math.Max(0, (int)Math.Round(track.Duration.TotalSeconds));
            foreach (var offset in new[] { -1, 1, -2, 2 })
            {
                var candidateSeconds = roundedSeconds + offset;
                if (candidateSeconds < 0)
                {
                    continue;
                }

                yield return GetPath(new TrackIdentity(
                    track.Title,
                    track.Artist,
                    TimeSpan.FromSeconds(candidateSeconds),
                    track.Album));
            }
        }

        private static void Touch(string path)
        {
            try
            {
                var now = DateTime.UtcNow;
                File.SetLastAccessTimeUtc(path, now);
                File.SetLastWriteTimeUtc(path, now);
            }
            catch
            {
            }
        }

        private static DateTime GetLastUseTimeUtc(FileInfo file)
        {
            try
            {
                file.Refresh();
                return file.Exists ? file.LastWriteTimeUtc : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string Slugify(string value)
        {
            var builder = new StringBuilder();
            var previousDash = false;

            foreach (var character in (value ?? string.Empty).ToLowerInvariant())
            {
                if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
                {
                    builder.Append(character);
                    previousDash = false;
                }
                else if (!previousDash)
                {
                    builder.Append('-');
                    previousDash = true;
                }
            }

            return builder.ToString().Trim('-');
        }
    }
}
