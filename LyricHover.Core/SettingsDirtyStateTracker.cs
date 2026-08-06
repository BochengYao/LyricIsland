using System;

namespace LyricHover.Core
{
    public sealed class SettingsDirtyStateTracker<T>
    {
        private readonly Func<T, string> createFingerprint;
        private string acceptedFingerprint;

        public SettingsDirtyStateTracker(T settings, Func<T, string> createFingerprint)
        {
            this.createFingerprint = createFingerprint ?? throw new ArgumentNullException(nameof(createFingerprint));
            Accept(settings);
        }

        public bool IsDirty(T settings)
        {
            return !string.Equals(
                acceptedFingerprint,
                createFingerprint(settings) ?? string.Empty,
                StringComparison.Ordinal);
        }

        public void Accept(T settings)
        {
            acceptedFingerprint = createFingerprint(settings) ?? string.Empty;
        }
    }
}
