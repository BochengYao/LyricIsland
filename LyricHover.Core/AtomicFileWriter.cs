using System;
using System.IO;

namespace LyricHover.Core
{
    public static class AtomicFileWriter
    {
        public static void WriteAllText(string path, string contents)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A destination path is required.", nameof(path));
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(
                directory,
                Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllText(temporaryPath, contents ?? string.Empty);
                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
