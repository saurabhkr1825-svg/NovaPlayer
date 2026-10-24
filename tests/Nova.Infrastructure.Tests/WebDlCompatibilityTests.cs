using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Playback;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class WebDlCompatibilityTests
{
    [Fact]
    public async Task WebDl_H264_Aac_Mp4_PlaybackAndControls_Succeed()
    {
        var filePath = @"d:\projects\Nova\sample_webdl_h264_aac.mp4";
        File.Exists(filePath).Should().BeTrue();

        using var service = new LibVlcPlaybackService();
        var playRes = await service.PlayAsync(filePath);
        playRes.IsSuccess.Should().BeTrue();

        // Wait for playback to start
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (service.State != PlaybackState.Playing && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        service.State.Should().Be(PlaybackState.Playing);

        // Aspect ratio
        var arRes = await service.SetAspectRatioAsync("16:9");
        arRes.IsSuccess.Should().BeTrue();
        service.AspectRatio.Should().Be("16:9");

        // Playback speed
        var speedRes = await service.SetPlaybackSpeedAsync(1.5f);
        speedRes.IsSuccess.Should().BeTrue();
        service.PlaybackSpeed.Should().Be(1.5f);

        // Media details
        var detailsRes = await service.GetMediaDetailsAsync();
        detailsRes.IsSuccess.Should().BeTrue();
        var details = detailsRes.Value;
        details.Container.Should().Be("MP4");
        details.Width.Should().Be(1920);
        details.Height.Should().Be(1080);

        await service.StopAsync();
    }

    [Fact]
    public async Task WebDl_Hevc10Bit_EAc3_51_Mkv_PlaybackAndTracks_Succeed()
    {
        var filePath = @"d:\projects\Nova\sample_webdl_hevc_eac3.mkv";
        File.Exists(filePath).Should().BeTrue();

        using var service = new LibVlcPlaybackService();
        var playRes = await service.PlayAsync(filePath);
        playRes.IsSuccess.Should().BeTrue();

        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (service.State != PlaybackState.Playing && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        service.State.Should().Be(PlaybackState.Playing);

        // Verify audio delay adjustment (-5000 to +5000)
        var audioDelayRes = await service.SetAudioDelayAsync(150);
        audioDelayRes.IsSuccess.Should().BeTrue();
        service.AudioDelayMs.Should().Be(150);

        // Verify subtitle delay adjustment
        var subDelayRes = await service.SetSubtitleDelayAsync(-100);
        subDelayRes.IsSuccess.Should().BeTrue();
        service.SubtitleDelayMs.Should().Be(-100);

        // Subtitle track detection
        service.SubtitleTracks.Count.Should().BeGreaterThan(0, "Embedded and/or auto-discovered subtitles should be detected");

        // Media details
        var detailsRes = await service.GetMediaDetailsAsync();
        detailsRes.IsSuccess.Should().BeTrue();
        var details = detailsRes.Value;
        details.Container.Should().Be("MKV");
        details.Width.Should().Be(1920);
        details.Height.Should().Be(1080);
        details.AudioChannels.Should().Be(6, "E-AC-3 track has 6 channels (5.1 surround)");

        await service.StopAsync();
    }

    [Fact]
    public async Task WebDl_Av1_Opus_Mkv_PlaybackAndInspection_Succeed()
    {
        var filePath = @"d:\projects\Nova\sample_webdl_av1_opus.mkv";
        File.Exists(filePath).Should().BeTrue();

        using var service = new LibVlcPlaybackService();
        var playRes = await service.PlayAsync(filePath);
        playRes.IsSuccess.Should().BeTrue();

        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (service.State != PlaybackState.Playing && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        service.State.Should().Be(PlaybackState.Playing);

        // Media details
        var detailsRes = await service.GetMediaDetailsAsync();
        detailsRes.IsSuccess.Should().BeTrue();
        var details = detailsRes.Value;
        details.Container.Should().Be("MKV");
        details.Width.Should().Be(1280);
        details.Height.Should().Be(720);

        await service.StopAsync();
    }
}
