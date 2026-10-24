using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nova.App.ViewModels;
using Nova.App.Views;
using Nova.Core.Interfaces;
using Nova.Infrastructure.Logging;
using Nova.Infrastructure.Playback;
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

        // 4. Resolve and Show Main Window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 5. If media file path argument is passed, auto-play
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
        // Core Services
        services.AddSingleton<IPlaybackService, LibVlcPlaybackService>();

        // ViewModels
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
