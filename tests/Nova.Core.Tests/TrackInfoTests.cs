using FluentAssertions;
using Nova.Core.Models;
using Xunit;

namespace Nova.Core.Tests;

public class TrackInfoTests
{
    [Fact]
    public void DisabledTrack_ShouldFormatProperly()
    {
        var track = new TrackInfo(-1, "Disable");
        track.ToString().Should().Be("Disabled");
    }

    [Fact]
    public void TrackWithLanguage_ShouldIncludeLanguageInToString()
    {
        var track = new TrackInfo(2, "English Stereo", "eng");
        track.ToString().Should().Be("English Stereo [eng]");
    }

    [Fact]
    public void TrackWithoutLanguage_ShouldReturnNameOnly()
    {
        var track = new TrackInfo(1, "Track 1");
        track.ToString().Should().Be("Track 1");
    }

    [Fact]
    public void TrackWithEmptyName_ShouldDefaultToTrackId()
    {
        var track = new TrackInfo(3, "");
        track.Name.Should().Be("Track 3");
    }
}
