using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Playback;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class PlaybackServiceTests
{
    [Fact]
    public async Task PlaybackLifecycle_WithRealMediaFile_ExecutesSuccessfully()
    {
        var sampleFile = @"d:\projects\Nova\test_sample.mp4";
        File.Exists(sampleFile).Should().BeTrue("Sample video file must exist for playback verification");

        using var service = new LibVlcPlaybackService();

        var stateChanges = new List<PlaybackState>();
        service.StateChanged += (s, state) => stateChanges.Add(state);

        // 1. Play media
        var playResult = await service.PlayAsync(sampleFile);
        playResult.IsSuccess.Should().BeTrue();

        // Wait up to 3 seconds for playback to transition to Playing
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (service.State != PlaybackState.Playing && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        service.State.Should().Be(PlaybackState.Playing);

        // 2. Test Volume
        var volResult = await service.SetVolumeAsync(70);
        volResult.IsSuccess.Should().BeTrue();
        service.Volume.Should().Be(70);

        // 3. Test Pause
        var pauseResult = await service.PauseAsync();
        pauseResult.IsSuccess.Should().BeTrue();

        // 4. Test Resume
        var resumeResult = await service.ResumeAsync();
        resumeResult.IsSuccess.Should().BeTrue();

        // 5. Test Seek
        var seekResult = await service.SeekAsync(2000);
        seekResult.IsSuccess.Should().BeTrue();

        // 6. Test Stop
        var stopResult = await service.StopAsync();
        stopResult.IsSuccess.Should().BeTrue();
        service.State.Should().Be(PlaybackState.Stopped);
    }
}
