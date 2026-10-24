namespace Nova.Core.Models;

public class DownloadProgressEventArgs : EventArgs
{
    public string DownloadId { get; }
    public long BytesDownloaded { get; }
    public long? TotalBytes { get; }
    public double BytesPerSecond { get; }
    public TimeSpan? EstimatedRemaining { get; }

    public DownloadProgressEventArgs(
        string downloadId,
        long bytesDownloaded,
        long? totalBytes,
        double bytesPerSecond,
        TimeSpan? estimatedRemaining)
    {
        DownloadId = downloadId;
        BytesDownloaded = bytesDownloaded;
        TotalBytes = totalBytes;
        BytesPerSecond = bytesPerSecond;
        EstimatedRemaining = estimatedRemaining;
    }
}

public class DownloadStateChangedEventArgs : EventArgs
{
    public string DownloadId { get; }
    public DownloadState PreviousState { get; }
    public DownloadState NewState { get; }
    public string? ErrorMessage { get; }

    public DownloadStateChangedEventArgs(
        string downloadId,
        DownloadState previousState,
        DownloadState newState,
        string? errorMessage = null)
    {
        DownloadId = downloadId;
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}
