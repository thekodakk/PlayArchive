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
    public partial class SessionsViewModel : ObservableObject
    {
        private readonly GameDatabaseService _dbService;
        private List<GameSession> _allSessions = new();

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private string _totalSessionHoursText = "0 hrs";

        [ObservableProperty]
        private int _totalSessionsCount;

        public ObservableCollection<GameSession> FilteredSessions { get; } = new();

        public IAsyncRelayCommand LoadSessionsCommand { get; }

        public SessionsViewModel(GameDatabaseService dbService)
        {
            _dbService = dbService;
            LoadSessionsCommand = new AsyncRelayCommand(LoadSessionsAsync);
            _ = LoadSessionsAsync();
        }

        public async Task LoadSessionsAsync()
        {
            try
            {
                var sessions = await _dbService.GetRecentSessionsAsync(100);
                _allSessions = sessions ?? new List<GameSession>();
                TotalSessionsCount = _allSessions.Count;

                double totalMinutes = _allSessions.Sum(s => s?.DurationMinutes ?? 0);
                TimeSpan ts = TimeSpan.FromMinutes(totalMinutes);
                TotalSessionHoursText = ts.TotalHours >= 1 ? $"{ts.TotalHours:F1} Hours" : $"{ts.TotalMinutes:F0} Mins";

                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionsViewModel] Error loading sessions: {ex.Message}");
                _allSessions = new List<GameSession>();
                FilteredSessions.Clear();
                TotalSessionsCount = 0;
                TotalSessionHoursText = "0 hrs";
            }
        }

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            try
            {
                FilteredSessions.Clear();
                if (_allSessions == null) return;

                var query = _allSessions.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(FilterText))
                {
                    string text = FilterText.Trim();
                    query = query.Where(s => s != null && (
                        (s.GameItem != null && !string.IsNullOrEmpty(s.GameItem.Title) && s.GameItem.Title.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(s.Notes) && s.Notes.Contains(text, StringComparison.OrdinalIgnoreCase))
                    ));
                }

                foreach (var session in query)
                {
                    if (session != null)
                    {
                        FilteredSessions.Add(session);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionsViewModel] Error applying filter: {ex.Message}");
            }
        }
    }
}
