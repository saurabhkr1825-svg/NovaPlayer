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

    public MediaItem() { }

    public MediaItem(string pathOrUrl, string? title = null)
    {
        FilePathOrUrl = pathOrUrl;
        IsNetworkStream = pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        Title = !string.IsNullOrWhiteSpace(title)
            ? title
            : (IsNetworkStream ? pathOrUrl : System.IO.Path.GetFileNameWithoutExtension(pathOrUrl));
    }
}
