namespace Nova.Core.Models;

public class MediaItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePathOrUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public bool IsNetworkStream { get; set; }
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;
    public long LastPositionMs { get; set; }

    // Phase 3 Extended Metadata
    public string? Container { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public int PlayCount { get; set; }
    public bool IsFavorite { get; set; }

    public string ResolutionDisplay => Width.HasValue && Height.HasValue && Width.Value > 0 && Height.Value > 0
        ? $"{Width.Value}×{Height.Value}"
        : "N/A";

    public MediaItem() { }

    public MediaItem(string pathOrUrl, string? title = null)
    {
        FilePathOrUrl = pathOrUrl;
        IsNetworkStream = pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        Title = !string.IsNullOrWhiteSpace(title)
            ? title
            : (IsNetworkStream ? pathOrUrl : System.IO.Path.GetFileNameWithoutExtension(pathOrUrl));

        if (!IsNetworkStream && File.Exists(pathOrUrl))
        {
            var ext = System.IO.Path.GetExtension(pathOrUrl).TrimStart('.').ToUpperInvariant();
            Container = ext;
            try
            {
                var fi = new FileInfo(pathOrUrl);
                FileSizeBytes = fi.Length;
            }
            catch
            {
                FileSizeBytes = 0;
            }
        }
    }
}
