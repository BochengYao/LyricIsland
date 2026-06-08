using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace AppleMusicDesktopLyrics.App
{
    public sealed class PowerShellNowPlayingProvider
    {
        private readonly string scriptPath;

        public PowerShellNowPlayingProvider(string scriptPath)
        {
            this.scriptPath = scriptPath ?? throw new ArgumentNullException(nameof(scriptPath));
        }

        public async Task<NowPlayingState> GetCurrentAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(error.Trim());
                }

                return Parse(output);
            }
        }

        private static NowPlayingState Parse(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                var root = document.RootElement;
                var state = new NowPlayingState();
                state.HasSession = GetBool(root, "hasSession");
                state.Title = GetString(root, "title");
                state.Artist = GetString(root, "artist");
                state.Album = GetString(root, "album");
                state.DurationSeconds = GetInt(root, "durationSeconds");
                state.PositionSeconds = GetInt(root, "positionSeconds");
                state.IsPlaying = GetBool(root, "isPlaying");
                state.SourceAppUserModelId = GetString(root, "sourceAppUserModelId");
                return state;
            }
        }

        private static string GetString(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static int GetInt(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number)
            {
                return property.GetInt32();
            }

            return 0;
        }

        private static bool GetBool(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var property) &&
                (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
            {
                return property.GetBoolean();
            }

            return false;
        }
    }
}
