using System;
using System.Collections.Generic;
using System.Linq;

namespace LyricsIsland.Core.Layout
{
    public static class ModuleLayoutProjection
    {
        public const string PlaceholderId = "__module_drag_placeholder__";

        public static IReadOnlyList<string> Project(
            IEnumerable<string> moduleIds,
            string sourceId,
            int destinationIndex)
        {
            var projected = (moduleIds ?? Enumerable.Empty<string>())
                .Where(id => !string.Equals(id, sourceId, StringComparison.Ordinal))
                .ToList();
            var normalized = Math.Max(0, Math.Min(destinationIndex, projected.Count));
            projected.Insert(normalized, PlaceholderId);
            return projected.AsReadOnly();
        }

        public static int ToMoveInsertionIndex(
            int sourceIndex,
            int destinationIndex,
            int moduleCount)
        {
            var maxDestination = Math.Max(0, moduleCount - 1);
            var normalized = Math.Max(0, Math.Min(destinationIndex, maxDestination));
            return sourceIndex >= 0 && sourceIndex <= normalized
                ? normalized + 1
                : normalized;
        }
    }
}
