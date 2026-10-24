namespace Nova.Core.Models;

public class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<PlaylistItem> Items { get; set; } = new();

    public Playlist() { }

    public Playlist(string name)
    {
        Name = name;
    }
}

public class PlaylistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlaylistId { get; set; } = string.Empty;
    public string MediaItemId { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public MediaItem? MediaItem { get; set; }

    public PlaylistItem() { }

    public PlaylistItem(string playlistId, string mediaItemId, int orderIndex)
    {
        PlaylistId = playlistId;
        MediaItemId = mediaItemId;
        OrderIndex = orderIndex;
    }
}
