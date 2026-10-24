namespace Nova.Core.Models;

public enum DownloadState
{
    Queued = 0,
    Downloading = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
