using System;

namespace LyricHover.App
{
    public sealed class SettingsRuntimeStateCoordinator
    {
        private readonly OverlaySettingsStore store;
        private OverlayPlacementSettings current;

        public SettingsRuntimeStateCoordinator(
            OverlaySettingsStore store,
            OverlayPlacementSettings initialSettings)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            current = CloneNormalized(initialSettings);
        }

        internal OverlayPlacementSettings Current => current;

        public OverlayPlacementSettings CreateEditSnapshot()
        {
            return current.DeepClone();
        }

        public OverlayPlacementSettings ApplyDraft(
            OverlayPlacementSettings draft,
            Action<OverlayPlacementSettings, OverlayPlacementSettings> synchronizeRuntime)
        {
            if (synchronizeRuntime == null)
            {
                throw new ArgumentNullException(nameof(synchronizeRuntime));
            }

            var previous = current.DeepClone();
            current = CloneNormalized(draft);
            try
            {
                synchronizeRuntime(previous, current);
                current.Normalize();
                store.Save(current);
                return CreateEditSnapshot();
            }
            catch
            {
                var failed = current;
                current = previous;
                try
                {
                    synchronizeRuntime(failed, current);
                }
                catch
                {
                    // Preserve the original Apply failure. The committed snapshot is still
                    // restored even if a best-effort runtime rollback cannot complete.
                }

                throw;
            }
        }

        public OverlayPlacementSettings CommitRuntimeMutation(Action<OverlayPlacementSettings> mutation)
        {
            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            var next = current.DeepClone();
            mutation(next);
            return CommitRuntimeSnapshot(next);
        }

        public OverlayPlacementSettings CommitRuntimeSnapshot(OverlayPlacementSettings settings)
        {
            var next = CloneNormalized(settings);
            store.Save(next);
            current = next;
            return CreateEditSnapshot();
        }

        public void SetTransientRuntimeSnapshot(OverlayPlacementSettings settings)
        {
            current = CloneNormalized(settings);
        }

        public void PersistCurrentRuntimeState()
        {
            current.Normalize();
            store.Save(current);
        }

        private static OverlayPlacementSettings CloneNormalized(OverlayPlacementSettings settings)
        {
            var clone = (settings ?? new OverlayPlacementSettings()).DeepClone();
            clone.Normalize();
            return clone;
        }
    }
}
