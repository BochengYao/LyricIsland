using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LyricHover.Core
{
    public sealed class SupporterBadgeIdentity
    {
        public string DisplayName { get; set; }

        public DateTimeOffset AcquiredDate { get; set; }
    }

    /// <summary>
    /// Stores the locally chosen badge engraving exactly once. The Store-acquired
    /// date is supplied by the entitlement pipeline and is never user editable.
    /// </summary>
    public sealed class SupporterBadgeIdentityStore
    {
        public const int MinimumDisplayNameLength = 2;
        public const int MaximumDisplayNameLength = 18;

        private readonly string path;

        public SupporterBadgeIdentityStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("An identity path is required.", nameof(path));
            }

            this.path = Path.GetFullPath(path);
        }

        public SupporterBadgeIdentity Load()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var identity = JsonSerializer.Deserialize<SupporterBadgeIdentity>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return IsValid(identity) ? Normalize(identity) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public SupporterBadgeIdentity Commit(string displayName, DateTimeOffset acquiredDate)
        {
            var identity = new SupporterBadgeIdentity
            {
                DisplayName = SanitizeDisplayName(displayName),
                AcquiredDate = acquiredDate.ToUniversalTime()
            };
            if (!IsValid(identity))
            {
                throw new ArgumentException(
                    $"The badge engraving must contain {MinimumDisplayNameLength}-{MaximumDisplayNameLength} supported characters.",
                    nameof(displayName));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var content = JsonSerializer.Serialize(
                identity,
                new JsonSerializerOptions { WriteIndented = true });
            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(content);
                }
            }
            catch (IOException) when (File.Exists(path))
            {
                throw new InvalidOperationException("The supporter badge engraving has already been committed.");
            }

            return identity;
        }

        public static string SanitizeDisplayName(string displayName)
        {
            var characters = (displayName ?? string.Empty)
                .Where(character =>
                    char.IsLetterOrDigit(character) ||
                    character == ' ' ||
                    character == '-' ||
                    character == '_')
                .ToArray();
            var normalized = string.Join(
                " ",
                new string(characters)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length > MaximumDisplayNameLength
                ? normalized.Substring(0, MaximumDisplayNameLength).TrimEnd()
                : normalized;
        }

        private static bool IsValid(SupporterBadgeIdentity identity)
        {
            return identity != null &&
                   identity.AcquiredDate != default &&
                   !string.IsNullOrWhiteSpace(identity.DisplayName) &&
                   identity.DisplayName.Length >= MinimumDisplayNameLength &&
                   identity.DisplayName.Length <= MaximumDisplayNameLength &&
                   string.Equals(
                       identity.DisplayName,
                       SanitizeDisplayName(identity.DisplayName),
                       StringComparison.Ordinal);
        }

        private static SupporterBadgeIdentity Normalize(SupporterBadgeIdentity identity)
        {
            return new SupporterBadgeIdentity
            {
                DisplayName = SanitizeDisplayName(identity.DisplayName),
                AcquiredDate = identity.AcquiredDate.ToUniversalTime()
            };
        }
    }
}
