using System;
using System.IO;
using System.Threading.Tasks;

namespace LyricHover.Core
{
    public sealed class ProEntitlementEvidence
    {
        public ProEntitlementEvidence(
            ProEntitlementKind kind,
            DateTimeOffset? acquiredAtUtc)
        {
            Kind = kind;
            AcquiredAtUtc = acquiredAtUtc?.ToUniversalTime();
        }

        public ProEntitlementKind Kind { get; }

        public DateTimeOffset? AcquiredAtUtc { get; }
    }

    public sealed class ProEntitlementResult
    {
        public ProEntitlementResult(
            ProEntitlementKind kind,
            bool storeQuerySucceeded,
            bool usedCache,
            DateTimeOffset? acquiredAtUtc = null)
        {
            Kind = kind;
            StoreQuerySucceeded = storeQuerySucceeded;
            UsedCache = usedCache;
            AcquiredAtUtc = acquiredAtUtc?.ToUniversalTime();
        }

        public ProEntitlementKind Kind { get; }

        public bool StoreQuerySucceeded { get; }

        public bool UsedCache { get; }

        public DateTimeOffset? AcquiredAtUtc { get; }
    }

    public sealed class ProEntitlementResolver
    {
        private readonly ProEntitlementCache cache;

        public ProEntitlementResolver(ProEntitlementCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<ProEntitlementResult> ResolveAsync(
            Func<Task<ProEntitlementKind>> queryStore)
        {
            if (queryStore == null)
            {
                throw new ArgumentNullException(nameof(queryStore));
            }

            return await ResolveAsync(async () =>
                new ProEntitlementEvidence(await queryStore(), null));
        }

        public async Task<ProEntitlementResult> ResolveAsync(
            Func<Task<ProEntitlementEvidence>> queryStore)
        {
            if (queryStore == null)
            {
                throw new ArgumentNullException(nameof(queryStore));
            }

            try
            {
                var evidence = await queryStore();
                if (evidence == null)
                {
                    throw new InvalidOperationException("Store entitlement evidence was not returned.");
                }

                var acquiredAtUtc = ResolveAcquiredAt(evidence);
                TryUpdateCache(evidence.Kind, acquiredAtUtc);
                return new ProEntitlementResult(
                    evidence.Kind,
                    true,
                    false,
                    acquiredAtUtc);
            }
            catch (Exception)
            {
                if (cache.TryRead(out var snapshot))
                {
                    return new ProEntitlementResult(
                        snapshot.Kind,
                        false,
                        true,
                        snapshot.AcquiredAtUtc);
                }

                return new ProEntitlementResult(
                    ProEntitlementKind.None,
                    false,
                    false,
                    null);
            }
        }

        private DateTimeOffset? ResolveAcquiredAt(ProEntitlementEvidence evidence)
        {
            if (evidence.Kind == ProEntitlementKind.None)
            {
                return null;
            }

            if (evidence.AcquiredAtUtc != null)
            {
                return evidence.AcquiredAtUtc;
            }

            if (cache.TryRead(out var snapshot) &&
                snapshot.Kind == evidence.Kind &&
                snapshot.AcquiredAtUtc != null)
            {
                return snapshot.AcquiredAtUtc;
            }

            return DateTimeOffset.UtcNow;
        }

        private void TryUpdateCache(
            ProEntitlementKind kind,
            DateTimeOffset? acquiredAtUtc)
        {
            try
            {
                if (kind == ProEntitlementKind.None)
                {
                    cache.Clear();
                }
                else
                {
                    cache.Write(kind, DateTimeOffset.UtcNow, acquiredAtUtc);
                }
            }
            catch (IOException)
            {
                TryOverwriteWithNone(kind);
            }
            catch (UnauthorizedAccessException)
            {
                TryOverwriteWithNone(kind);
            }
        }

        private void TryOverwriteWithNone(ProEntitlementKind kind)
        {
            if (kind != ProEntitlementKind.None)
            {
                return;
            }

            try
            {
                cache.Write(ProEntitlementKind.None, DateTimeOffset.UtcNow);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
