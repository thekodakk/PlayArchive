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
    public partial class AddGameViewModel : ObservableObject
    {
        private readonly GameDatabaseService _dbService;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _executableName = string.Empty;

        [ObservableProperty]
        private string _category = "Action / RPG";

        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private RunningProcessInfo? _selectedProcess;

        public ObservableCollection<RunningProcessInfo> RunningProcesses { get; } = new();

        public IRelayCommand RefreshProcessesCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public event EventHandler? RequestClose;
        public event EventHandler<GameItem>? GameAdded;

        public AddGameViewModel(GameDatabaseService dbService)
        {
            _dbService = dbService;

            RefreshProcessesCommand = new RelayCommand(LoadRunningProcesses);
            SaveCommand = new AsyncRelayCommand(SaveGameAsync);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

            LoadRunningProcesses();
        }

        public void LoadRunningProcesses()
        {
            RunningProcesses.Clear();
            var processes = RunningProcessHelper.GetRunningProcesses();
            foreach (var proc in processes)
            {
                RunningProcesses.Add(proc);
            }
        }

        partial void OnSelectedProcessChanged(RunningProcessInfo? value)
        {
            if (value != null)
            {
                Title = string.IsNullOrWhiteSpace(value.MainWindowTitle) ? value.ProcessName : value.MainWindowTitle;
                ExecutableName = value.ExecutableName;
            }
        }

        private async Task SaveGameAsync()
        {
            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(ExecutableName))
            {
                return;
            }

            var game = new GameItem
            {
                Title = Title.Trim(),
                ExecutableName = ExecutableName.Trim(),
                Category = string.IsNullOrWhiteSpace(Category) ? "General" : Category.Trim(),
                Notes = Notes,
                IsTrackingEnabled = true,
                CreatedAt = DateTime.Now
            };

            await _dbService.AddGameAsync(game);
            GameAdded?.Invoke(this, game);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
