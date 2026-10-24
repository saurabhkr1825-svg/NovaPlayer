using System.Text;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.Infrastructure.Playlists;

public class M3uPlaylistService : IPlaylistService
{
    private readonly ILibraryRepository _libraryRepository;

    public M3uPlaylistService(ILibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository;
    }

    public async Task<Result<Playlist>> ImportM3uAsync(string m3uFilePath)
    {
        try
        {
            if (!File.Exists(m3uFilePath))
                return Result<Playlist>.Failure($"Playlist file not found: {m3uFilePath}");

            var playlistName = Path.GetFileNameWithoutExtension(m3uFilePath);
            var m3uDirectory = Path.GetDirectoryName(m3uFilePath) ?? string.Empty;

            var createResult = await _libraryRepository.CreatePlaylistAsync(playlistName);
            if (createResult.IsFailure)
                return Result<Playlist>.Failure(createResult.Error!);

            var playlist = createResult.Value;
            var lines = await File.ReadAllLinesAsync(m3uFilePath, Encoding.UTF8);

            string? pendingTitle = null;
            int order = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Handle #EXTINF:duration,Title
                if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                {
                    var commaIdx = line.IndexOf(',');
                    if (commaIdx >= 0 && commaIdx + 1 < line.Length)
                    {
                        pendingTitle = line[(commaIdx + 1)..].Trim();
                    }
                    continue;
                }

                // Skip other directives
                if (line.StartsWith('#')) continue;

                // Resolve path (support relative paths against M3U location)
                string fullPath;
                if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    fullPath = line;
                }
                else
                {
                    if (Path.IsPathRooted(line))
                    {
                        fullPath = line;
                    }
                    else
                    {
                        fullPath = Path.GetFullPath(Path.Combine(m3uDirectory, line));
                    }
                }

                // Add or update media item in repository
                var mediaItem = new MediaItem(fullPath, pendingTitle);
                var addMediaResult = await _libraryRepository.AddOrUpdateMediaItemAsync(mediaItem);
                if (addMediaResult.IsSuccess)
                {
                    await _libraryRepository.AddItemToPlaylistAsync(playlist.Id, addMediaResult.Value.Id);
                    order++;
                }

                pendingTitle = null;
            }

            Log.Information("Imported playlist {Name} with {Count} entries from {Path}", playlistName, order, m3uFilePath);
            return Result<Playlist>.Success(playlist);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to import M3U file {Path}", m3uFilePath);
            return Result<Playlist>.Failure($"Failed to import playlist: {ex.Message}");
        }
    }

    public async Task<Result> ExportM3uAsync(string playlistId, string targetFilePath, bool useRelativePaths = true)
    {
        try
        {
            var itemsResult = await _libraryRepository.GetPlaylistItemsAsync(playlistId);
            if (itemsResult.IsFailure)
                return Result.Failure(itemsResult.Error!);

            var items = itemsResult.Value;
            var targetDir = Path.GetDirectoryName(targetFilePath);

            var sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");

            foreach (var item in items)
            {
                if (item.MediaItem == null) continue;

                var durationSec = item.MediaItem.DurationMs > 0 ? (int)(item.MediaItem.DurationMs / 1000) : -1;
                var title = string.IsNullOrWhiteSpace(item.MediaItem.Title) ? Path.GetFileName(item.MediaItem.FilePathOrUrl) : item.MediaItem.Title;

                sb.AppendLine($"#EXTINF:{durationSec},{title}");

                var rawPath = item.MediaItem.FilePathOrUrl;
                if (useRelativePaths && !string.IsNullOrEmpty(targetDir) && !item.MediaItem.IsNetworkStream)
                {
                    try
                    {
                        var relPath = Path.GetRelativePath(targetDir, rawPath);
                        sb.AppendLine(relPath);
                    }
                    catch
                    {
                        sb.AppendLine(rawPath);
                    }
                }
                else
                {
                    sb.AppendLine(rawPath);
                }
            }

            await File.WriteAllTextAsync(targetFilePath, sb.ToString(), Encoding.UTF8);
            Log.Information("Exported playlist {PlaylistId} to {Path} ({Count} tracks)", playlistId, targetFilePath, items.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export playlist {Id} to {Path}", playlistId, targetFilePath);
            return Result.Failure($"Export failed: {ex.Message}");
        }
    }

    public IReadOnlyList<PlaylistItem> Shuffle(IReadOnlyList<PlaylistItem> items)
    {
        var list = new List<PlaylistItem>(items);
        var rng = new Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
        return list.AsReadOnly();
    }
}
