using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.App.ViewModels;

public partial class DownloadsViewModel : ObservableObject, IDisposable
{
    private readonly IDownloadService _downloadService;
    private readonly ILibraryRepository? _libraryRepository;
    private bool _isDisposed;

    public event EventHandler<string>? PlayMediaRequested;
    public event Action? RequestAddDownloadDialog;

    [ObservableProperty]
    private ObservableCollection<DownloadItemViewModel> _downloads = new();

    [ObservableProperty]
    private DownloadItemViewModel? _selectedDownload;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    public static string[] FilterOptions { get; } = ["All", "Active", "Completed"];

    public DownloadsViewModel(IDownloadService downloadService, ILibraryRepository? libraryRepository = null)
    {
        _downloadService = downloadService;
        _libraryRepository = libraryRepository;

        _downloadService.ProgressChanged += OnDownloadProgressChanged;
        _downloadService.StateChanged += OnDownloadStateChanged;
    }

    public async Task InitializeAsync()
    {
        await _downloadService.InitializeAsync();

        RunOnUi(() =>
        {
            Downloads.Clear();
            foreach (var item in _downloadService.Downloads)
            {
                Downloads.Add(new DownloadItemViewModel(item));
            }
        });
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var view = CollectionViewSource.GetDefaultView(Downloads);
        if (view == null) return;

        view.Filter = obj =>
        {
            if (obj is not DownloadItemViewModel item) return false;

            // Search text filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var matchesName = item.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                var matchesUrl = item.Url.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                if (!matchesName && !matchesUrl) return false;
            }

            // Category filter
            return SelectedFilter switch
            {
                "Active" => item.State is DownloadState.Downloading or DownloadState.Queued or DownloadState.Paused,
                "Completed" => item.State == DownloadState.Completed,
                _ => true
            };
        };

        view.Refresh();
    }

    [RelayCommand]
    public void OpenAddDownloadDialog()
    {
        RequestAddDownloadDialog?.Invoke();
    }

    public async Task AddDownloadAsync(string url, string? destDir = null, string? customName = null)
    {
        var result = await _downloadService.EnqueueDownloadAsync(url, destDir, customName);
        if (result.IsSuccess)
        {
            var newItem = result.Value;
            RunOnUi(() =>
            {
                var vm = new DownloadItemViewModel(newItem);
                Downloads.Insert(0, vm);
                SelectedDownload = vm;
                ApplyFilter();
            });
        }
    }

    [RelayCommand]
    public async Task PauseDownloadAsync(DownloadItemViewModel? item)
    {
        if (item == null) return;
        await _downloadService.PauseDownloadAsync(item.Id);
    }

    [RelayCommand]
    public async Task ResumeDownloadAsync(DownloadItemViewModel? item)
    {
        if (item == null) return;
        await _downloadService.ResumeDownloadAsync(item.Id);
    }

    [RelayCommand]
    public async Task CancelDownloadAsync(DownloadItemViewModel? item)
    {
        if (item == null) return;
        await _downloadService.CancelDownloadAsync(item.Id);
    }

    [RelayCommand]
    public async Task RetryDownloadAsync(DownloadItemViewModel? item)
    {
        if (item == null) return;
        await _downloadService.RetryDownloadAsync(item.Id);
    }

    [RelayCommand]
    public async Task RemoveDownloadAsync(DownloadItemViewModel? item)
    {
        if (item == null) return;
        await _downloadService.RemoveDownloadAsync(item.Id, deleteFile: false);
        RunOnUi(() =>
        {
            Downloads.Remove(item);
        });
    }

    [RelayCommand]
    public async Task DeleteDownloadWithFileAsync(DownloadItemViewModel? item)
    {
        if (item == null) return;
        await _downloadService.RemoveDownloadAsync(item.Id, deleteFile: true);
        RunOnUi(() =>
        {
            Downloads.Remove(item);
        });
    }

    [RelayCommand]
    public async Task ClearCompletedAsync()
    {
        var completed = Downloads.Where(d => d.State == DownloadState.Completed).ToList();
        foreach (var item in completed)
        {
            await _downloadService.RemoveDownloadAsync(item.Id, deleteFile: false);
            RunOnUi(() => Downloads.Remove(item));
        }
    }

    [RelayCommand]
    public void OpenFile(DownloadItemViewModel? item)
    {
        if (item == null || !File.Exists(item.DestinationPath)) return;

        // If media file, play directly in NovaPlayer!
        PlayMediaRequested?.Invoke(this, item.DestinationPath);
    }

    [RelayCommand]
    public void OpenContainingFolder(DownloadItemViewModel? item)
    {
        if (item == null) return;

        var target = File.Exists(item.DestinationPath)
            ? item.DestinationPath
            : (File.Exists(item.TempFilePath) ? item.TempFilePath : Path.GetDirectoryName(item.DestinationPath));

        if (string.IsNullOrEmpty(target) || (!File.Exists(target) && !Directory.Exists(target))) return;

        try
        {
            if (File.Exists(target))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open containing folder for {Path}", target);
        }
    }

    private void OnDownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        RunOnUi(() =>
        {
            var item = Downloads.FirstOrDefault(d => d.Id == e.DownloadId);
            item?.UpdateProgress(e.BytesDownloaded, e.TotalBytes, e.BytesPerSecond, e.EstimatedRemaining);
        });
    }

    private void OnDownloadStateChanged(object? sender, DownloadStateChangedEventArgs e)
    {
        RunOnUi(() =>
        {
            var item = Downloads.FirstOrDefault(d => d.Id == e.DownloadId);
            if (item != null)
            {
                item.UpdateState(e.NewState, e.ErrorMessage);
                ApplyFilter();
            }
            else if (e.NewState == DownloadState.Queued)
            {
                // New item added from engine
                var model = _downloadService.Downloads.FirstOrDefault(d => d.Id == e.DownloadId);
                if (model != null)
                {
                    Downloads.Insert(0, new DownloadItemViewModel(model));
                    ApplyFilter();
                }
            }
        });
    }

    private static void RunOnUi(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _downloadService.ProgressChanged -= OnDownloadProgressChanged;
        _downloadService.StateChanged -= OnDownloadStateChanged;
        _downloadService.Dispose();
    }
}
