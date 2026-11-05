using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyricHover.Core;
using LyricHover.Core.Media;

namespace LyricHover.App.Fullscreen
{
    internal sealed class SongEffectCatalog
    {
        private static readonly char[] VersionSuffixStarts = { '(', '[', '{', '（', '【', '-', '–', '—' };
        private static readonly Regex ArtistSeparator = new Regex(
            @"\s*(?:,|;|&|/|、|\bx\b|\bfeat(?:uring)?\.?\b|\bft\.?\b)\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly IReadOnlyList<SongEffectDescriptor> entries;

        private SongEffectCatalog(IReadOnlyList<SongEffectDescriptor> entries)
        {
            this.entries = entries ?? Array.Empty<SongEffectDescriptor>();
        }

        public IReadOnlyList<SongEffectDescriptor> Entries => entries;

        public static SongEffectCatalog LoadDefault()
        {
            try
            {
                using (var stream = OpenEmbeddedResource("Assets.SongEffects.catalog.json"))
                {
                    if (stream == null) return new SongEffectCatalog(Array.Empty<SongEffectDescriptor>());
                    string json;
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                        json = reader.ReadToEnd();
                    var document = JsonSerializer.Deserialize<SongEffectCatalogDocument>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (document?.SchemaVersion != 1 || document.Entries == null)
                        return new SongEffectCatalog(Array.Empty<SongEffectDescriptor>());
                    var valid = document.Entries.Where(IsValid).ToList();
                    if (valid.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != valid.Count)
                        return new SongEffectCatalog(Array.Empty<SongEffectDescriptor>());
                    return new SongEffectCatalog(valid);
                }
            }
            catch
            {
                return new SongEffectCatalog(Array.Empty<SongEffectDescriptor>());
            }
        }

        public SongEffectDescriptor Resolve(MediaSessionSnapshot session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.Title) || string.IsNullOrWhiteSpace(session.Artist))
                return null;
            var cleaned = TrackIdentityCleaner.Clean(new TrackIdentity(
                session.Title, session.Artist, session.Duration, session.Album));
            var title = Normalize(cleaned.Title);
            var artist = Normalize(cleaned.Artist);
            return entries.FirstOrDefault(entry => Matches(entry.Match, title, artist));
        }

        internal static Stream OpenEmbeddedResource(string suffix)
        {
            var assembly = typeof(SongEffectCatalog).GetTypeInfo().Assembly;
            var name = assembly.GetManifestResourceNames().FirstOrDefault(candidate =>
                candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            return name == null ? null : assembly.GetManifestResourceStream(name);
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
            var builder = new StringBuilder(normalized.Length);
            var pendingSpace = false;
            foreach (var character in normalized)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }
                if (pendingSpace) builder.Append(' ');
                builder.Append(character);
                pendingSpace = false;
            }
            return builder.ToString();
        }

        private static bool Matches(SongEffectMatch match, string title, string artist)
        {
            if (match == null || !MatchesArtist(match.ArtistAliases, artist)) return false;
            var titleBase = Normalize(match.TitleBase);
            if (title.Equals(titleBase, StringComparison.OrdinalIgnoreCase)) return true;
            if (!match.AllowVersionSuffixes || title.Length <= titleBase.Length ||
                !title.StartsWith(titleBase, StringComparison.OrdinalIgnoreCase)) return false;
            var suffix = title.Substring(titleBase.Length).TrimStart();
            return suffix.Length > 0 && VersionSuffixStarts.Contains(suffix[0]);
        }

        private static bool MatchesArtist(IReadOnlyList<string> aliases, string artist)
        {
            if (aliases == null || aliases.Count == 0) return false;
            var credits = ArtistSeparator.Split(artist)
                .Select(Normalize)
                .Where(credit => credit.Length > 0);
            return credits.Any(credit => aliases.Any(alias =>
                credit.Equals(Normalize(alias), StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsValid(SongEffectDescriptor entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Match == null || entry.Effect == null ||
                string.IsNullOrWhiteSpace(entry.Match.TitleBase) || entry.Match.ArtistAliases == null ||
                entry.Match.ArtistAliases.Count == 0 ||
                !FullscreenSongEffectHost.IsRegisteredEffectType(entry.Effect.Type) ||
                entry.Effect.Assets == null || entry.Effect.Assets.Count == 0 || entry.Effect.Colors == null ||
                entry.Effect.Colors.Count == 0) return false;
            var effect = entry.Effect;
            return effect.MinSize > 0 && effect.MaxSize >= effect.MinSize && effect.MaxSize <= 256 &&
                effect.Count1080p >= 1 && effect.Count1440p >= effect.Count1080p &&
                effect.Count4k >= effect.Count1440p && effect.Count4k <= 32 &&
                effect.MinOpacity >= 0 && effect.MaxOpacity >= effect.MinOpacity && effect.MaxOpacity <= 1 &&
                effect.LyricsOpacityMultiplier >= 0 && effect.LyricsOpacityMultiplier <= 1 &&
                effect.CurrentLineOpacityMultiplier >= 0 && effect.CurrentLineOpacityMultiplier <= 1;
        }
    }

    internal sealed class SongEffectCatalogDocument
    {
        public int SchemaVersion { get; set; }
        public List<SongEffectDescriptor> Entries { get; set; }
    }

    internal sealed class SongEffectDescriptor
    {
        public string Id { get; set; }
        public SongEffectMatch Match { get; set; }
        public SongEffectDefinition Effect { get; set; }
    }

    internal sealed class SongEffectMatch
    {
        public List<string> ArtistAliases { get; set; }
        public string TitleBase { get; set; }
        public bool AllowVersionSuffixes { get; set; }
    }

    internal sealed class SongEffectDefinition
    {
        public string Type { get; set; }
        public List<string> Assets { get; set; }
        public List<string> Colors { get; set; }
        public double MinSize { get; set; }
        public double MaxSize { get; set; }
        public int Count1080p { get; set; }
        public int Count1440p { get; set; }
        public int Count4k { get; set; }
        public double MinOpacity { get; set; }
        public double MaxOpacity { get; set; }
        public double LyricsOpacityMultiplier { get; set; }
        public double CurrentLineOpacityMultiplier { get; set; }
    }
}
