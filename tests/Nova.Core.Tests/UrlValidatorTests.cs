using Nova.Core.Common;
using Xunit;

namespace Nova.Core.Tests;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("http://example.com/video.mp4")]
    [InlineData("https://streams.example.org/master.m3u8")]
    [InlineData("https://dash.example.com/stream.mpd?token=123")]
    [InlineData("https://cdn.provider.net:8080/media/live/index.m3u8#track=1")]
    public void ValidateMediaUrl_ValidHttpHttpsUrls_ReturnsSuccess(string validUrl)
    {
        var result = UrlValidator.ValidateMediaUrl(validUrl);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(validUrl.Split('?')[0].Split('#')[0], result.Value.GetLeftPart(UriPartial.Path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateMediaUrl_NullOrEmpty_ReturnsFailure(string? emptyUrl)
    {
        var result = UrlValidator.ValidateMediaUrl(emptyUrl);

        Assert.True(result.IsFailure);
        Assert.Contains("cannot be empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ftp://ftp.example.com/video.mp4")]
    [InlineData("file:///C:/movies/video.mp4")]
    [InlineData("smb://nas/share/movie.mkv")]
    [InlineData("javascript:alert(1)")]
    public void ValidateMediaUrl_NonHttpProtocols_ReturnsFailure(string invalidProtocol)
    {
        var result = UrlValidator.ValidateMediaUrl(invalidProtocol);

        Assert.True(result.IsFailure);
        Assert.Contains("Unsupported protocol", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("http://")]
    [InlineData("https:///only-path.mp4")]
    [InlineData("://missing-scheme.com")]
    public void ValidateMediaUrl_MalformedUrls_ReturnsFailure(string malformedUrl)
    {
        var result = UrlValidator.ValidateMediaUrl(malformedUrl);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8", true)]
    [InlineData("https://example.com/video.m3u8?token=xyz", true)]
    [InlineData("https://example.com/video.mp4", false)]
    public void IsHlsStream_DetectsM3u8Extension(string url, bool expected)
    {
        Assert.Equal(expected, UrlValidator.IsHlsStream(url));
    }

    [Theory]
    [InlineData("https://dash.akamaized.net/akamai/test/index.mpd", true)]
    [InlineData("https://example.com/stream.mpd?auth=abc", true)]
    [InlineData("https://example.com/stream.m3u8", false)]
    public void IsDashStream_DetectsMpdExtension(string url, bool expected)
    {
        Assert.Equal(expected, UrlValidator.IsDashStream(url));
    }
}
