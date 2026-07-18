using System;
using System.Collections.Generic;
using System.Linq;

namespace AppleMusicDesktopLyrics.Core.Layout
{
    public sealed class IslandLayoutProfile
    {
        public List<IslandModuleInstance> Modules { get; set; } = new List<IslandModuleInstance>();

        public void Normalize()
        {
            Modules = (Modules ?? new List<IslandModuleInstance>())
                .Where(module => module != null && Enum.IsDefined(typeof(IslandModuleType), module.Type))
                .ToList();
            foreach (var module in Modules)
            {
                if (string.IsNullOrWhiteSpace(module.Id)) module.Id = Guid.NewGuid().ToString("N");
                module.DividerOpacity = Math.Max(0, Math.Min(1, module.DividerOpacity));
                module.MarginBefore = Math.Max(0, Math.Min(64, module.MarginBefore));
                module.MarginAfter = Math.Max(0, Math.Min(64, module.MarginAfter));
                module.LyricsWidth = IslandModuleInstance.NormalizeLyricsWidth(module.LyricsWidth);
            }
        }
    }
}
