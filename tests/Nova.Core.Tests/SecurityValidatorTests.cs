using System.Net;
using FluentAssertions;
using Nova.Core.Common;
using Xunit;

namespace Nova.Core.Tests;

public class SecurityValidatorTests
{
    [Theory]
    [InlineData("http://example.com/video.mp4")]
    [InlineData("https://cdn.example.org/files/movie.mkv")]
    [InlineData("https://media.server.com:8443/download/audio.flac?auth=token")]
    public void ValidateUrlStructure_ValidUrls_Succeeds(string validUrl)
    {
        var result = SecurityValidator.ValidateUrlStructure(validUrl);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Theory]
    [InlineData("ftp://ftp.example.com/video.mp4")]
    [InlineData("file:///C:/secret/doc.pdf")]
    [InlineData("smb://nas/share/movie.mkv")]
    [InlineData("javascript:alert(1)")]
    public void ValidateUrlStructure_ProhibitedSchemes_Fails(string invalidSchemeUrl)
    {
        var result = SecurityValidator.ValidateUrlStructure(invalidSchemeUrl);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported scheme");
    }

    [Theory]
    [InlineData("http://user:password@example.com/video.mp4")]
    [InlineData("https://admin:secret123@cdn.example.org/file.mp4")]
    [InlineData("http://root:@server.net/download")]
    public void ValidateUrlStructure_UrlsWithEmbeddedCredentials_AreRejected(string credentialUrl)
    {
        var result = SecurityValidator.ValidateUrlStructure(credentialUrl);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("embedded user credentials");
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.255.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("10.254.1.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("192.168.100.50", true)]
    [InlineData("169.254.169.254", true)] // AWS/cloud metadata service
    [InlineData("100.64.0.1", true)]      // CGNAT
    [InlineData("224.0.0.1", true)]       // Multicast
    [InlineData("255.255.255.255", true)] // Broadcast
    [InlineData("8.8.8.8", false)]        // Public Google DNS
    [InlineData("1.1.1.1", false)]        // Public Cloudflare DNS
    [InlineData("93.184.216.34", false)]  // Public example.com
    public void IsProhibitedIpAddress_IPv4_AccuratelyClassifiesRestrictedRanges(string ipString, bool expectedProhibited)
    {
        var ip = IPAddress.Parse(ipString);
        SecurityValidator.IsProhibitedIpAddress(ip).Should().Be(expectedProhibited);
    }

    [Theory]
    [InlineData("::1", true)]               // IPv6 Loopback
    [InlineData("fe80::1", true)]           // IPv6 Link-Local
    [InlineData("fc00::1", true)]           // IPv6 Unique Local
    [InlineData("fd12:3456:789a::1", true)] // IPv6 Unique Local
    [InlineData("ff02::1", true)]           // IPv6 Multicast
    [InlineData("2606:4700:4700::1111", false)] // Public Cloudflare IPv6
    public void IsProhibitedIpAddress_IPv6_AccuratelyClassifiesRestrictedRanges(string ipString, bool expectedProhibited)
    {
        var ip = IPAddress.Parse(ipString);
        SecurityValidator.IsProhibitedIpAddress(ip).Should().Be(expectedProhibited);
    }

    [Fact]
    public void IsProhibitedIpAddress_IPv4MappedIPv6_AccuratelyBlocksPrivateIPv4()
    {
        // ::ffff:192.168.1.1
        var mappedIp = IPAddress.Parse("::ffff:192.168.1.1");
        SecurityValidator.IsProhibitedIpAddress(mappedIp).Should().BeTrue();

        // ::ffff:127.0.0.1
        var mappedLoopback = IPAddress.Parse("::ffff:127.0.0.1");
        SecurityValidator.IsProhibitedIpAddress(mappedLoopback).Should().BeTrue();

        // ::ffff:8.8.8.8 (Public)
        var mappedPublic = IPAddress.Parse("::ffff:8.8.8.8");
        SecurityValidator.IsProhibitedIpAddress(mappedPublic).Should().BeFalse();
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\Windows\\System32\\cmd.exe", "cmd.exe")]
    [InlineData("movie:name*?<>.mp4", "moviename.mp4")]
    [InlineData("video.mp4...   ", "video.mp4")]
    public void SanitizeFileName_StripsDirectoryTraversalAndIllegalChars(string rawName, string expectedClean)
    {
        var result = SecurityValidator.SanitizeFileName(rawName);
        result.Should().Be(expectedClean);
    }

    [Theory]
    [InlineData("CON.mp4")]
    [InlineData("PRN.mkv")]
    [InlineData("AUX.avi")]
    [InlineData("NUL")]
    [InlineData("COM1.mp3")]
    public void SanitizeFileName_WindowsReservedDeviceNames_AreNeutralized(string reservedName)
    {
        var result = SecurityValidator.SanitizeFileName(reservedName);
        result.Should().StartWith("file_");
    }

    [Fact]
    public void VerifyPathContainment_DetectsDirectoryEscapes()
    {
        var baseDir = @"C:\Users\User\Downloads";
        var safePath = @"C:\Users\User\Downloads\video.mp4";
        var escapePath = @"C:\Users\User\Downloads\..\..\Windows\System32\malware.exe";

        SecurityValidator.VerifyPathContainment(baseDir, safePath).Should().BeTrue();
        SecurityValidator.VerifyPathContainment(baseDir, escapePath).Should().BeFalse();
    }

    [Fact]
    public void GetUniqueFilePath_WhenFileDoesNotExist_ReturnsOriginalPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"nova_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var uniquePath = SecurityValidator.GetUniqueFilePath(tempDir, "video.mp4");
            uniquePath.Should().Be(Path.Combine(tempDir, "video.mp4"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetUniqueFilePath_WhenFileAlreadyExists_AppendsCounter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"nova_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var existingFile = Path.Combine(tempDir, "video.mp4");
            File.WriteAllText(existingFile, "test");

            var uniquePath = SecurityValidator.GetUniqueFilePath(tempDir, "video.mp4");
            uniquePath.Should().Be(Path.Combine(tempDir, "video (1).mp4"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
