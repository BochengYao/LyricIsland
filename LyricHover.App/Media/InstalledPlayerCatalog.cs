using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LyricHover.Core.Media;
using Microsoft.Win32;

namespace LyricHover.App.Media
{
    public sealed class InstalledPlayer
    {
        public InstalledPlayer(PlayerKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName ?? string.Empty;
        }

        public PlayerKind Kind { get; }
        public string DisplayName { get; }
        public string SelectionKey => PlayerProfileCatalog.GetSelectionKey(Kind);
    }

    public static class InstalledPlayerCatalog
    {
        private sealed class Definition
        {
            public Definition(PlayerKind kind, string displayName, string[] evidenceTokens, string[] executableNames)
            {
                Kind = kind;
                DisplayName = displayName;
                EvidenceTokens = evidenceTokens;
                ExecutableNames = executableNames;
            }

            public PlayerKind Kind { get; }
            public string DisplayName { get; }
            public string[] EvidenceTokens { get; }
            public string[] ExecutableNames { get; }
        }

        private static readonly Definition[] Definitions =
        {
            new Definition(PlayerKind.AppleMusic, "Apple Music", new[] { "apple music", "applemusic" }, new[] { "AppleMusic.exe" }),
            new Definition(PlayerKind.QQMusic, "QQ 音乐", new[] { "qq音乐", "qq music", "qqmusic" }, new[] { "QQMusic.exe" }),
            new Definition(PlayerKind.NetEaseCloudMusicUwp, "网易云音乐", new[] { "网易云音乐", "netease cloudmusic", "cloudmusic", "orpheus" }, new[] { "cloudmusic.exe" }),
            new Definition(PlayerKind.KuGou, "酷狗音乐", new[] { "酷狗音乐", "kugou", "kgmusic" }, new[] { "KuGou.exe" }),
            new Definition(PlayerKind.Kuwo, "酷我音乐", new[] { "酷我音乐", "kuwo", "kwmusic" }, new[] { "KuwoMusic.exe", "KwMusic.exe" }),
            new Definition(PlayerKind.Spotify, "Spotify", new[] { "spotify" }, new[] { "Spotify.exe" })
        };

        public static IReadOnlyList<InstalledPlayer> Detect()
        {
            var evidence = ReadRegistryEvidence();
            var installed = new List<InstalledPlayer>();
            foreach (var definition in Definitions)
            {
                if (HasMatchingEvidence(definition, evidence) ||
                    HasRegisteredExecutable(definition.ExecutableNames) ||
                    HasCommonInstallPath(definition.Kind))
                {
                    installed.Add(new InstalledPlayer(definition.Kind, definition.DisplayName));
                }
            }

            return installed.AsReadOnly();
        }

        private static bool HasMatchingEvidence(Definition definition, IEnumerable<string> evidence)
        {
            return evidence.Any(value => definition.EvidenceTokens.Any(token =>
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static List<string> ReadRegistryEvidence()
        {
            var evidence = new List<string>();
            ReadUninstallEvidence(RegistryHive.CurrentUser, RegistryView.Default, evidence);
            ReadUninstallEvidence(RegistryHive.LocalMachine, RegistryView.Registry64, evidence);
            ReadUninstallEvidence(RegistryHive.LocalMachine, RegistryView.Registry32, evidence);
            try
            {
                using (var packages = Registry.CurrentUser.OpenSubKey(
                    @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                {
                    if (packages != null)
                    {
                        evidence.AddRange(packages.GetSubKeyNames());
                    }
                }
            }
            catch
            {
                // Package registration can be unavailable under restricted Windows profiles.
            }

            ReadStartMenuEvidence(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), evidence);
            ReadStartMenuEvidence(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), evidence);

            return evidence;
        }

        private static void ReadStartMenuEvidence(string root, ICollection<string> evidence)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return;
            }

            try
            {
                foreach (var shortcut in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    AddEvidence(evidence, Path.GetFileNameWithoutExtension(shortcut));
                    AddEvidence(evidence, shortcut);
                }
            }
            catch
            {
                // A broken or protected Start menu folder should not block the remaining detection sources.
            }
        }

        private static void ReadUninstallEvidence(RegistryHive hive, RegistryView view, ICollection<string> evidence)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstall == null)
                    {
                        return;
                    }

                    foreach (var keyName in uninstall.GetSubKeyNames())
                    {
                        using (var application = uninstall.OpenSubKey(keyName))
                        {
                            AddEvidence(evidence, keyName);
                            AddEvidence(evidence, application?.GetValue("DisplayName") as string);
                            AddEvidence(evidence, application?.GetValue("InstallLocation") as string);
                            AddEvidence(evidence, application?.GetValue("DisplayIcon") as string);
                        }
                    }
                }
            }
            catch
            {
                // One registry view failing should not hide results from the remaining views.
            }
        }

        private static void AddEvidence(ICollection<string> evidence, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                evidence.Add(value);
            }
        }

        private static bool HasRegisteredExecutable(IEnumerable<string> executableNames)
        {
            foreach (var executableName in executableNames)
            {
                if (IsAppPathRegistered(RegistryHive.CurrentUser, RegistryView.Default, executableName) ||
                    IsAppPathRegistered(RegistryHive.LocalMachine, RegistryView.Registry64, executableName) ||
                    IsAppPathRegistered(RegistryHive.LocalMachine, RegistryView.Registry32, executableName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAppPathRegistered(RegistryHive hive, RegistryView view, string executableName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var appPath = baseKey.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\App Paths\" + executableName))
                {
                    return appPath != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool HasCommonInstallPath(PlayerKind kind)
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            IEnumerable<string> candidates;
            switch (kind)
            {
                case PlayerKind.AppleMusic:
                    candidates = new[] { Path.Combine(localAppData, "Microsoft", "WindowsApps", "AppleMusic.exe") };
                    break;
                case PlayerKind.QQMusic:
                    candidates = new[] { Path.Combine(programFilesX86, "Tencent", "QQMusic", "QQMusic.exe") };
                    break;
                case PlayerKind.NetEaseCloudMusicUwp:
                    candidates = new[]
                    {
                        Path.Combine(programFilesX86, "Netease", "CloudMusic", "cloudmusic.exe"),
                        Path.Combine(localAppData, "NetEase", "CloudMusic", "cloudmusic.exe")
                    };
                    break;
                case PlayerKind.KuGou:
                    candidates = new[]
                    {
                        Path.Combine(programFilesX86, "KuGou", "KGMusic", "KuGou.exe"),
                        Path.Combine(roamingAppData, "KuGou8", "KuGou.exe")
                    };
                    break;
                case PlayerKind.Kuwo:
                    candidates = new[] { Path.Combine(programFilesX86, "Kuwo", "KuwoMusic", "KuwoMusic.exe") };
                    break;
                case PlayerKind.Spotify:
                    candidates = new[] { Path.Combine(roamingAppData, "Spotify", "Spotify.exe") };
                    break;
                default:
                    candidates = Enumerable.Empty<string>();
                    break;
            }

            return candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Any(File.Exists);
        }
    }
}
