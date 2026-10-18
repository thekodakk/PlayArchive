using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlayArchive.Models;
using PlayArchive.Services;

namespace PlayArchive.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GameDatabaseService _dbService;
        private readonly ProcessTrackerService _trackerService;

        [ObservableProperty]
        private ObservableObject _currentViewModel = null!;

        [ObservableProperty]
        private string _activeTabName = "Dashboard";

        [ObservableProperty]
        private bool _isLiveTrackingActive;

        [ObservableProperty]
        private string _nowPlayingTitle = "None";

        [ObservableProperty]
        private bool _isNowPlaying;

        [ObservableProperty]
        private string _nowPlayingStatusText = "System Idle";

        public ObservableCollection<TrackerLogEvent> RecentLogs { get; } = new();

        public DashboardViewModel DashboardVM { get; }
        public LibraryViewModel LibraryVM { get; }
        public SessionsViewModel SessionsVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public IRelayCommand NavigateDashboardCommand { get; }
        public IRelayCommand NavigateLibraryCommand { get; }
        public IRelayCommand NavigateSessionsCommand { get; }
        public IRelayCommand NavigateSettingsCommand { get; }
        public IRelayCommand ToggleTrackingCommand { get; }
        public IRelayCommand AddGameCommand { get; }

        public MainViewModel(
            GameDatabaseService dbService,
            ProcessTrackerService trackerService,
            DashboardViewModel dashboardVM,
            LibraryViewModel libraryVM,
            SessionsViewModel sessionsVM,
            SettingsViewModel settingsVM)
        {
            _dbService = dbService;
            _trackerService = trackerService;

            DashboardVM = dashboardVM;
            LibraryVM = libraryVM;
            SessionsVM = sessionsVM;
            SettingsVM = settingsVM;

            CurrentViewModel = DashboardVM;

            NavigateDashboardCommand = new RelayCommand(() => { CurrentViewModel = DashboardVM; ActiveTabName = "Dashboard"; });
            NavigateLibraryCommand = new RelayCommand(() => { CurrentViewModel = LibraryVM; ActiveTabName = "Library"; _ = LibraryVM.LoadGamesAsync(); });
            NavigateSessionsCommand = new RelayCommand(() => { CurrentViewModel = SessionsVM; ActiveTabName = "Sessions"; _ = SessionsVM.LoadSessionsAsync(); });
            NavigateSettingsCommand = new RelayCommand(() => { CurrentViewModel = SettingsVM; ActiveTabName = "Settings"; });

            ToggleTrackingCommand = new RelayCommand(ToggleTracking);
            AddGameCommand = new RelayCommand(OpenAddGameModal);

            // Wire up tracker service events
            _trackerService.LogEventOccurred += OnTrackerLog;
            _trackerService.GameStarted += OnGameStarted;
            _trackerService.GameStopped += OnGameStopped;
            _trackerService.TrackingStateChanged += (s, state) => IsLiveTrackingActive = state;

            // Start background tracking engine automatically
            _trackerService.Start();
            IsLiveTrackingActive = _trackerService.IsRunning;
        }

        private void ToggleTracking()
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

        private void OpenAddGameModal()
        {
            LibraryVM.OpenAddGameDialog();
            CurrentViewModel = LibraryVM;
            ActiveTabName = "Library";
        }

        private void OnTrackerLog(object? sender, TrackerLogEvent e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RecentLogs.Insert(0, e);
                if (RecentLogs.Count > 50)
                {
                    RecentLogs.RemoveAt(RecentLogs.Count - 1);
                }
            });
        }

        private void OnGameStarted(object? sender, ActiveTrackedSession session)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsNowPlaying = true;
                NowPlayingTitle = session.GameTitle;
                NowPlayingStatusText = $"Playing since {session.StartTime:HH:mm}";

                // Refresh views
                _ = DashboardVM.RefreshDataAsync();
                _ = LibraryVM.LoadGamesAsync();
                _ = SessionsVM.LoadSessionsAsync();
            });
        }

        private void OnGameStopped(object? sender, ActiveTrackedSession session)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsNowPlaying = false;
                NowPlayingTitle = "None";
                NowPlayingStatusText = "System Idle";

                // Refresh views
                _ = DashboardVM.RefreshDataAsync();
                _ = LibraryVM.LoadGamesAsync();
                _ = SessionsVM.LoadSessionsAsync();
            });
        }
    }
}
