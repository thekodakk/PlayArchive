using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using PlayArchive.Models;

namespace PlayArchive.Services
{
    public class DiscoveredGameInfo
    {
        public string Title { get; set; } = string.Empty;
        public string ExecutableName { get; set; } = string.Empty;
        public string Source { get; set; } = "Scanner";
        public string Category { get; set; } = "PC Game";
        public string? ExecutablePath { get; set; }
        public string? SteamAppId { get; set; }
    }

    public class GameScannerService
    {
        private readonly GameDatabaseService _dbService;

        public event EventHandler<string>? ScanProgress;

        public GameScannerService(GameDatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task<int> ScanAndImportGamesAsync()
        {
            ScanProgress?.Invoke(this, "Starting system scan for installed games...");

            var discoveredGames = new List<DiscoveredGameInfo>();

            try
            {
                // 1. Scan Steam Libraries
                var steamGames = ScanSteamLibraries();
                discoveredGames.AddRange(steamGames);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameScanner] Steam scan error: {ex.Message}");
            }

            try
            {
                // 2. Scan Epic Games Manifests
                var epicGames = ScanEpicGames();
                discoveredGames.AddRange(epicGames);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameScanner] Epic scan error: {ex.Message}");
            }

            // 3. Deduplicate against current DB
            var existingGames = await _dbService.GetAllGamesAsync();
            var existingExes = new HashSet<string>(existingGames.Select(g => g.ExecutableName), StringComparer.OrdinalIgnoreCase);
            var existingTitles = new HashSet<string>(existingGames.Select(g => g.Title), StringComparer.OrdinalIgnoreCase);

            int addedCount = 0;

            foreach (var discovered in discoveredGames)
            {
                if (string.IsNullOrWhiteSpace(discovered.ExecutableName) || string.IsNullOrWhiteSpace(discovered.Title))
                {
                    continue;
                }

                string exeKey = discovered.ExecutableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? discovered.ExecutableName
                    : discovered.ExecutableName + ".exe";

                if (!existingExes.Contains(exeKey) && !existingTitles.Contains(discovered.Title))
                {
                    var newGame = new GameItem
                    {
                        Title = discovered.Title.Trim(),
                        ExecutableName = exeKey.Trim(),
                        ExecutablePath = discovered.ExecutablePath,
                        SteamAppId = discovered.SteamAppId,
                        Category = discovered.Category,
                        Notes = $"Discovered via {discovered.Source} Scanner on {DateTime.Now:yyyy-MM-dd}",
                        IsTrackingEnabled = true,
                        CreatedAt = DateTime.Now
                    };

                    await _dbService.AddGameAsync(newGame);
                    existingExes.Add(exeKey);
                    existingTitles.Add(discovered.Title);
                    addedCount++;
                }
            }

            ScanProgress?.Invoke(this, $"Scan completed. Discovered and added {addedCount} new games.");
            return addedCount;
        }

