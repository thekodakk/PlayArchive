using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlayArchive.Models;

namespace PlayArchive.Services
{
    public class ActiveTrackedSession
    {
        public int GameItemId { get; set; }
        public int SessionId { get; set; }
        public string GameTitle { get; set; } = string.Empty;
        public string ExecutableName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class ProcessTrackerService
    {
        private readonly GameDatabaseService _dbService;
        private CancellationTokenSource? _cts;
        private bool _isRunning;
        private int _pollingIntervalSeconds = 3;
        private readonly SemaphoreSlim _scanLock = new(1, 1);

        // Background utility process noise to ignore unless explicitly foreground
        private static readonly HashSet<string> UtilityNoise = new(StringComparer.OrdinalIgnoreCase)
        {
            "wallpaper32", "wallpaper64", "wallpaperengine", "steamwebhelper", "cefhost", "discord", "gamemanager"
        };

        // Tracks active running games: ExecutableName -> ActiveTrackedSession
        private readonly ConcurrentDictionary<string, ActiveTrackedSession> _activeSessions = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<TrackerLogEvent>? LogEventOccurred;
        public event EventHandler<ActiveTrackedSession>? GameStarted;
        public event EventHandler<ActiveTrackedSession>? GameStopped;
        public event EventHandler<bool>? TrackingStateChanged;

        public bool IsRunning => _isRunning;
        public int PollingIntervalSeconds
        {
            get => _pollingIntervalSeconds;
            set => _pollingIntervalSeconds = Math.Max(1, value);
        }

        public IReadOnlyDictionary<string, ActiveTrackedSession> ActiveSessions => _activeSessions;

        public ProcessTrackerService(GameDatabaseService dbService)
        {
            _dbService = dbService;
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _isRunning = true;
            TrackingStateChanged?.Invoke(this, true);

            EmitLog(LogEventType.TrackingStatus, "System", $"Background Game Process Tracking engine started (Interval: {_pollingIntervalSeconds}s).");

            // Clean orphan active sessions left over from previous crashes
            Task.Run(async () =>
            {
                await _dbService.CleanupOrphanSessionsAsync();
                await TrackingLoopAsync(_cts.Token);
            });
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _isRunning = false;
            TrackingStateChanged?.Invoke(this, false);

            EmitLog(LogEventType.TrackingStatus, "System", "Background Game Process Tracking engine stopped.");
        }

        private async Task TrackingLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckRunningProcessesAsync();
                }
                catch (Exception ex)
                {
                    EmitLog(LogEventType.Warning, "Engine Error", $"Error scanning processes: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public async Task CheckRunningProcessesAsync()
        {
            // Thread-safe lock to prevent concurrent scan collisions
            if (!await _scanLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                var games = await _dbService.GetAllGamesAsync();
                var trackedGames = games.Where(g => g.IsTrackingEnabled).ToList();

                if (!trackedGames.Any()) return;

                // Map executable names to GameItems
                var trackedMap = new Dictionary<string, GameItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in trackedGames)
                {
                    string exeKey = CleanExeName(g.ExecutableName);
                    if (!string.IsNullOrEmpty(exeKey))
                    {
                        trackedMap[exeKey] = g;
                    }
                }

                // Get current running process names
                Process[] processes = Process.GetProcesses();
                var runningProcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var runningProcIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var runningProcWindowHandles = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);

                foreach (var proc in processes)
                {
                    try
                    {
                        string pName = proc.ProcessName;
                        runningProcNames.Add(pName);
                        runningProcNames.Add(pName + ".exe");
                        if (!runningProcIds.ContainsKey(pName))
                        {
                            runningProcIds[pName] = proc.Id;
                            runningProcWindowHandles[pName] = proc.MainWindowHandle;
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible processes
                    }
                }

                // 1. Detect Newly Started Games
                foreach (var kvp in trackedMap)
                {
                    string exeKey = kvp.Key;
                    GameItem game = kvp.Value;
                    string cleanName = exeKey.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? exeKey[..^4]
                        : exeKey;

                    // Filter out background utility noise unless it has an active visible window
                    if (UtilityNoise.Contains(cleanName))
                    {
                        IntPtr handle = runningProcWindowHandles.TryGetValue(cleanName, out var h) ? h : IntPtr.Zero;
                        if (handle == IntPtr.Zero)
                        {
                            continue;
                        }
                    }

                    bool isRunningNow = runningProcNames.Contains(exeKey) || runningProcNames.Contains(cleanName);

                    // Check if an active session already exists in memory or for this game ID
                    bool isAlreadyTracked = _activeSessions.ContainsKey(exeKey) ||
                                            _activeSessions.Values.Any(s => s.GameItemId == game.Id);

                    if (isRunningNow && !isAlreadyTracked)
                    {
                        // Double check DB for existing active session before creating a new one
                        var dbActiveSession = await _dbService.GetActiveSessionForGameAsync(game.Id);
                        GameSession session = dbActiveSession ?? await _dbService.StartSessionAsync(game.Id);

                        int pid = runningProcIds.TryGetValue(cleanName, out int id) ? id : 0;

                        var activeTrack = new ActiveTrackedSession
                        {
                            GameItemId = game.Id,
                            SessionId = session.Id,
                            GameTitle = game.Title,
                            ExecutableName = game.ExecutableName,
                            ProcessId = pid,
                            StartTime = session.StartTime
                        };

                        _activeSessions[exeKey] = activeTrack;

                        if (dbActiveSession == null)
                        {
                            EmitLog(LogEventType.GameStarted, game.Title, $"Detected launch of '{game.Title}' ({game.ExecutableName}, PID: {pid}). Session #{session.Id} started.");
                            GameStarted?.Invoke(this, activeTrack);
                        }
                    }
                }

                // 2. Detect Closed Games
                var currentActiveKeys = _activeSessions.Keys.ToList();
                foreach (var exeKey in currentActiveKeys)
                {
                    string cleanName = exeKey.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? exeKey[..^4]
                        : exeKey;

                    bool isStillRunning = runningProcNames.Contains(exeKey) || runningProcNames.Contains(cleanName);

                    if (!isStillRunning)
                    {
                        if (_activeSessions.TryRemove(exeKey, out var activeTrack))
                        {
                            // End session cleanly in SQLite
                            await _dbService.EndSessionAsync(activeTrack.SessionId);

                            TimeSpan duration = DateTime.Now - activeTrack.StartTime;
                            string formattedDur = duration.TotalHours >= 1
                                ? $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s"
                                : $"{duration.Minutes}m {duration.Seconds}s";

                            EmitLog(LogEventType.GameStopped, activeTrack.GameTitle, $"Detected exit of '{activeTrack.GameTitle}'. Session saved (Duration: {formattedDur}).");
                            GameStopped?.Invoke(this, activeTrack);
                        }
                    }
                }
            }
            finally
            {
                _scanLock.Release();
            }
        }

        private static string CleanExeName(string exeName)
        {
            if (string.IsNullOrWhiteSpace(exeName)) return string.Empty;
            return Path.GetFileName(exeName).Trim();
        }

        private void EmitLog(LogEventType type, string gameTitle, string message)
        {
            var log = new TrackerLogEvent
            {
                Timestamp = DateTime.Now,
                Type = type,
                GameTitle = gameTitle,
                Message = message
            };
            LogEventOccurred?.Invoke(this, log);
        }
    }
}
