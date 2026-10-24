using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class DownloadsViewModelTests
{
    private readonly Mock<IDownloadService> _mockService;
    private readonly Mock<ILibraryRepository> _mockLibraryRepo;

    public DownloadsViewModelTests()
    {
        _mockService = new Mock<IDownloadService>();
        _mockLibraryRepo = new Mock<ILibraryRepository>();

        _mockService.Setup(s => s.Downloads).Returns(new List<DownloadItem>());
        _mockService.Setup(s => s.InitializeAsync()).ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task InitializeAsync_LoadsExistingDownloadsFromService()
    {
        var existing = new List<DownloadItem>
        {
            new("https://example.com/movie.mp4", @"C:\Downloads\movie.mp4") { State = DownloadState.Completed, BytesDownloaded = 5000, TotalBytes = 5000 },
            new("https://example.com/audio.mp3", @"C:\Downloads\audio.mp3") { State = DownloadState.Downloading, BytesDownloaded = 2000, TotalBytes = 8000 }
        };

        _mockService.Setup(s => s.Downloads).Returns(existing);

        var vm = new DownloadsViewModel(_mockService.Object, _mockLibraryRepo.Object);
        await vm.InitializeAsync();

        vm.Downloads.Should().HaveCount(2);
        vm.Downloads[0].FileName.Should().Be("movie.mp4");
        vm.Downloads[0].State.Should().Be(DownloadState.Completed);
        vm.Downloads[1].FileName.Should().Be("audio.mp3");
        vm.Downloads[1].State.Should().Be(DownloadState.Downloading);
    }

    [Fact]
    public async Task AddDownloadAsync_CallsServiceAndAddsItemToCollection()
    {
        var newItem = new DownloadItem("https://example.com/clip.mp4", @"C:\Downloads\clip.mp4")
        {
            State = DownloadState.Queued
        };

        _mockService
            .Setup(s => s.EnqueueDownloadAsync("https://example.com/clip.mp4", null, null))
            .ReturnsAsync(Result<DownloadItem>.Success(newItem));

        var vm = new DownloadsViewModel(_mockService.Object, _mockLibraryRepo.Object);
        await vm.AddDownloadAsync("https://example.com/clip.mp4");

        _mockService.Verify(s => s.EnqueueDownloadAsync("https://example.com/clip.mp4", null, null), Times.Once);
        vm.Downloads.Should().ContainSingle(d => d.FileName == "clip.mp4");
    }

    [Fact]
    public async Task PauseAndResumeCommands_DispatchToService()
    {
        var item = new DownloadItem("https://example.com/clip.mp4", @"C:\Downloads\clip.mp4") { State = DownloadState.Downloading };
        var itemVm = new DownloadItemViewModel(item);

        _mockService.Setup(s => s.PauseDownloadAsync(item.Id)).ReturnsAsync(Result.Success());
        _mockService.Setup(s => s.ResumeDownloadAsync(item.Id)).ReturnsAsync(Result.Success());

        var vm = new DownloadsViewModel(_mockService.Object, _mockLibraryRepo.Object);

        await vm.PauseDownloadAsync(itemVm);
        _mockService.Verify(s => s.PauseDownloadAsync(item.Id), Times.Once);

        await vm.ResumeDownloadAsync(itemVm);
        _mockService.Verify(s => s.ResumeDownloadAsync(item.Id), Times.Once);
    }

    [Fact]
    public async Task ProgressChangedEvent_UpdatesCorrespondingItemViewModel()
    {
        var item = new DownloadItem("https://example.com/video.mp4", @"C:\Downloads\video.mp4")
        {
            Id = "dl-123",
            State = DownloadState.Downloading,
            BytesDownloaded = 0,
            TotalBytes = 1000000
        };

        _mockService.Setup(s => s.Downloads).Returns(new List<DownloadItem> { item });

        var vm = new DownloadsViewModel(_mockService.Object, _mockLibraryRepo.Object);
        await vm.InitializeAsync();

        // Raise progress event from service
        _mockService.Raise(
            s => s.ProgressChanged += null,
            _mockService.Object,
            new DownloadProgressEventArgs("dl-123", 500000, 1000000, 250000.0, TimeSpan.FromSeconds(2)));

        var target = vm.Downloads.First(d => d.Id == "dl-123");
        target.BytesDownloaded.Should().Be(500000);
        target.ProgressPercentage.Should().Be(50.0);
        target.SpeedText.Should().Contain("KB/s");
        target.EtaText.Should().Contain("2s remaining");
    }

    [Fact]
    public void OpenAddDownloadDialog_RaisesRequestAddDownloadDialogEvent()
    {
        var vm = new DownloadsViewModel(_mockService.Object, _mockLibraryRepo.Object);
        bool fired = false;
        vm.RequestAddDownloadDialog += () => fired = true;

        vm.OpenAddDownloadDialog();
        fired.Should().BeTrue();
    }
}
