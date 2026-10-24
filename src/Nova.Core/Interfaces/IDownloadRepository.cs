using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.Core.Interfaces;

public interface IDownloadRepository
{
    Task<Result<IReadOnlyList<DownloadItem>>> GetAllDownloadsAsync();
    Task<Result<DownloadItem?>> GetDownloadAsync(string id);
    Task<Result> AddDownloadAsync(DownloadItem item);
    Task<Result> UpdateDownloadProgressAsync(string id, long bytesDownloaded, long? totalBytes, string? etag, string? lastModified);
    Task<Result> UpdateDownloadStateAsync(string id, DownloadState state, string? errorMessage = null);
    Task<Result> DeleteDownloadAsync(string id);
}
