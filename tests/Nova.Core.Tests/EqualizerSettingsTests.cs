using Nova.Core.Models;
using Xunit;

namespace Nova.Core.Tests;

public class EqualizerSettingsTests
{
    [Fact]
    public void EqualizerSettings_DefaultValues_AreSensible()
    {
        var settings = new EqualizerSettings();

        Assert.False(settings.IsEnabled);
        Assert.Equal(0f, settings.PreampGainDb);
        Assert.Equal(10, settings.BandGainsDb.Length);
        Assert.Equal("Flat", settings.ActivePreset);
        Assert.All(settings.BandGainsDb, gain => Assert.Equal(0f, gain));
    }

    [Theory]
    [InlineData("Flat")]
    [InlineData("Rock")]
    [InlineData("Pop")]
    [InlineData("Jazz")]
    [InlineData("Bass Boost")]
    [InlineData("Vocal")]
    [InlineData("Classical")]
    [InlineData("Electronic")]
    public void CreatePreset_AllStandardPresets_ProduceValidGains(string presetName)
    {
        var settings = EqualizerSettings.CreatePreset(presetName);

        Assert.True(settings.IsEnabled);
        Assert.Equal(presetName, settings.ActivePreset, ignoreCase: true);
        Assert.Equal(10, settings.BandGainsDb.Length);

        // Every band must be within [-20dB, +20dB]
        foreach (var gain in settings.BandGainsDb)
        {
            Assert.InRange(gain, EqualizerSettings.MinGainDb, EqualizerSettings.MaxGainDb);
        }
    }

    [Fact]
    public void BassBoost_Preset_AmplifiesLowFrequencies()
    {
        var settings = EqualizerSettings.CreatePreset("Bass Boost");

        // First two bands (32Hz, 64Hz) should have higher gain than upper bands
        Assert.True(settings.BandGainsDb[0] > 5.0f);
        Assert.True(settings.BandGainsDb[1] > 4.0f);
        Assert.Equal(0f, settings.BandGainsDb[9]); // 16kHz
    }

    [Fact]
    public void PresetsList_ContainsAllExpectedPresets()
    {
        var presets = EqualizerSettings.Presets;

        Assert.Contains("Flat", presets);
        Assert.Contains("Rock", presets);
        Assert.Contains("Pop", presets);
        Assert.Contains("Jazz", presets);
        Assert.Contains("Bass Boost", presets);
        Assert.Contains("Vocal", presets);
        Assert.Contains("Classical", presets);
        Assert.Contains("Electronic", presets);
    }
}
