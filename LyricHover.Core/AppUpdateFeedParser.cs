using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LyricHover.Core
{
    public sealed class AppUpdateRelease
    {
        public AppUpdateRelease(string versionLabel, Version version, IReadOnlyList<string> notes)
        {
            VersionLabel = versionLabel ?? string.Empty;
            Version = version ?? new Version(0, 0, 0, 0);
            Notes = notes ?? Array.Empty<string>();
        }

        public string VersionLabel { get; }
        public Version Version { get; }
        public IReadOnlyList<string> Notes { get; }
    }

    public static class AppUpdateFeedParser
    {
        private static readonly Regex ReleaseVersionPattern = new Regex(
            @"^[vV]?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?(?:[-+].*)?$",
            RegexOptions.CultureInvariant);

        public static bool TryGetAvailableUpdate(string json, Version currentVersion, string locale, out AppUpdateRelease release)
        {
            release = null;
            if (string.IsNullOrWhiteSpace(json) || currentVersion == null) return false;

            try
            {
                using (var document = JsonDocument.Parse(json))
                {
                    if (!TryGetEnabledUpdate(document.RootElement, out var update)) return false;
                    var label = ReadString(update, "release_version");
                    if (!TryParseReleaseVersion(label, out var version) || version <= currentVersion) return false;

                    var content = ReadLocalizedString(update, "content", LocaleSuffixes(locale));
                    var notes = content
                        .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim().TrimStart('-', '•', ' '))
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToArray();
                    if (notes.Length == 0) return false;

                    release = new AppUpdateRelease(label.Trim(), version, notes);
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParseReleaseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var match = ReleaseVersionPattern.Match(value.Trim());
            if (!match.Success ||
                !int.TryParse(match.Groups[1].Value, out var major) ||
                !int.TryParse(match.Groups[2].Value, out var minor) ||
                !int.TryParse(match.Groups[3].Value, out var patch)) return false;

            var revision = 0;
            if (match.Groups[4].Success && !int.TryParse(match.Groups[4].Value, out revision)) return false;
            try
            {
                version = new Version(major, minor, patch, revision);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool TryGetEnabledUpdate(JsonElement root, out JsonElement update)
        {
            update = default;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("update", out update) &&
                update.ValueKind == JsonValueKind.Object &&
                update.TryGetProperty("enabled", out var enabled) &&
                enabled.ValueKind == JsonValueKind.True;
        }

        private static string[] LocaleSuffixes(string locale)
        {
            if (string.Equals(locale, "zh-Hant", StringComparison.OrdinalIgnoreCase)) return new[] { "zh_tw", "zh", "en" };
            if (string.Equals(locale, "ja", StringComparison.OrdinalIgnoreCase)) return new[] { "ja", "en", "zh" };
            if (string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase)) return new[] { "en", "zh" };
            return new[] { "zh", "en" };
        }

        private static string ReadLocalizedString(JsonElement element, string prefix, IEnumerable<string> suffixes)
        {
            foreach (var suffix in suffixes)
            {
                var value = ReadString(element, prefix + "_" + suffix);
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return string.Empty;
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
    }
}
