using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlayArchive.Models;
using PlayArchive.Services;

namespace PlayArchive.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly GameDatabaseService _dbService;
        private readonly ProcessTrackerService _trackerService;

        [ObservableProperty]
        private int _totalGamesCount;

        [ObservableProperty]
        private string _totalPlayTimeFormatted = "0 hrs";

        [ObservableProperty]
        private int _totalSessionsCount;

        [ObservableProperty]
        private string _mostPlayedGameTitle = "N/A";

        [ObservableProperty]
        private bool _isNowPlaying;

        [ObservableProperty]
        private string _activeGameTitle = "None";

        public ObservableCollection<GameItem> FeaturedGames { get; } = new();
        public ObservableCollection<GameSession> RecentSessions { get; } = new();

        public IAsyncRelayCommand RefreshCommand { get; }

        public DashboardViewModel(GameDatabaseService dbService, ProcessTrackerService trackerService)
        {
            _dbService = dbService;
            _trackerService = trackerService;

            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
            _ = RefreshDataAsync();
        }

        public async Task RefreshDataAsync()
        {
            var games = await _dbService.GetAllGamesAsync();
            TotalGamesCount = games.Count;

            double totalMinutes = games.Sum(g => g.TotalPlayTimeMinutes);
            TimeSpan ts = TimeSpan.FromMinutes(totalMinutes);
            TotalPlayTimeFormatted = ts.TotalHours >= 1 ? $"{ts.TotalHours:F1} Hours" : $"{ts.TotalMinutes:F0} Mins";

            var topGame = games.OrderByDescending(g => g.TotalPlayTimeMinutes).FirstOrDefault();
            MostPlayedGameTitle = topGame != null ? $"{topGame.Title} ({topGame.FormattedTotalPlayTime})" : "N/A";

            FeaturedGames.Clear();
            foreach (var game in games.Take(4))
            {
                FeaturedGames.Add(game);
            }

            var recent = await _dbService.GetRecentSessionsAsync(6);
            TotalSessionsCount = recent.Count;
            RecentSessions.Clear();
            foreach (var session in recent)
            {
                RecentSessions.Add(session);
            }

            var active = _trackerService.ActiveSessions.Values.FirstOrDefault();
            if (active != null)
            {
                IsNowPlaying = true;
                ActiveGameTitle = active.GameTitle;
            }
            else
            {
                IsNowPlaying = false;
                ActiveGameTitle = "None";
            }
        }
    }
}
