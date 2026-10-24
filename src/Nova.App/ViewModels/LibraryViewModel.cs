using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.App.ViewModels;

public partial class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly ILibraryRepository _libraryRepository;
    private readonly IMediaScanner _mediaScanner;
    private readonly IPlaylistService _playlistService;
    private readonly Dispatcher _dispatcher;

    private CancellationTokenSource? _scanCts;
    private List<MediaItem> _allLoadedMediaItems = new();

    public event EventHandler<string>? PlayMediaRequested;
    public event EventHandler<(IReadOnlyList<string> items, int startIndex)>? PlayQueueRequested;

    [ObservableProperty]
    private ObservableCollection<MediaItem> _mediaItems = new();

    [ObservableProperty]
    private ObservableCollection<Playlist> _playlists = new();

    [ObservableProperty]
    private ObservableCollection<PlaylistItem> _playlistItems = new();

    [ObservableProperty]
    private ObservableCollection<PlaybackHistoryEntry> _playbackHistory = new();

    [ObservableProperty]
    private MediaItem? _selectedMediaItem;

    [ObservableProperty]
    private Playlist? _selectedPlaylist;

    [ObservableProperty]
    private PlaylistItem? _selectedPlaylistItem;

    [ObservableProperty]
    private PlaybackHistoryEntry? _selectedHistoryEntry;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = string.Empty;

    [ObservableProperty]
    private int _scanProgressPercentage;

    [ObservableProperty]
    private string? _currentScanFileName;

    [ObservableProperty]
    private string? _statusMessage;

    public static string[] FilterOptions { get; } = ["All", "Video", "Audio", "Favorites"];

    public LibraryViewModel(
        ILibraryRepository libraryRepository,
        IMediaScanner mediaScanner,
        IPlaylistService playlistService)
    {
        _libraryRepository = libraryRepository;
        _mediaScanner = mediaScanner;
        _playlistService = playlistService;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public async Task InitializeAsync()
    {
        await LoadAllAsync();
    }

    [RelayCommand]
    public async Task LoadAllAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading library...";
        try
        {
            await LoadMediaItemsAsync();
            await LoadPlaylistsAsync();
            await LoadHistoryAsync();
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load library data");
            StatusMessage = $"Failed to load library: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadMediaItemsAsync()
    {
        var result = await _libraryRepository.GetAllMediaItemsAsync();
        if (result.IsSuccess && result.Value != null)
        {
            _allLoadedMediaItems = result.Value.ToList();
            ApplyFilterAndSearch();
        }
    }

    [RelayCommand]
    public async Task LoadPlaylistsAsync()
    {
        var result = await _libraryRepository.GetAllPlaylistsAsync();
        if (result.IsSuccess && result.Value != null)
        {
            Playlists.Clear();
            foreach (var pl in result.Value)
            {
                Playlists.Add(pl);
            }
        }
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var result = await _libraryRepository.GetRecentHistoryAsync(100);
        if (result.IsSuccess && result.Value != null)
        {
            PlaybackHistory.Clear();
            foreach (var h in result.Value)
            {
                PlaybackHistory.Add(h);
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilterAndSearch();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        ApplyFilterAndSearch();
    }

    private void ApplyFilterAndSearch()
    {
        IEnumerable<MediaItem> query = _allLoadedMediaItems;

        // 1. Category / Favorite filter
        if (SelectedFilter == "Video")
        {
            var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp4", ".mkv", ".avi", ".webm", ".mov", ".wmv", ".flv", ".ts", ".m4v"
            };
            query = query.Where(m => videoExts.Contains(Path.GetExtension(m.FilePathOrUrl)));
        }
        else if (SelectedFilter == "Audio")
        {
            var audioExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".opus", ".wma", ".aac"
            };
            query = query.Where(m => audioExts.Contains(Path.GetExtension(m.FilePathOrUrl)));
        }
        else if (SelectedFilter == "Favorites")
        {
            query = query.Where(m => m.IsFavorite);
        }

        // 2. Search query filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(m =>
                m.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.FilePathOrUrl.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        MediaItems.Clear();
        foreach (var item in query)
        {
            MediaItems.Add(item);
        }
    }

    [RelayCommand]
    public async Task ScanFolderDialogAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder to Scan for Media — NovaPlayer",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            await ScanFolderAsync(dialog.FolderName);
        }
    }

    public async Task ScanFolderAsync(string folderPath, bool recursive = true)
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanProgressPercentage = 0;
        ScanStatus = "Discovering files...";
        CurrentScanFileName = null;
        _scanCts = new CancellationTokenSource();

        var progress = new Progress<ScanProgress>(p =>
        {
            _dispatcher.InvokeAsync(() =>
            {
                if (p.TotalFilesDiscovered > 0)
                {
                    ScanProgressPercentage = (int)((double)p.FilesProcessed / p.TotalFilesDiscovered * 100);
                    ScanStatus = $"Processing {p.FilesProcessed} of {p.TotalFilesDiscovered}...";
                }
                else
                {
                    ScanStatus = "Scanning directory...";
                }
                CurrentScanFileName = p.CurrentFileName;
            });
        });

        try
        {
            var result = await _mediaScanner.ScanFolderAsync(folderPath, recursive, progress, _scanCts.Token);
            if (result.IsSuccess)
            {
                StatusMessage = $"Scan completed: {result.Value} media items updated/added.";
            }
            else
            {
                StatusMessage = result.Error;
            }

            // Reload media items after scan
            await LoadMediaItemsAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled by user.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Scan folder failed: {FolderPath}", folderPath);
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            CurrentScanFileName = null;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        if (IsScanning && _scanCts != null && !_scanCts.IsCancellationRequested)
        {
            _scanCts.Cancel();
            ScanStatus = "Cancelling scan...";
        }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync(MediaItem? item)
    {
        if (item == null) return;

        item.IsFavorite = !item.IsFavorite;
        var result = await _libraryRepository.AddOrUpdateMediaItemAsync(item);
        if (result.IsSuccess)
        {
            // If filtering by favorites, refresh the list
            if (SelectedFilter == "Favorites")
            {
                ApplyFilterAndSearch();
            }
        }
        else
        {
            // Revert state on failure
            item.IsFavorite = !item.IsFavorite;
            StatusMessage = $"Failed to update favorite: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task DeleteMediaItemAsync(MediaItem? item)
    {
        if (item == null) return;

        var result = await _libraryRepository.DeleteMediaItemAsync(item.Id);
        if (result.IsSuccess)
        {
            _allLoadedMediaItems.RemoveAll(m => m.Id == item.Id);
            MediaItems.Remove(item);
            StatusMessage = $"Removed '{item.Title}' from library.";
        }
        else
        {
            StatusMessage = $"Failed to delete item: {result.Error}";
        }
    }

    [RelayCommand]
    public void OpenFileLocation(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusMessage = "File does not exist or cannot be located.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open file location {Path}", filePath);
            StatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    public void PlayMediaItem(MediaItem? item)
    {
        if (item != null)
        {
            PlayMediaRequested?.Invoke(this, item.FilePathOrUrl);
        }
    }

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        if (value != null)
        {
            _ = LoadPlaylistItemsAsync(value.Id);
        }
        else
        {
            PlaylistItems.Clear();
        }
    }

    [RelayCommand]
    public async Task LoadPlaylistItemsAsync(string playlistId)
    {
        var result = await _libraryRepository.GetPlaylistItemsAsync(playlistId);
        if (result.IsSuccess && result.Value != null)
        {
            PlaylistItems.Clear();
            foreach (var item in result.Value)
            {
                PlaylistItems.Add(item);
            }
        }
    }

    [RelayCommand]
    public async Task CreatePlaylistAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Playlist {Playlists.Count + 1}";
        }

        var playlistName = name.Trim();
        var result = await _libraryRepository.CreatePlaylistAsync(playlistName);
        if (result.IsSuccess)
        {
            Playlists.Add(result.Value);
            SelectedPlaylist = result.Value;
            StatusMessage = $"Created playlist '{playlistName}'.";
        }
        else
        {
            StatusMessage = $"Failed to create playlist: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task DeletePlaylistAsync(Playlist? playlist)
    {
        if (playlist == null) return;

        var result = await _libraryRepository.DeletePlaylistAsync(playlist.Id);
        if (result.IsSuccess)
        {
            Playlists.Remove(playlist);
            if (SelectedPlaylist?.Id == playlist.Id)
            {
                SelectedPlaylist = Playlists.FirstOrDefault();
            }
            StatusMessage = $"Deleted playlist '{playlist.Name}'.";
        }
        else
        {
            StatusMessage = $"Failed to delete playlist: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task RenamePlaylistAsync((Playlist playlist, string newName) args)
    {
        if (args.playlist == null || string.IsNullOrWhiteSpace(args.newName)) return;

        var result = await _libraryRepository.RenamePlaylistAsync(args.playlist.Id, args.newName.Trim());
        if (result.IsSuccess)
        {
            args.playlist.Name = args.newName.Trim();
            StatusMessage = $"Renamed playlist to '{args.playlist.Name}'.";
        }
        else
        {
            StatusMessage = $"Failed to rename playlist: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task AddMediaToSelectedPlaylistAsync(MediaItem? item)
    {
        if (SelectedPlaylist == null || item == null) return;

        var result = await _libraryRepository.AddItemToPlaylistAsync(SelectedPlaylist.Id, item.Id);
        if (result.IsSuccess)
        {
            await LoadPlaylistItemsAsync(SelectedPlaylist.Id);
            StatusMessage = $"Added '{item.Title}' to playlist '{SelectedPlaylist.Name}'.";
        }
        else
        {
            StatusMessage = $"Failed to add to playlist: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task RemovePlaylistItemAsync(PlaylistItem? item)
    {
        if (SelectedPlaylist == null || item == null) return;

        var result = await _libraryRepository.RemoveItemFromPlaylistAsync(SelectedPlaylist.Id, item.Id);
        if (result.IsSuccess)
        {
            await LoadPlaylistItemsAsync(SelectedPlaylist.Id);
        }
        else
        {
            StatusMessage = $"Failed to remove item: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task MovePlaylistItemUpAsync(PlaylistItem? item)
    {
        if (SelectedPlaylist == null || item == null) return;

        int index = PlaylistItems.IndexOf(item);
        if (index > 0)
        {
            var itemsList = PlaylistItems.Select(x => x.Id).ToList();
            itemsList.RemoveAt(index);
            itemsList.Insert(index - 1, item.Id);

            var result = await _libraryRepository.ReorderPlaylistItemsAsync(SelectedPlaylist.Id, itemsList);
            if (result.IsSuccess)
            {
                await LoadPlaylistItemsAsync(SelectedPlaylist.Id);
                SelectedPlaylistItem = PlaylistItems.ElementAtOrDefault(index - 1);
            }
        }
    }

    [RelayCommand]
    public async Task MovePlaylistItemDownAsync(PlaylistItem? item)
    {
        if (SelectedPlaylist == null || item == null) return;

        int index = PlaylistItems.IndexOf(item);
        if (index >= 0 && index < PlaylistItems.Count - 1)
        {
            var itemsList = PlaylistItems.Select(x => x.Id).ToList();
            itemsList.RemoveAt(index);
            itemsList.Insert(index + 1, item.Id);

            var result = await _libraryRepository.ReorderPlaylistItemsAsync(SelectedPlaylist.Id, itemsList);
            if (result.IsSuccess)
            {
                await LoadPlaylistItemsAsync(SelectedPlaylist.Id);
                SelectedPlaylistItem = PlaylistItems.ElementAtOrDefault(index + 1);
            }
        }
    }

    [RelayCommand]
    public async Task ShufflePlaylistAsync(Playlist? playlist)
    {
        if (playlist == null || PlaylistItems.Count <= 1) return;

        var shuffled = _playlistService.Shuffle(PlaylistItems.ToList());
        var orderedIds = shuffled.Select(x => x.Id).ToList();

        var result = await _libraryRepository.ReorderPlaylistItemsAsync(playlist.Id, orderedIds);
        if (result.IsSuccess)
        {
            await LoadPlaylistItemsAsync(playlist.Id);
            StatusMessage = $"Playlist '{playlist.Name}' shuffled.";
        }
    }

    [RelayCommand]
    public async Task ImportPlaylistDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Playlist — NovaPlayer",
            Filter = "M3U Playlist (*.m3u;*.m3u8)|*.m3u;*.m3u8|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var result = await _playlistService.ImportM3uAsync(dialog.FileName);
            if (result.IsSuccess && result.Value != null)
            {
                await LoadPlaylistsAsync();
                SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == result.Value.Id);
                StatusMessage = $"Imported playlist '{result.Value.Name}'.";
            }
            else
            {
                StatusMessage = $"Import failed: {result.Error}";
            }
        }
    }

    [RelayCommand]
    public async Task ExportPlaylistDialogAsync(Playlist? playlist)
    {
        if (playlist == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Playlist — NovaPlayer",
            Filter = "Extended M3U8 Playlist (*.m3u8)|*.m3u8|M3U Playlist (*.m3u)|*.m3u",
            FileName = $"{playlist.Name}.m3u8"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = await _playlistService.ExportM3uAsync(playlist.Id, dialog.FileName);
            if (result.IsSuccess)
            {
                StatusMessage = $"Playlist exported to {dialog.FileName}.";
            }
            else
            {
                StatusMessage = $"Export failed: {result.Error}";
            }
        }
    }

    [RelayCommand]
    public void PlayPlaylist(Playlist? playlist)
    {
        if (playlist == null || PlaylistItems.Count == 0) return;

        var paths = PlaylistItems
            .Where(pi => pi.MediaItem != null && !string.IsNullOrWhiteSpace(pi.MediaItem.FilePathOrUrl))
            .Select(pi => pi.MediaItem!.FilePathOrUrl)
            .ToList();

        if (paths.Count > 0)
        {
            PlayQueueRequested?.Invoke(this, (paths, 0));
        }
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        var result = await _libraryRepository.ClearHistoryAsync();
        if (result.IsSuccess)
        {
            PlaybackHistory.Clear();
            StatusMessage = "Playback history cleared.";
        }
        else
        {
            StatusMessage = $"Failed to clear history: {result.Error}";
        }
    }

    [RelayCommand]
    public async Task DeleteHistoryItemAsync(PlaybackHistoryEntry? entry)
    {
        if (entry == null) return;

        var result = await _libraryRepository.DeleteHistoryEntryAsync(entry.Id);
        if (result.IsSuccess)
        {
            PlaybackHistory.Remove(entry);
        }
    }

    [RelayCommand]
    public void PlayHistoryItem(PlaybackHistoryEntry? entry)
    {
        if (entry != null && !string.IsNullOrWhiteSpace(entry.FilePathOrUrl))
        {
            PlayMediaRequested?.Invoke(this, entry.FilePathOrUrl);
        }
    }

    public void Dispose()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
    }
}
