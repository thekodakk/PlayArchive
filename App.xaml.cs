using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PlayArchive.Data;
using PlayArchive.Services;
using PlayArchive.ViewModels;
using PlayArchive.Views;

namespace PlayArchive
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            // Initialize SQLite DB
            var dbService = _serviceProvider.GetRequiredService<GameDatabaseService>();
            await dbService.InitializeDatabaseAsync();

            // Create and show MainWindow
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Auto-scan installed games in background on startup
            var scanner = _serviceProvider.GetRequiredService<GameScannerService>();
            _ = scanner.ScanAndImportGamesAsync().ContinueWith(async t =>
            {
                if (t.IsCompletedSuccessfully && t.Result > 0)
                {
                    var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
                    await mainVM.DashboardVM.RefreshDataAsync();
                    await mainVM.LibraryVM.LoadGamesAsync();
                }
            });
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Data Context & Services
            services.AddDbContext<PlayArchiveDbContext>();
            services.AddSingleton<GameDatabaseService>();
            services.AddSingleton<ProcessTrackerService>();
            services.AddSingleton<GameScannerService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<SessionsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AddGameViewModel>();

            // Views
            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var tracker = _serviceProvider?.GetService<ProcessTrackerService>();
            tracker?.Stop();

            base.OnExit(e);
        }
    }
}
