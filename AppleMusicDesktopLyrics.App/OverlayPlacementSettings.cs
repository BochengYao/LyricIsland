using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using AppleMusicDesktopLyrics.Core;

namespace AppleMusicDesktopLyrics.App
{
    public sealed class OverlayPlacementSettings
    {
        public const int MinCacheLimitMegabytes = 1;
        public const int MaxCacheLimitMegabytes = 1024;
        public const int MinHoverAuraSize = 40;
        public const int MaxHoverAuraSize = 220;
        public const int MinHoverDetectionRange = 60;
        public const int MaxHoverDetectionRange = 640;
        public const double MinHoverAuraAspectRatio = 0.8;
        public const double MaxHoverAuraAspectRatio = 2.0;
        public const int MinHoverTransparencyPercent = 0;
        public const int MaxHoverTransparencyPercent = 100;

        public string ScreenName { get; set; }

        public OverlayDockEdge Edge { get; set; } = OverlayDockEdge.Top;

        public double OffsetRatio { get; set; } = 0.5;

        public int CacheLimitMegabytes { get; set; } = LyricsCache.DefaultMaxMegabytes;

        public int HoverAuraSize { get; set; } = 96;

        public int HoverDetectionRange { get; set; } = 340;

        public double HoverAuraAspectRatio { get; set; } = 1.25;

        public int HoverTransparencyPercent { get; set; } = 82;

        public List<HoverSpectrumStop> HoverSpectrumStops { get; set; } = CreateDefaultHoverSpectrumStops();

        public bool PassThroughOnHover { get; set; } = true;

        public SettingsThemePreference SettingsTheme { get; set; } = SettingsThemePreference.System;

        public LyricsSourcePreference LyricsSource { get; set; } = LyricsSourcePreference.Automatic;

        public bool UseMultiLineDisplay { get; set; } = true;

        public bool ShowTranslation { get; set; } = true;

        public OverlayPlacement ToPlacement()
        {
            return new OverlayPlacement(ScreenName ?? string.Empty, NormalizeEdge(Edge), OffsetRatio);
        }

        public void Normalize()
        {
            Edge = NormalizeEdge(Edge);
            OffsetRatio = Math.Max(0, Math.Min(1, OffsetRatio));
            CacheLimitMegabytes = Math.Max(MinCacheLimitMegabytes, Math.Min(MaxCacheLimitMegabytes, CacheLimitMegabytes));
            HoverAuraSize = Math.Max(MinHoverAuraSize, Math.Min(MaxHoverAuraSize, HoverAuraSize));
            HoverDetectionRange = Math.Max(MinHoverDetectionRange, Math.Min(MaxHoverDetectionRange, HoverDetectionRange));
            HoverAuraAspectRatio = Math.Max(MinHoverAuraAspectRatio, Math.Min(MaxHoverAuraAspectRatio, HoverAuraAspectRatio));
            HoverTransparencyPercent = Math.Max(MinHoverTransparencyPercent, Math.Min(MaxHoverTransparencyPercent, HoverTransparencyPercent));
            HoverSpectrumStops = NormalizeHoverSpectrumStops(HoverSpectrumStops, HoverTransparencyPercent);
            if (!Enum.IsDefined(typeof(LyricsSourcePreference), LyricsSource))
            {
                LyricsSource = LyricsSourcePreference.Automatic;
            }

            if (!Enum.IsDefined(typeof(SettingsThemePreference), SettingsTheme))
            {
                SettingsTheme = SettingsThemePreference.System;
            }

            if (ShowTranslation)
            {
                UseMultiLineDisplay = true;
            }
        }

        private static OverlayDockEdge NormalizeEdge(OverlayDockEdge edge)
        {
            return OverlayDockEdge.Top;
        }

        public static List<HoverSpectrumStop> CreateDefaultHoverSpectrumStops()
        {
            return new List<HoverSpectrumStop>
            {
                new HoverSpectrumStop { PositionPercent = 0, TransparencyPercent = 88 },
                new HoverSpectrumStop { PositionPercent = 46, TransparencyPercent = 54 },
                new HoverSpectrumStop { PositionPercent = 100, TransparencyPercent = 0 }
            };
        }

