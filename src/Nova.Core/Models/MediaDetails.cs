namespace Nova.Core.Models;

public class MediaDetails
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Container { get; set; } = "Unknown";
    public long DurationMs { get; set; }

    // Video Specs
    public string? VideoCodec { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? FrameRate { get; set; }
    public uint? VideoBitrate { get; set; }

    // Audio Specs
    public string? AudioCodec { get; set; }
    public int AudioChannels { get; set; }
    public int AudioSampleRate { get; set; }
    public uint? AudioBitrate { get; set; }

    // Track Collections
    public List<TrackInfo> AudioTracks { get; set; } = new();
    public List<TrackInfo> SubtitleTracks { get; set; } = new();

    // Formatted Display Properties
    public string ResolutionDisplay => Width.HasValue && Height.HasValue && Width.Value > 0 && Height.Value > 0
        ? $"{Width.Value} × {Height.Value}"
        : "N/A";

    public string FrameRateDisplay => FrameRate.HasValue && FrameRate.Value > 0
        ? $"{FrameRate.Value:F2} fps"
        : "N/A";

    public string TotalBitrateDisplay
    {
        get
        {
            var total = (VideoBitrate ?? 0) + (AudioBitrate ?? 0);
            return total > 0 ? $"{total} kbps" : "Variable / Not specified";
        }
    }

    public string AudioChannelsDisplay => AudioChannels switch
    {
        1 => "Mono (1.0)",
        2 => "Stereo (2.0)",
        6 => "5.1 Surround (6 ch)",
        8 => "7.1 Surround (8 ch)",
        > 0 => $"{AudioChannels} channels",
        _ => "None / N/A"
    };

    public string AudioSampleRateDisplay => AudioSampleRate > 0
        ? $"{AudioSampleRate:N0} Hz"
        : "N/A";
}
