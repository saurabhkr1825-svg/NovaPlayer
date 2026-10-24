namespace Nova.Core.Models;

public class TrackInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Language { get; set; }
    public bool IsSelected { get; set; }

    public TrackInfo() { }

    public TrackInfo(int id, string name, string? language = null, bool isSelected = false)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? (id == -1 ? "Disable" : $"Track {id}") : name;
        Language = language;
        IsSelected = isSelected;
    }

    public override string ToString()
    {
        if (Id == -1) return "Disabled";
        return string.IsNullOrWhiteSpace(Language) ? Name : $"{Name} [{Language}]";
    }
}
