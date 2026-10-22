using System;
using System.IO;
using System.Threading;

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
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                File.Replace(temporaryPath, fullPath, null, true);
                            }
                            catch (IOException)
                            {
                                // MSIX file redirection and other file users can reject
                                // ReplaceFile even when a same-directory rename works.
                                File.Move(temporaryPath, fullPath, true);
                            }
                        }
                        else
                        {
                            File.Move(temporaryPath, fullPath);
                        }

                        return;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(80 * (attempt + 1));
                    }
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
