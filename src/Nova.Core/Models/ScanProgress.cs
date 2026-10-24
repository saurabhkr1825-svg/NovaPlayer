namespace Nova.Core.Models;

public class ScanProgress
{
    public int TotalFilesDiscovered { get; set; }
    public int FilesProcessed { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }

    public double Percentage => TotalFilesDiscovered > 0
        ? Math.Clamp((double)FilesProcessed / TotalFilesDiscovered * 100.0, 0, 100)
        : 0;
}
