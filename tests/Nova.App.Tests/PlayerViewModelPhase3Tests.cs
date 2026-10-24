using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class PlayerViewModelPhase3Tests
{
    private readonly Mock<IPlaybackService> _mockPlayback;
    private readonly Mock<ILibraryRepository> _mockRepo;

    public PlayerViewModelPhase3Tests()
    {
        _mockPlayback = new Mock<IPlaybackService>();
        _mockPlayback.Setup(p => p.PlayAsync(It.IsAny<string>(), It.IsAny<long>())).ReturnsAsync(Result.Success());
        _mockPlayback.Setup(p => p.SeekAsync(It.IsAny<long>())).ReturnsAsync(Result.Success());

        _mockRepo = new Mock<ILibraryRepository>();
    }

    [Fact]
    public async Task PlayFile_WhenResumePointExceeds10Seconds_ShouldShowResumePrompt()
    {
        var resumeEntry = new PlaybackHistoryEntry
        {
            FilePathOrUrl = "C:\\Videos\\Movie.mkv",
            PositionMs = 45000,
            DurationMs = 120000
        };

        _mockRepo.Setup(r => r.GetResumePointAsync("C:\\Videos\\Movie.mkv"))
            .ReturnsAsync(Result<PlaybackHistoryEntry?>.Success(resumeEntry));

        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object);
        await vm.PlayFileAsync("C:\\Videos\\Movie.mkv");

        vm.IsResumePromptVisible.Should().BeTrue();
        vm.PendingResumePositionMs.Should().Be(45000);
        vm.ResumePromptText.Should().Contain("00:45");
    }

    [Fact]
    public async Task PlayFile_WhenResumePointBelowThreshold_ShouldNotShowResumePrompt()
    {
        // 5 seconds in: below the 10-second threshold
        var resumeEntry = new PlaybackHistoryEntry
        {
            FilePathOrUrl = "C:\\Videos\\Clip.mp4",
            PositionMs = 5000,
            DurationMs = 120000
        };

        _mockRepo.Setup(r => r.GetResumePointAsync("C:\\Videos\\Clip.mp4"))
            .ReturnsAsync(Result<PlaybackHistoryEntry?>.Success(resumeEntry));

        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object);
        await vm.PlayFileAsync("C:\\Videos\\Clip.mp4");

        vm.IsResumePromptVisible.Should().BeFalse();
        vm.PendingResumePositionMs.Should().Be(0);
    }

    [Fact]
    public async Task ResumePlayback_WhenConfirmed_ShouldSeekAndDismissPrompt()
    {
        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object)
        {
            IsResumePromptVisible = true,
            PendingResumePositionMs = 55000
        };

        await vm.ResumePlaybackAsync();

        _mockPlayback.Verify(p => p.SeekAsync(55000), Times.Once);
        vm.IsResumePromptVisible.Should().BeFalse();
    }

    [Fact]
    public void DismissResumePrompt_ShouldHidePromptWithoutSeeking()
    {
        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object)
        {
            IsResumePromptVisible = true,
            PendingResumePositionMs = 55000
        };

        vm.DismissResumePrompt();

        _mockPlayback.Verify(p => p.SeekAsync(It.IsAny<long>()), Times.Never);
        vm.IsResumePromptVisible.Should().BeFalse();
    }

    [Fact]
    public void SetQueue_ShouldInitializeQueueAndPlayFirstTrack()
    {
        var tracks = new List<string>
        {
            "C:\\Music\\Track1.mp3",
            "C:\\Music\\Track2.mp3",
            "C:\\Music\\Track3.mp3"
        };

        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object);
        vm.SetQueue(tracks, startIndex: 0);

        vm.PlaybackQueue.Should().HaveCount(3);
        vm.CurrentQueueIndex.Should().Be(0);
        vm.HasNextTrack.Should().BeTrue();
        vm.HasPreviousTrack.Should().BeFalse();
    }

    [Fact]
    public async Task PlayNextTrack_ShouldAdvanceInQueue()
    {
        var tracks = new List<string>
        {
            "C:\\Music\\Track1.mp3",
            "C:\\Music\\Track2.mp3"
        };

        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object);
        vm.SetQueue(tracks, startIndex: 0);

        await vm.PlayNextTrackAsync();

        vm.CurrentQueueIndex.Should().Be(1);
        vm.CurrentFilePath.Should().Be("C:\\Music\\Track2.mp3");
        vm.HasNextTrack.Should().BeFalse();
        vm.HasPreviousTrack.Should().BeTrue();
    }

    [Fact]
    public void CycleRepeatMode_ShouldTransitionSequentially()
    {
        using var vm = new PlayerViewModel(_mockPlayback.Object, _mockRepo.Object);

        vm.RepeatMode.Should().Be(RepeatMode.None);

        vm.CycleRepeatMode();
        vm.RepeatMode.Should().Be(RepeatMode.RepeatAll);

        vm.CycleRepeatMode();
        vm.RepeatMode.Should().Be(RepeatMode.RepeatOne);

        vm.CycleRepeatMode();
        vm.RepeatMode.Should().Be(RepeatMode.None);
    }
}
