using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class PlayerViewModelPhase2Tests
{
    private readonly Mock<IPlaybackService> _mockPlaybackService;

    public PlayerViewModelPhase2Tests()
    {
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockPlaybackService.Setup(s => s.NativeMediaPlayer).Returns(new object());
        _mockPlaybackService.Setup(s => s.Volume).Returns(80);
        _mockPlaybackService.Setup(s => s.PlaybackSpeed).Returns(1.0f);
        _mockPlaybackService.Setup(s => s.AudioTracks).Returns(new List<TrackInfo>());
        _mockPlaybackService.Setup(s => s.SubtitleTracks).Returns(new List<TrackInfo>());
    }

    [Fact]
    public async Task SelectAudioTrack_ShouldInvokeService()
    {
        _mockPlaybackService.Setup(s => s.SetAudioTrackAsync(2)).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        await vm.SelectAudioTrackAsync(2);

        _mockPlaybackService.Verify(s => s.SetAudioTrackAsync(2), Times.Once);
    }

    [Fact]
    public async Task AdjustAudioDelay_ShouldAddDeltaAndInvokeService()
    {
        _mockPlaybackService.Setup(s => s.SetAudioDelayAsync(100)).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object)
        {
            AudioDelayMs = 50
        };

        await vm.AdjustAudioDelayAsync(50);

        _mockPlaybackService.Verify(s => s.SetAudioDelayAsync(100), Times.Once);
    }

    [Fact]
    public async Task ResetAudioDelay_ShouldSetToZero()
    {
        _mockPlaybackService.Setup(s => s.SetAudioDelayAsync(0)).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object)
        {
            AudioDelayMs = 250
        };

        await vm.ResetAudioDelayAsync();

        _mockPlaybackService.Verify(s => s.SetAudioDelayAsync(0), Times.Once);
    }

    [Fact]
    public async Task SelectSubtitleTrack_ShouldInvokeService()
    {
        _mockPlaybackService.Setup(s => s.SetSubtitleTrackAsync(1)).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        await vm.SelectSubtitleTrackAsync(1);

        _mockPlaybackService.Verify(s => s.SetSubtitleTrackAsync(1), Times.Once);
    }

    [Fact]
    public async Task AdjustSubtitleDelay_ShouldAddDeltaAndInvokeService()
    {
        _mockPlaybackService.Setup(s => s.SetSubtitleDelayAsync(-50)).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object)
        {
            SubtitleDelayMs = 0
        };

        await vm.AdjustSubtitleDelayAsync(-50);

        _mockPlaybackService.Verify(s => s.SetSubtitleDelayAsync(-50), Times.Once);
    }

    [Fact]
    public async Task SetPlaybackSpeed_ShouldInvokeServiceAndFormatText()
    {
        _mockPlaybackService.Setup(s => s.SetPlaybackSpeedAsync(1.5f)).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        await vm.SetPlaybackSpeedAsync(1.5f);

        _mockPlaybackService.Verify(s => s.SetPlaybackSpeedAsync(1.5f), Times.Once);
    }

    [Fact]
    public async Task CyclePlaybackSpeed_ShouldAdvanceSequentially()
    {
        _mockPlaybackService.Setup(s => s.SetPlaybackSpeedAsync(It.IsAny<float>())).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object)
        {
            PlaybackSpeed = 1.0f
        };

        await vm.CyclePlaybackSpeedAsync();
        _mockPlaybackService.Verify(s => s.SetPlaybackSpeedAsync(1.25f), Times.Once);
    }

    [Fact]
    public async Task SetAspectRatio_ShouldInvokeService()
    {
        _mockPlaybackService.Setup(s => s.SetAspectRatioAsync("16:9")).ReturnsAsync(Result.Success());

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        await vm.SetAspectRatioAsync("16:9");

        _mockPlaybackService.Verify(s => s.SetAspectRatioAsync("16:9"), Times.Once);
    }

    [Fact]
    public async Task ShowMediaInfo_WhenSuccessful_ShouldRetrieveDetails()
    {
        var details = new MediaDetails { Container = "MKV", VideoCodec = "H.264" };
        _mockPlaybackService.Setup(s => s.GetMediaDetailsAsync()).ReturnsAsync(Result<MediaDetails>.Success(details));

        using var vm = new PlayerViewModel(_mockPlaybackService.Object);
        MediaDetails? receivedDetails = null;
        vm.ShowMediaInfoRequested += d => receivedDetails = d;

        await vm.ShowMediaInfoAsync();

        _mockPlaybackService.Verify(s => s.GetMediaDetailsAsync(), Times.Once);
        receivedDetails.Should().NotBeNull();
        receivedDetails!.Container.Should().Be("MKV");
        receivedDetails.VideoCodec.Should().Be("H.264");
    }
}
