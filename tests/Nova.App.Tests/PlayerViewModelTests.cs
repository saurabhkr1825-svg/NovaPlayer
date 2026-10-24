using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class PlayerViewModelTests
{
    private readonly Mock<IPlaybackService> _mockPlaybackService;

    public PlayerViewModelTests()
    {
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockPlaybackService.Setup(s => s.NativeMediaPlayer).Returns(new object());
        _mockPlaybackService.Setup(s => s.Volume).Returns(80);
    }

    [Fact]
    public void InitialState_ShouldHaveDefaultValues()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object);

        vm.CurrentMediaTitle.Should().Be("No media loaded");
        vm.WindowTitle.Should().Be("NovaPlayer");
        vm.IsPlaying.Should().BeFalse();
        vm.IsPaused.Should().BeFalse();
        vm.IsFullscreen.Should().BeFalse();
        vm.CurrentTimeText.Should().Be("00:00");
    }

    [Fact]
    public async Task PlayPause_WhenPlaying_ShouldCallPause()
    {
        _mockPlaybackService.Setup(s => s.PauseAsync()).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);

        // Simulate state transition to playing
        _mockPlaybackService.Raise(s => s.StateChanged += null, _mockPlaybackService.Object, PlaybackState.Playing);
        vm.IsPlaying.Should().BeTrue();

        await vm.PlayPauseAsync();

        _mockPlaybackService.Verify(s => s.PauseAsync(), Times.Once);
    }

    [Fact]
    public async Task PlayPause_WhenPaused_ShouldCallResume()
    {
        _mockPlaybackService.Setup(s => s.ResumeAsync()).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);

        // Simulate state transition to paused
        _mockPlaybackService.Raise(s => s.StateChanged += null, _mockPlaybackService.Object, PlaybackState.Paused);
        vm.IsPaused.Should().BeTrue();

        await vm.PlayPauseAsync();

        _mockPlaybackService.Verify(s => s.ResumeAsync(), Times.Once);
    }

    [Fact]
    public async Task Stop_ShouldCallStopServiceAndResetTimers()
    {
        _mockPlaybackService.Setup(s => s.StopAsync()).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        vm.CurrentPositionMs = 5000;

        await vm.StopAsync();

        _mockPlaybackService.Verify(s => s.StopAsync(), Times.Once);
        vm.IsPlaying.Should().BeFalse();
        vm.IsPaused.Should().BeFalse();
        vm.CurrentPositionMs.Should().Be(0);
        vm.CurrentTimeText.Should().Be("00:00");
    }

    [Fact]
    public async Task SeekRelative_ShouldCalculateNewPositionAndSeek()
    {
        _mockPlaybackService.Setup(s => s.SeekAsync(It.IsAny<long>())).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        vm.CurrentPositionMs = 10000;

        await vm.SeekRelativeAsync(5000);

        _mockPlaybackService.Verify(s => s.SeekAsync(15000), Times.Once);
    }

    [Fact]
    public async Task VolumeRelative_ShouldClampAndCallSetVolume()
    {
        _mockPlaybackService.Setup(s => s.SetVolumeAsync(It.IsAny<int>())).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object)
        {
            Volume = 95
        };

        await vm.VolumeRelativeAsync(10); // 95 + 10 should clamp to 100

        _mockPlaybackService.Verify(s => s.SetVolumeAsync(100), Times.Once);
    }

    [Fact]
    public void ToggleFullscreen_ShouldFlipFullscreenProperty()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object);

        vm.IsFullscreen.Should().BeFalse();
        vm.ToggleFullscreen();
        vm.IsFullscreen.Should().BeTrue();
        vm.ToggleFullscreen();
        vm.IsFullscreen.Should().BeFalse();
    }
}
