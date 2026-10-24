using FluentAssertions;
using Nova.Core.Models;
using Xunit;

namespace Nova.Core.Tests;

public class MediaItemTests
{
    [Fact]
    public void Constructor_WithLocalFilePath_ShouldInferTitleAndSetLocal()
    {
        var item = new MediaItem(@"C:\Videos\ActionMovie.mp4");

        item.FilePathOrUrl.Should().Be(@"C:\Videos\ActionMovie.mp4");
        item.Title.Should().Be("ActionMovie");
        item.IsNetworkStream.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://example.com/live/stream.m3u8")]
    [InlineData("https://example.com/video.mp4")]
    public void Constructor_WithHttpUrl_ShouldDetectNetworkStream(string url)
    {
        var item = new MediaItem(url);

        item.FilePathOrUrl.Should().Be(url);
        item.IsNetworkStream.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomTitle_ShouldPreserveTitle()
    {
        var item = new MediaItem("https://example.com/stream", "Custom Channel");

        item.Title.Should().Be("Custom Channel");
    }
}
