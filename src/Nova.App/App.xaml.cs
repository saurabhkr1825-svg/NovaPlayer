using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nova.App.ViewModels;
using Nova.App.Views;
using Nova.Core.Interfaces;
using Nova.Infrastructure.Downloads;
using Nova.Infrastructure.Logging;
using Nova.Infrastructure.Persistence;
using Nova.Infrastructure.Playback;
using Nova.Infrastructure.Playlists;
using Nova.Infrastructure.Scanner;
using Serilog;

namespace Nova.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Initialize Logging
        LoggingConfigurator.ConfigureLogging();
        Log.Information("Starting NovaPlayer application.");

        // 2. Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 3. Setup Dependency Injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 4. Initialize Database
        try
        {
            var libraryRepo = _serviceProvider.GetRequiredService<ILibraryRepository>();
            var initResult = libraryRepo.InitializeAsync().GetAwaiter().GetResult();
            if (initResult.IsFailure)
            {
                Log.Error("Failed to initialize SQLite database: {Error}", initResult.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unexpected error during database initialization.");
        }

        // 5. Resolve and Show Main Window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 6. If media file path argument is passed, auto-play
        if (e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0]))
        {
            var targetPath = e.Args[0];
            Log.Information("Command-line argument received: {Path}", targetPath);
            if (mainWindow.DataContext is PlayerViewModel vm)
            {
                _ = vm.PlayFileAsync(targetPath);
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Playback Service
        services.AddSingleton<IPlaybackService, LibVlcPlaybackService>();

        // Persistence & Scanner Services
        services.AddSingleton<DatabaseContext>(_ => new DatabaseContext());
        services.AddSingleton<ILibraryRepository, SqliteLibraryRepository>();
        services.AddSingleton<IMediaScanner, MediaScanner>();
        services.AddSingleton<IPlaylistService, M3uPlaylistService>();

        // Download Services
        services.AddSingleton<IDownloadRepository, SqliteDownloadRepository>();
        services.AddSingleton<IDownloadService, ResumableDownloadService>();

        // ViewModels
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<DownloadsViewModel>();
        services.AddTransient<PlayerViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI Dispatcher exception encountered.");
        MessageBox.Show($"An unexpected UI error occurred: {e.Exception.Message}\n\nPlease check logs in %LOCALAPPDATA%\\NovaPlayer\\logs for details.",
            "NovaPlayer Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled AppDomain exception encountered. IsTerminating={IsTerminating}", e.IsTerminating);
        }
        else
        {
            Log.Fatal("Unhandled non-exception object encountered in AppDomain: {Object}", e.ExceptionObject);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved Task exception encountered.");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("NovaPlayer exiting with code {ExitCode}.", e.ApplicationExitCode);
        _serviceProvider?.Dispose();
        LoggingConfigurator.CloseAndFlush();
        base.OnExit(e);
    }
}
