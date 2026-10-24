using FluentAssertions;
using Nova.Core.Models;
using Xunit;

namespace Nova.Core.Tests;

public class MediaDetailsTests
{
    [Fact]
    public void ResolutionDisplay_WithValidDimensions_FormatsCorrectly()
    {
        var details = new MediaDetails { Width = 1920, Height = 1080 };
        details.ResolutionDisplay.Should().Be("1920 × 1080");
    }

    [Fact]
    public void ResolutionDisplay_WithZeroOrNull_ReturnsNotAvailable()
    {
        var details = new MediaDetails { Width = 0, Height = 0 };
        details.ResolutionDisplay.Should().Be("N/A");

        var detailsNull = new MediaDetails { Width = null, Height = null };
        detailsNull.ResolutionDisplay.Should().Be("N/A");
    }

    [Fact]
    public void FrameRateDisplay_WithValidFps_FormatsCorrectly()
    {
        var details = new MediaDetails { FrameRate = 23.976 };
        details.FrameRateDisplay.Should().Be("23.98 fps");
    }

    [Fact]
    public void FrameRateDisplay_WithZeroOrNull_ReturnsNotAvailable()
    {
        var details = new MediaDetails { FrameRate = null };
        details.FrameRateDisplay.Should().Be("N/A");
    }

    [Theory]
    [InlineData(1, "Mono (1.0)")]
    [InlineData(2, "Stereo (2.0)")]
    [InlineData(6, "5.1 Surround (6 ch)")]
    [InlineData(8, "7.1 Surround (8 ch)")]
    [InlineData(0, "None / N/A")]
    public void AudioChannelsDisplay_MapsAccurately(int channels, string expected)
    {
        var details = new MediaDetails { AudioChannels = channels };
        details.AudioChannelsDisplay.Should().Be(expected);
    }

    [Fact]
    public void TotalBitrateDisplay_WithVideoAndAudio_CalculatesSum()
    {
        var details = new MediaDetails
        {
            VideoBitrate = 4500,
            AudioBitrate = 384
        };
        details.TotalBitrateDisplay.Should().Be("4884 kbps");
    }

    [Fact]
    public void TotalBitrateDisplay_WithZero_ReturnsVariable()
    {
        var details = new MediaDetails { VideoBitrate = null, AudioBitrate = null };
        details.TotalBitrateDisplay.Should().Be("Variable / Not specified");
    }
}
