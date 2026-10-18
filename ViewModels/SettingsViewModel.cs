using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlayArchive.Services;

namespace PlayArchive.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ProcessTrackerService _trackerService;
        private readonly GameDatabaseService _dbService;
        private readonly GameScannerService _scannerService;

        [ObservableProperty]
        private int _pollingIntervalSeconds = 3;

        [ObservableProperty]
        private string _databasePath = string.Empty;

        [ObservableProperty]
        private string _engineStatusText = "Active";

        [ObservableProperty]
        private string _dbSizeText = "0 KB";

        [ObservableProperty]
        private string _scanResultText = "Ready to scan system for Steam & Epic installed games.";

        public IRelayCommand ToggleEngineCommand { get; }
        public IAsyncRelayCommand TriggerManualScanCommand { get; }
        public IAsyncRelayCommand ScanGamesCommand { get; }

        public SettingsViewModel(ProcessTrackerService trackerService, GameDatabaseService dbService, GameScannerService scannerService)
        {
            _trackerService = trackerService;
            _dbService = dbService;
            _scannerService = scannerService;

            PollingIntervalSeconds = _trackerService.PollingIntervalSeconds;
            EngineStatusText = _trackerService.IsRunning ? "Active" : "Stopped";

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayArchive");
            DatabasePath = Path.Combine(folder, "PlayArchive.db");

            UpdateDbSize();

            ToggleEngineCommand = new RelayCommand(ToggleEngine);
            TriggerManualScanCommand = new AsyncRelayCommand(TriggerManualScanAsync);
            ScanGamesCommand = new AsyncRelayCommand(ScanGamesAsync);

            _trackerService.TrackingStateChanged += (s, running) =>
            {
                EngineStatusText = running ? "Active" : "Stopped";
            };
        }

        partial void OnPollingIntervalSecondsChanged(int value)
        {
            _trackerService.PollingIntervalSeconds = value;
        }

        private void ToggleEngine()
        {
            if (_trackerService.IsRunning)
            {
                _trackerService.Stop();
            }
            else
            {
                _trackerService.Start();
            }
        }

        private async Task TriggerManualScanAsync()
        {
            await _trackerService.CheckRunningProcessesAsync();
            UpdateDbSize();
        }

        private async Task ScanGamesAsync()
        {
            ScanResultText = "Scanning Steam & Epic game directories...";
            try
            {
                int count = await _scannerService.ScanAndImportGamesAsync();
                ScanResultText = count > 0 ? $"Scan completed: Added {count} new installed games!" : "Scan completed: All installed games are already in library.";
                UpdateDbSize();
            }
            catch (Exception ex)
            {
                ScanResultText = $"Scan failed: {ex.Message}";
            }
        }

        private void UpdateDbSize()
        {
            if (File.Exists(DatabasePath))
            {
                FileInfo fi = new FileInfo(DatabasePath);
                DbSizeText = $"{fi.Length / 1024.0:F1} KB";
            }
        }
    }
}