        private static List<HoverSpectrumStop> NormalizeHoverSpectrumStops(List<HoverSpectrumStop> stops, int fallbackTransparencyPercent)
        {
            if (stops == null || stops.Count < 3)
            {
                stops = CreateDefaultHoverSpectrumStops();
                stops[0].TransparencyPercent = Math.Max(MinHoverTransparencyPercent, Math.Min(MaxHoverTransparencyPercent, fallbackTransparencyPercent));
            }

            var normalized = stops
                .Where(stop => stop != null)
                .OrderBy(stop => stop.PositionPercent)
                .Take(3)
                .Select(stop => new HoverSpectrumStop
                {
                    PositionPercent = Math.Max(0, Math.Min(100, stop.PositionPercent)),
                    TransparencyPercent = Math.Max(MinHoverTransparencyPercent, Math.Min(MaxHoverTransparencyPercent, stop.TransparencyPercent))
                })
                .ToList();

            while (normalized.Count < 3)
            {
                normalized.Add(CreateDefaultHoverSpectrumStops()[normalized.Count]);
            }

            normalized[0].PositionPercent = 0;
            normalized[1].PositionPercent = Math.Max(5, Math.Min(95, normalized[1].PositionPercent));
            normalized[2].PositionPercent = 100;
            return normalized;
        }
    }

    public sealed class OverlaySettingsStore
    {
        private readonly string path;

        public OverlaySettingsStore(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public OverlayPlacementSettings Load()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new OverlayPlacementSettings();
                }

                var settings = JsonSerializer.Deserialize<OverlayPlacementSettings>(File.ReadAllText(path)) ?? new OverlayPlacementSettings();
                var originalEdge = settings.Edge;
                var originalOffset = settings.OffsetRatio;
                var originalCacheLimit = settings.CacheLimitMegabytes;
                var originalHoverAuraSize = settings.HoverAuraSize;
                var originalHoverDetectionRange = settings.HoverDetectionRange;
                var originalHoverAuraAspectRatio = settings.HoverAuraAspectRatio;
                var originalHoverTransparency = settings.HoverTransparencyPercent;
                var originalHoverSpectrum = SerializeHoverSpectrum(settings.HoverSpectrumStops);
                var originalPassThroughOnHover = settings.PassThroughOnHover;
                var originalSettingsTheme = settings.SettingsTheme;
                var originalLyricsSource = settings.LyricsSource;
                var originalUseMultiLineDisplay = settings.UseMultiLineDisplay;
                var originalShowTranslation = settings.ShowTranslation;
                settings.Normalize();
                if (settings.Edge != originalEdge ||
                    Math.Abs(settings.OffsetRatio - originalOffset) > 0.0001 ||
                    settings.CacheLimitMegabytes != originalCacheLimit ||
                    settings.HoverAuraSize != originalHoverAuraSize ||
                    settings.HoverDetectionRange != originalHoverDetectionRange ||
                    Math.Abs(settings.HoverAuraAspectRatio - originalHoverAuraAspectRatio) > 0.0001 ||
                    settings.HoverTransparencyPercent != originalHoverTransparency ||
                    SerializeHoverSpectrum(settings.HoverSpectrumStops) != originalHoverSpectrum ||
                    settings.PassThroughOnHover != originalPassThroughOnHover ||
                    settings.SettingsTheme != originalSettingsTheme ||
                    settings.LyricsSource != originalLyricsSource ||
                    settings.UseMultiLineDisplay != originalUseMultiLineDisplay ||
                    settings.ShowTranslation != originalShowTranslation)
                {
                    Save(settings);
                }

                return settings;
            }
            catch
            {
                return new OverlayPlacementSettings();
            }
        }

        public void Save(OverlayPlacementSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var normalized = settings ?? new OverlayPlacementSettings();
            normalized.Normalize();
            var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static string SerializeHoverSpectrum(List<HoverSpectrumStop> stops)
        {
            if (stops == null)
            {
                return string.Empty;
            }

            return string.Join("|", stops.Select(stop => (stop?.PositionPercent ?? 0) + ":" + (stop?.TransparencyPercent ?? 0)));
        }
    }

    public sealed class HoverSpectrumStop
    {
        public int PositionPercent { get; set; }

        public int TransparencyPercent { get; set; }
    }

    public enum SettingsThemePreference
    {
        System,
        Light,
        Dark
    }
}
