using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.Core.Interfaces;

public interface ILibraryRepository
{
    Task<Result> InitializeAsync();

    // Media Items
    Task<Result<MediaItem>> AddOrUpdateMediaItemAsync(MediaItem item);
    Task<Result<int>> AddMediaItemsBatchAsync(IEnumerable<MediaItem> items);
    Task<Result<IReadOnlyList<MediaItem>>> GetAllMediaItemsAsync(string? searchQuery = null);
    Task<Result<MediaItem?>> GetMediaItemByPathAsync(string path);
    Task<Result<MediaItem?>> GetMediaItemByIdAsync(string id);
    Task<Result> DeleteMediaItemAsync(string id);

    // Playlists
    Task<Result<IReadOnlyList<Playlist>>> GetAllPlaylistsAsync();
    Task<Result<Playlist>> CreatePlaylistAsync(string name);
    Task<Result> RenamePlaylistAsync(string playlistId, string newName);
    Task<Result> DeletePlaylistAsync(string playlistId);
    Task<Result<PlaylistItem>> AddItemToPlaylistAsync(string playlistId, string mediaItemId);
    Task<Result> RemoveItemFromPlaylistAsync(string playlistId, string playlistItemId);
    Task<Result> ReorderPlaylistItemsAsync(string playlistId, IReadOnlyList<string> orderedPlaylistItemIds);
    Task<Result<IReadOnlyList<PlaylistItem>>> GetPlaylistItemsAsync(string playlistId);

    // Playback History & Resume
    Task<Result> RecordPlaybackHistoryAsync(string filePathOrUrl, string title, long positionMs, long durationMs);
    Task<Result<PlaybackHistoryEntry?>> GetResumePointAsync(string filePathOrUrl);
    Task<Result<IReadOnlyList<PlaybackHistoryEntry>>> GetRecentHistoryAsync(int limit = 50);
    Task<Result> ClearHistoryAsync();
    Task<Result> DeleteHistoryEntryAsync(string id);

    // Key-Value App Settings
    Task<Result> SetSettingAsync(string key, string value);
    Task<Result<string?>> GetSettingAsync(string key, string? defaultValue = null);
}
