using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlayArchive.Models;
using PlayArchive.Services;

namespace PlayArchive.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        private readonly GameDatabaseService _dbService;
        private readonly GameScannerService _scannerService;
        private List<GameItem> _allGames = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isAddDialogOpen;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _scanStatusText = "Auto-detect installed Steam & PC Games";

        [ObservableProperty]
        private AddGameViewModel? _addGameVM;

        [ObservableProperty]
        private GameItem? _selectedGame;

        public ObservableCollection<GameItem> FilteredGames { get; } = new();

        public IAsyncRelayCommand LoadGamesCommand { get; }
        public IAsyncRelayCommand ScanInstalledGamesCommand { get; }
        public IRelayCommand OpenAddGameCommand { get; }
        public IAsyncRelayCommand<GameItem> ToggleTrackingCommand { get; }
        public IAsyncRelayCommand<GameItem> DeleteGameCommand { get; }

        public LibraryViewModel(GameDatabaseService dbService, GameScannerService scannerService)
        {
            _dbService = dbService;
            _scannerService = scannerService;

            LoadGamesCommand = new AsyncRelayCommand(LoadGamesAsync);
            ScanInstalledGamesCommand = new AsyncRelayCommand(ScanInstalledGamesAsync);
            OpenAddGameCommand = new RelayCommand(OpenAddGameDialog);
            ToggleTrackingCommand = new AsyncRelayCommand<GameItem>(ToggleTrackingAsync);
            DeleteGameCommand = new AsyncRelayCommand<GameItem>(DeleteGameAsync);

            _scannerService.ScanProgress += (s, msg) => ScanStatusText = msg;

            _ = LoadGamesAsync();
        }

        public async Task LoadGamesAsync()
        {
            _allGames = await _dbService.GetAllGamesAsync();
            ApplyFilter();
        }

        public async Task ScanInstalledGamesAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            ScanStatusText = "Scanning Steam & PC Libraries...";

            try
            {
                int count = await _scannerService.ScanAndImportGamesAsync();
                ScanStatusText = count > 0 ? $"Success! Detected and added {count} new games." : "Scan finished. No new unadded games found.";
                await LoadGamesAsync();
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Scan Error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredGames.Clear();
            var query = _allGames.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string s = SearchText.Trim();
                query = query.Where(g => g.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                         g.ExecutableName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                         g.Category.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var g in query)
            {
                FilteredGames.Add(g);
            }
        }

        public void OpenAddGameDialog()
        {
            AddGameVM = new AddGameViewModel(_dbService);
            AddGameVM.RequestClose += (s, e) => IsAddDialogOpen = false;
            AddGameVM.GameAdded += async (s, newGame) =>
            {
                await LoadGamesAsync();
            };
            IsAddDialogOpen = true;
        }

        private async Task ToggleTrackingAsync(GameItem? game)
        {
            if (game == null) return;
            game.IsTrackingEnabled = !game.IsTrackingEnabled;
            await _dbService.UpdateGameAsync(game);
            await LoadGamesAsync();
        }

        private async Task DeleteGameAsync(GameItem? game)
        {
            if (game == null) return;
            await _dbService.DeleteGameAsync(game.Id);
            await LoadGamesAsync();
        }
    }
}
