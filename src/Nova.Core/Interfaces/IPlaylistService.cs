using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.Core.Interfaces;

public interface IPlaylistService
{
    Task<Result<Playlist>> ImportM3uAsync(string m3uFilePath);
    Task<Result> ExportM3uAsync(string playlistId, string targetFilePath, bool useRelativePaths = true);
    IReadOnlyList<PlaylistItem> Shuffle(IReadOnlyList<PlaylistItem> items);
}
