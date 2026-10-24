namespace Nova.Core.Models;

public class DownloadItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string TempFilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DownloadState State { get; set; } = DownloadState.Queued;
    public long BytesDownloaded { get; set; }
    public long? TotalBytes { get; set; }
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }

    public double ProgressPercentage => TotalBytes.HasValue && TotalBytes.Value > 0
        ? Math.Clamp((double)BytesDownloaded / TotalBytes.Value * 100.0, 0.0, 100.0)
        : 0.0;

    public bool IsIndeterminate => !TotalBytes.HasValue || TotalBytes.Value <= 0;

    public DownloadItem() { }

    public DownloadItem(string url, string destinationPath, string? customFileName = null)
    {
        Url = url;
        DestinationPath = destinationPath;
        FileName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName
            : Path.GetFileName(destinationPath);
        TempFilePath = $"{destinationPath}.part";
    }
}