        private List<DiscoveredGameInfo> ScanSteamLibraries()
        {
            var results = new List<DiscoveredGameInfo>();

            string? steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            {
                return results;
            }

            var libraryFolders = new List<string> { Path.Combine(steamPath, "steamapps") };

            // Parse libraryfolders.vdf
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    string vdfContent = File.ReadAllText(vdfPath);
                    var matches = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        if (m.Success && m.Groups.Count > 1)
                        {
                            string pathStr = m.Groups[1].Value.Replace(@"\\", @"\");
                            string appsDir = Path.Combine(pathStr, "steamapps");
                            if (Directory.Exists(appsDir) && !libraryFolders.Contains(appsDir, StringComparer.OrdinalIgnoreCase))
                            {
                                libraryFolders.Add(appsDir);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parse error
                }
            }

            // Scan each steamapps folder for appmanifest_*.acf
            foreach (var libFolder in libraryFolders)
            {
                if (!Directory.Exists(libFolder)) continue;

                try
                {
                    var acfFiles = Directory.GetFiles(libFolder, "appmanifest_*.acf");
                    foreach (var acf in acfFiles)
                    {
                        var info = ParseSteamAcfFile(acf, libFolder);
                        if (info != null)
                        {
                            results.Add(info);
                        }
                    }
                }
                catch
                {
                    // Continue
                }
            }

            return results;
        }

        private static string? GetSteamInstallPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string p1 && Directory.Exists(p1))
                {
                    return p1;
                }
            }
            catch { }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                if (key?.GetValue("InstallPath") is string p2 && Directory.Exists(p2))
                {
                    return p2;
                }
            }
            catch { }

            string fallback = @"C:\Program Files (x86)\Steam";
            return Directory.Exists(fallback) ? fallback : null;
        }

        private static DiscoveredGameInfo? ParseSteamAcfFile(string acfPath, string steamAppsFolder)
        {
            try
            {
                string text = File.ReadAllText(acfPath);

                string? name = MatchVdfValue(text, "name");
                string? installDir = MatchVdfValue(text, "installdir");
                string? appId = MatchVdfValue(text, "appid");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDir))
                {
                    return null;
                }

                // Filter out Steamworks Common Redistributables or tools
                if (name.Contains("Steamworks Common", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Proton", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("SteamVR", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string commonPath = Path.Combine(steamAppsFolder, "common", installDir);
                if (!Directory.Exists(commonPath))
                {
                    return null;
                }

                // Find main executable inside game directory
                string? mainExePath = FindMainExecutable(commonPath, name);
                string exeName = !string.IsNullOrEmpty(mainExePath)
                    ? Path.GetFileName(mainExePath)
                    : installDir + ".exe";

                return new DiscoveredGameInfo
                {
                    Title = name,
                    ExecutableName = exeName,
                    ExecutablePath = mainExePath,
                    SteamAppId = appId,
                    Category = "Steam Game",
                    Source = "Steam"
                };
            }
            catch
            {
                return null;
            }
        }

        private static string? MatchVdfValue(string vdfText, string key)
        {
            var match = Regex.Match(vdfText, $@"""{key}""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
        }

        private List<DiscoveredGameInfo> ScanEpicGames()
        {
            var results = new List<DiscoveredGameInfo>();

            string epicDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
            if (!Directory.Exists(epicDataDir))
            {
                return results;
            }

            try
            {
                var manifestFiles = Directory.GetFiles(epicDataDir, "*.item");
                foreach (var itemFile in manifestFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(itemFile);

                        string? name = MatchJsonValue(content, "DisplayName");
                        string? installLocation = MatchJsonValue(content, "InstallLocation");
                        string? launchExe = MatchJsonValue(content, "LaunchExecutable");

                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(launchExe))
                        {
                            string exeName = Path.GetFileName(launchExe);
                            string? fullPath = !string.IsNullOrEmpty(installLocation)
                                ? Path.Combine(installLocation, launchExe)
                                : null;

                            results.Add(new DiscoveredGameInfo
                            {
                                Title = name,
                                ExecutableName = exeName,
                                ExecutablePath = fullPath,
                                Category = "Epic Games",
                                Source = "Epic Games"
                            });
                        }
                    }
                    catch
                    {
                        // Skip unparseable item
                    }
                }
            }
            catch
            {
                // Skip
            }

            return results;
        }

        private static string? MatchJsonValue(string json, string key)
        {
            var match = Regex.Match(json, $@"""{key}""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
        }

        private static string? FindMainExecutable(string directoryPath, string gameTitle)
        {
            try
            {
                var exeFiles = Directory.GetFiles(directoryPath, "*.exe", SearchOption.TopDirectoryOnly)
                    .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                    .ToList();

                if (!exeFiles.Any())
                {
                    // Search 1 level deeper
                    exeFiles = Directory.GetFiles(directoryPath, "*.exe", SearchOption.AllDirectories)
                        .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                        .Take(15)
                        .ToList();
                }

                if (!exeFiles.Any()) return null;

                // Priority 1: Match title
                string cleanTitle = Regex.Replace(gameTitle, @"[^\w]", "").ToLower();
                var titleMatch = exeFiles.FirstOrDefault(f =>
                {
                    string name = Regex.Replace(Path.GetFileNameWithoutExtension(f), @"[^\w]", "").ToLower();
                    return name.Equals(cleanTitle, StringComparison.OrdinalIgnoreCase) || cleanTitle.Contains(name);
                });

                if (titleMatch != null) return titleMatch;

                // Priority 2: Largest .exe file size
                return exeFiles.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUtilityExe(string exeName)
        {
            string name = exeName.ToLower();
            string[] utils = { "unins", "setup", "crash", "unitycrashhandler", "dxwebsetup", "vcredist", "dotnet", "report", "bugreport", "launcher" };
            return utils.Any(u => name.Contains(u));
        }
    }
}
