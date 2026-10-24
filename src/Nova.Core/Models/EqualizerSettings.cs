namespace Nova.Core.Models;

public class EqualizerSettings
{
    public const int BandCount = 10;
    public const float MinGainDb = -20.0f;
    public const float MaxGainDb = 20.0f;

    public static readonly float[] DefaultFrequenciesHz = [32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f];
    public static readonly string[] BandLabels = ["32Hz", "64Hz", "125Hz", "250Hz", "500Hz", "1kHz", "2kHz", "4kHz", "8kHz", "16kHz"];

    public bool IsEnabled { get; set; }
    public float PreampGainDb { get; set; }
    public float[] BandGainsDb { get; set; } = new float[BandCount];
    public string ActivePreset { get; set; } = "Flat";

    public static IReadOnlyList<string> Presets =>
    [
        "Flat",
        "Rock",
        "Pop",
        "Jazz",
        "Bass Boost",
        "Vocal",
        "Classical",
        "Electronic"
    ];

    public EqualizerSettings()
    {
        Array.Clear(BandGainsDb, 0, BandGainsDb.Length);
    }

    public static EqualizerSettings CreatePreset(string presetName)
    {
        var settings = new EqualizerSettings
        {
            IsEnabled = true,
            ActivePreset = presetName,
            PreampGainDb = 0f
        };

        switch (presetName.ToLowerInvariant())
        {
            case "rock":
                settings.BandGainsDb = [4.5f, 3.0f, -1.5f, -2.5f, -1.0f, 1.5f, 3.5f, 4.5f, 5.0f, 5.0f];
                break;
            case "pop":
                settings.BandGainsDb = [-1.5f, -0.5f, 2.0f, 3.5f, 3.5f, 1.5f, -0.5f, -1.5f, -1.5f, -1.5f];
                break;
            case "jazz":
                settings.BandGainsDb = [3.5f, 2.5f, 1.0f, 1.5f, -1.5f, -1.5f, 0.0f, 1.5f, 2.5f, 3.5f];
                break;
            case "bass boost":
                settings.BandGainsDb = [7.0f, 5.5f, 4.0f, 2.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f];
                break;
            case "vocal":
                settings.BandGainsDb = [-2.0f, -1.5f, -1.0f, 2.0f, 4.0f, 4.0f, 2.5f, 1.0f, 0.0f, -1.5f];
                break;
            case "classical":
                settings.BandGainsDb = [4.0f, 3.0f, 2.5f, 2.0f, -1.5f, -1.5f, 0.0f, 2.0f, 3.0f, 3.5f];
                break;
            case "electronic":
                settings.BandGainsDb = [4.0f, 3.5f, 1.0f, 0.0f, -1.5f, 2.0f, 1.0f, 2.0f, 3.5f, 4.0f];
                break;
            case "flat":
            default:
                settings.ActivePreset = "Flat";
                Array.Clear(settings.BandGainsDb, 0, BandCount);
                break;
        }

        return settings;
    }
}
