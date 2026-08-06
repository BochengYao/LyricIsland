using System;
using System.IO;
using System.Text.Json;

namespace LyricHover.Core
{
    public sealed class ProEntitlementSnapshot
    {
        public int SchemaVersion { get; set; } = 2;

        public ProEntitlementKind Kind { get; set; }

        public DateTimeOffset VerifiedAtUtc { get; set; }

        public DateTimeOffset? AcquiredAtUtc { get; set; }
    }

    public sealed class ProEntitlementCache
    {
        private readonly string path;

        public ProEntitlementCache(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A cache path is required.", nameof(path));
            }

            this.path = Path.GetFullPath(path);
        }

        public bool TryRead(out ProEntitlementSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var parsed = JsonSerializer.Deserialize<ProEntitlementSnapshot>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                if (parsed == null ||
                    (parsed.SchemaVersion != 1 && parsed.SchemaVersion != 2) ||
                    !Enum.IsDefined(typeof(ProEntitlementKind), parsed.Kind) ||
                    parsed.VerifiedAtUtc == default)
                {
                    return false;
                }

                if (parsed.Kind != ProEntitlementKind.None &&
                    parsed.AcquiredAtUtc == null)
                {
                    parsed.AcquiredAtUtc = parsed.VerifiedAtUtc;
                }

                parsed.SchemaVersion = 2;
                snapshot = parsed;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public void Write(ProEntitlementKind kind, DateTimeOffset verifiedAtUtc)
        {
            Write(kind, verifiedAtUtc, null);
        }

        public void Write(
            ProEntitlementKind kind,
            DateTimeOffset verifiedAtUtc,
            DateTimeOffset? acquiredAtUtc)
        {
            var snapshot = new ProEntitlementSnapshot
            {
                Kind = kind,
                VerifiedAtUtc = verifiedAtUtc.ToUniversalTime(),
                AcquiredAtUtc = kind == ProEntitlementKind.None
                    ? (DateTimeOffset?)null
                    : (acquiredAtUtc ?? verifiedAtUtc).ToUniversalTime()
            };
            var json = JsonSerializer.Serialize(
                snapshot,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            AtomicFileWriter.WriteAllText(path, json);
        }

        public void Clear()
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
