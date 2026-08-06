using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LyricHover.Core
{
    public sealed class SupporterProfile
    {
        public const string DefaultNickname = "LYRIC HOVER 支持者";

        public int SchemaVersion { get; set; } = 1;

        public string Nickname { get; set; } = DefaultNickname;
    }

    public sealed class SupporterProfileStore
    {
        public const int MaximumNicknameLength = 24;

        private readonly string path;

        public SupporterProfileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A profile path is required.", nameof(path));
            }

            this.path = Path.GetFullPath(path);
        }

        public SupporterProfile Load()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return CreateDefault();
                }

                var parsed = JsonSerializer.Deserialize<SupporterProfile>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                if (parsed == null || parsed.SchemaVersion != 1)
                {
                    return CreateDefault();
                }

                parsed.Nickname = SanitizeNickname(parsed.Nickname);
                return parsed;
            }
            catch (IOException)
            {
                return CreateDefault();
            }
            catch (UnauthorizedAccessException)
            {
                return CreateDefault();
            }
            catch (JsonException)
            {
                return CreateDefault();
            }
        }

        public SupporterProfile Save(string nickname)
        {
            var profile = new SupporterProfile
            {
                Nickname = SanitizeNickname(nickname)
            };
            AtomicFileWriter.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    profile,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
            return profile;
        }

        public static string SanitizeNickname(string nickname)
        {
            var filtered = new string((nickname ?? string.Empty)
                .Where(character => !char.IsControl(character))
                .ToArray())
                .Trim();
            if (filtered.Length > MaximumNicknameLength)
            {
                filtered = filtered.Substring(0, MaximumNicknameLength).TrimEnd();
            }

            return string.IsNullOrWhiteSpace(filtered)
                ? SupporterProfile.DefaultNickname
                : filtered;
        }

        private static SupporterProfile CreateDefault()
        {
            return new SupporterProfile();
        }
    }
}
