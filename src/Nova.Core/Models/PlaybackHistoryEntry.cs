using Nova.Core.Common;

namespace Nova.Core.Models;

public class PlaybackHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MediaItemId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FilePathOrUrl { get; set; } = string.Empty;
    public long PositionMs { get; set; }
    public long DurationMs { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public string FormattedPosition => TimeFormatter.FormatMilliseconds(PositionMs);
    public string FormattedDuration => TimeFormatter.FormatMilliseconds(DurationMs);
    public double ProgressPercentage => DurationMs > 0 ? Math.Clamp((double)PositionMs / DurationMs * 100.0, 0, 100) : 0;
}
