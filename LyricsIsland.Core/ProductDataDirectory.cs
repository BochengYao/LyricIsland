using System;
using System.IO;

namespace LyricsIsland.Core
{
    public static class ProductDataDirectory
    {
        public const string DirectoryName = "LyricsIsland";

        public static string Prepare(string localApplicationDataRoot)
        {
            if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
            {
                throw new ArgumentException("A local application data directory is required.", nameof(localApplicationDataRoot));
            }

            var currentRoot = Path.Combine(localApplicationDataRoot, DirectoryName);
            var legacyRoot = Path.Combine(
                localApplicationDataRoot,
                string.Concat("AppleMusic", "DesktopLyrics"));

            try
            {
                if (Directory.Exists(legacyRoot) && !Directory.Exists(currentRoot))
                {
                    Directory.Move(legacyRoot, currentRoot);
                    return currentRoot;
                }

                Directory.CreateDirectory(currentRoot);
                if (Directory.Exists(legacyRoot))
                {
                    CopyMissingFiles(legacyRoot, currentRoot);
                }
            }
            catch (IOException)
            {
                Directory.CreateDirectory(currentRoot);
                CopyMissingFilesBestEffort(legacyRoot, currentRoot);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.CreateDirectory(currentRoot);
                CopyMissingFilesBestEffort(legacyRoot, currentRoot);
            }

            return currentRoot;
        }

        private static void CopyMissingFilesBestEffort(string sourceRoot, string destinationRoot)
        {
            try
            {
                if (Directory.Exists(sourceRoot))
                {
                    CopyMissingFiles(sourceRoot, destinationRoot);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void CopyMissingFiles(string sourceRoot, string destinationRoot)
        {
            foreach (var sourceDirectory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, sourceDirectory);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
            }

            foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
                var destinationFile = Path.Combine(destinationRoot, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                if (!File.Exists(destinationFile))
                {
                    File.Copy(sourceFile, destinationFile);
                }
            }
        }
    }
}
