using System;
using System.Collections.Generic;

namespace LyricsIsland.Core.Layout
{
    public sealed class LayoutDropCommitGuard
    {
        private readonly HashSet<string> committedOperationIds =
            new HashSet<string>(StringComparer.Ordinal);

        public bool TryCommit(string operationId)
        {
            return !string.IsNullOrWhiteSpace(operationId) &&
                committedOperationIds.Add(operationId);
        }

        public void Reset()
        {
            committedOperationIds.Clear();
        }
    }
}
