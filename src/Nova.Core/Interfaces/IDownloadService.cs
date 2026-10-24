using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.Core.Interfaces;

public interface IDownloadService : IDisposable
{
    IReadOnlyList<DownloadItem> Downloads { get; }
    int MaxConcurrentDownloads { get; set; }
    string DefaultDownloadDirectory { get; set; }

    event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
    event EventHandler<DownloadStateChangedEventArgs>? StateChanged;

    Task<Result> InitializeAsync();
    Task<Result<DownloadItem>> EnqueueDownloadAsync(string url, string? destinationDirectory = null, string? customFileName = null);
    Task<Result> PauseDownloadAsync(string id);
    Task<Result> ResumeDownloadAsync(string id);
    Task<Result> CancelDownloadAsync(string id);
    Task<Result> RetryDownloadAsync(string id);
    Task<Result> RemoveDownloadAsync(string id, bool deleteFile = false);
}
