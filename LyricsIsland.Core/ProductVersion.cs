using System;
using System.Reflection;

namespace LyricsIsland.Core
{
    public static class ProductVersion
    {
        public static string DisplayVersion
        {
            get
            {
                var attribute = typeof(ProductVersion).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return FormatDisplayVersion(attribute?.InformationalVersion);
            }
        }

        public static string DisplayVersionNumber
        {
            get
            {
                var displayVersion = DisplayVersion;
                var separatorIndex = displayVersion.IndexOf(' ');
                return separatorIndex < 0
                    ? displayVersion
                    : displayVersion.Substring(0, separatorIndex);
            }
        }

        public static string DisplayVersionChannel
        {
            get
            {
                var displayVersion = DisplayVersion;
                var separatorIndex = displayVersion.IndexOf(' ');
                return separatorIndex < 0 || separatorIndex == displayVersion.Length - 1
                    ? string.Empty
                    : displayVersion.Substring(separatorIndex + 1);
            }
        }

        public static string FormatDisplayVersion(string informationalVersion)
        {
            if (string.IsNullOrWhiteSpace(informationalVersion))
            {
                return "v0.0.0";
            }

            var version = informationalVersion.Trim();
            var buildMetadataIndex = version.IndexOf('+');
            if (buildMetadataIndex >= 0)
            {
                version = version.Substring(0, buildMetadataIndex);
            }

            var suffixIndex = version.IndexOf('-');
            if (suffixIndex < 0)
            {
                return "v" + version;
            }

            var number = version.Substring(0, suffixIndex);
            var suffix = version.Substring(suffixIndex + 1);
            if (suffix.StartsWith("beta", StringComparison.OrdinalIgnoreCase))
            {
                suffix = "Beta" + suffix.Substring(4);
            }
            return string.IsNullOrWhiteSpace(suffix)
                ? "v" + number
                : "v" + number + " " + suffix;
        }
    }
}
