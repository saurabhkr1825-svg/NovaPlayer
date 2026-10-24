using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class PlayerViewModelPhase4Tests
{
    private readonly Mock<IPlaybackService> _mockPlaybackService;
    private readonly Mock<ILibraryRepository> _mockLibraryRepository;

    public PlayerViewModelPhase4Tests()
    {
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockLibraryRepository = new Mock<ILibraryRepository>();

        _mockPlaybackService.Setup(s => s.NativeMediaPlayer).Returns(new object());
        _mockPlaybackService.Setup(s => s.Volume).Returns(100);
        _mockPlaybackService.Setup(s => s.PlayAsync(It.IsAny<string>(), It.IsAny<long>())).ReturnsAsync(Result.Success());
        _mockPlaybackService.Setup(s => s.TakeSnapshotAsync(It.IsAny<string?>())).ReturnsAsync(Result<string>.Success("C:\\Users\\Mock\\Pictures\\snapshot.png"));
        _mockPlaybackService.Setup(s => s.SetEqualizerAsync(It.IsAny<EqualizerSettings>())).ReturnsAsync(Result.Success());
    }

    [Fact]
    public void BufferingProgressChanged_UpdatesBufferingPercentageAndState()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        // Raise buffering at 45%
        _mockPlaybackService.Raise(s => s.BufferingProgressChanged += null, _mockPlaybackService.Object, 45.0f);

        vm.BufferingPercentage.Should().Be(45.0f);
        vm.IsBuffering.Should().BeTrue();

        // Raise buffering at 100% (buffering completed)
        _mockPlaybackService.Raise(s => s.BufferingProgressChanged += null, _mockPlaybackService.Object, 100.0f);

        vm.BufferingPercentage.Should().Be(100.0f);
        vm.IsBuffering.Should().BeFalse();
    }

    [Fact]
    public async Task PlayUrlAsync_WithValidHttpUrl_SwitchesToTabZeroAndPlays()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);
        vm.ActiveTabIndex = 2;

        const string validUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";
        await vm.PlayUrlAsync(validUrl);

        vm.ActiveTabIndex.Should().Be(0);
        vm.IsNetworkPlayback.Should().BeTrue();
        _mockPlaybackService.Verify(s => s.PlayAsync(validUrl, 0L), Times.Once);
    }

    [Fact]
    public async Task PlayUrlAsync_WithInvalidScheme_RejectsAndSetsStatusMessage()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        await vm.PlayUrlAsync("ftp://ftp.example.com/video.mp4");

        vm.StatusMessage.Should().Contain("Unsupported protocol");
        _mockPlaybackService.Verify(s => s.PlayAsync(It.IsAny<string>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public void ToggleMiniPlayer_TogglesIsMiniPlayerState()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        vm.IsMiniPlayer.Should().BeFalse();

        vm.ToggleMiniPlayer();
        vm.IsMiniPlayer.Should().BeTrue();

        vm.ExitMiniPlayer();
        vm.IsMiniPlayer.Should().BeFalse();
    }

    [Fact]
    public async Task TakeSnapshotAsync_InvokesPlaybackServiceSnapshot()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        await vm.TakeSnapshotAsync();

        _mockPlaybackService.Verify(s => s.TakeSnapshotAsync(null), Times.Once);
        vm.StatusMessage.Should().Contain("Snapshot saved");
    }

    [Fact]
    public void OpenUrl_And_ShowEqualizer_FireEvents()
    {
        using var vm = new PlayerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        bool openUrlFired = false;
        bool equalizerFired = false;

        vm.OpenUrlRequested += () => openUrlFired = true;
        vm.ShowEqualizerRequested += () => equalizerFired = true;

        vm.OpenUrl();
        vm.ShowEqualizer();

        openUrlFired.Should().BeTrue();
        equalizerFired.Should().BeTrue();
    }
}
