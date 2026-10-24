using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class LibraryViewModelTests
{
    private readonly Mock<ILibraryRepository> _mockRepo;
    private readonly Mock<IMediaScanner> _mockScanner;
    private readonly Mock<IPlaylistService> _mockPlaylistService;

    public LibraryViewModelTests()
    {
        _mockRepo = new Mock<ILibraryRepository>();
        _mockScanner = new Mock<IMediaScanner>();
        _mockPlaylistService = new Mock<IPlaylistService>();
    }

    [Fact]
    public async Task LoadAllAsync_ShouldPopulateMediaItemsPlaylistsAndHistory()
    {
        var mediaItems = new List<MediaItem>
        {
            new("C:\\Videos\\Movie1.mp4") { Title = "Movie 1", DurationMs = 60000 },
            new("C:\\Music\\Song1.mp3") { Title = "Song 1", DurationMs = 30000 }
        };
        var playlists = new List<Playlist>
        {
            new() { Id = "pl-1", Name = "Favorites" }
        };
        var history = new List<PlaybackHistoryEntry>
        {
            new() { Id = "h-1", Title = "Movie 1", FilePathOrUrl = "C:\\Videos\\Movie1.mp4", PositionMs = 12000, DurationMs = 60000 }
        };

        _mockRepo.Setup(r => r.GetAllMediaItemsAsync(null)).ReturnsAsync(Result<IReadOnlyList<MediaItem>>.Success(mediaItems));
        _mockRepo.Setup(r => r.GetAllPlaylistsAsync()).ReturnsAsync(Result<IReadOnlyList<Playlist>>.Success(playlists));
        _mockRepo.Setup(r => r.GetRecentHistoryAsync(100)).ReturnsAsync(Result<IReadOnlyList<PlaybackHistoryEntry>>.Success(history));

        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        await vm.LoadAllAsync();

        vm.MediaItems.Should().HaveCount(2);
        vm.Playlists.Should().HaveCount(1);
        vm.PlaybackHistory.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchText_ShouldFilterMediaItemsByTitle()
    {
        var mediaItems = new List<MediaItem>
        {
            new("C:\\Videos\\Inception.mkv") { Title = "Inception" },
            new("C:\\Videos\\Interstellar.mkv") { Title = "Interstellar" },
            new("C:\\Videos\\Avatar.mp4") { Title = "Avatar" }
        };

        _mockRepo.Setup(r => r.GetAllMediaItemsAsync(null)).ReturnsAsync(Result<IReadOnlyList<MediaItem>>.Success(mediaItems));

        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        await vm.LoadMediaItemsAsync();

        vm.MediaItems.Should().HaveCount(3);

        vm.SearchText = "inter";
        vm.MediaItems.Should().HaveCount(1);
        vm.MediaItems[0].Title.Should().Be("Interstellar");

        vm.SearchText = string.Empty;
        vm.MediaItems.Should().HaveCount(3);
    }

    [Fact]
    public async Task SelectedFilter_ShouldFilterByCategoryOrFavorites()
    {
        var mediaItems = new List<MediaItem>
        {
            new("C:\\Videos\\Movie.mp4") { Title = "Movie", IsFavorite = false },
            new("C:\\Music\\Track.mp3") { Title = "Track", IsFavorite = true }
        };

        _mockRepo.Setup(r => r.GetAllMediaItemsAsync(null)).ReturnsAsync(Result<IReadOnlyList<MediaItem>>.Success(mediaItems));

        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        await vm.LoadMediaItemsAsync();

        vm.SelectedFilter = "Video";
        vm.MediaItems.Should().HaveCount(1);
        vm.MediaItems[0].Title.Should().Be("Movie");

        vm.SelectedFilter = "Audio";
        vm.MediaItems.Should().HaveCount(1);
        vm.MediaItems[0].Title.Should().Be("Track");

        vm.SelectedFilter = "Favorites";
        vm.MediaItems.Should().HaveCount(1);
        vm.MediaItems[0].Title.Should().Be("Track");
    }

    [Fact]
    public async Task CreatePlaylist_ShouldInvokeRepositoryAndAddToList()
    {
        var created = new Playlist { Id = "pl-new", Name = "Rock Classics" };
        _mockRepo.Setup(r => r.CreatePlaylistAsync("Rock Classics")).ReturnsAsync(Result<Playlist>.Success(created));

        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        await vm.CreatePlaylistAsync("Rock Classics");

        _mockRepo.Verify(r => r.CreatePlaylistAsync("Rock Classics"), Times.Once);
        vm.Playlists.Should().Contain(created);
        vm.SelectedPlaylist.Should().Be(created);
    }

    [Fact]
    public async Task DeletePlaylist_ShouldInvokeRepositoryAndRemoveFromList()
    {
        var playlist = new Playlist { Id = "pl-del", Name = "To Delete" };
        _mockRepo.Setup(r => r.DeletePlaylistAsync("pl-del")).ReturnsAsync(Result.Success());

        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        vm.Playlists.Add(playlist);
        vm.SelectedPlaylist = playlist;

        await vm.DeletePlaylistAsync(playlist);

        _mockRepo.Verify(r => r.DeletePlaylistAsync("pl-del"), Times.Once);
        vm.Playlists.Should().NotContain(playlist);
    }

    [Fact]
    public void PlayMediaItem_ShouldRaisePlayMediaRequestedEvent()
    {
        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        var item = new MediaItem("C:\\Videos\\Clip.mp4") { Title = "Clip" };

        string? playedPath = null;
        vm.PlayMediaRequested += (s, path) => playedPath = path;

        vm.PlayMediaItem(item);

        playedPath.Should().Be("C:\\Videos\\Clip.mp4");
    }

    [Fact]
    public void PlayPlaylist_ShouldRaisePlayQueueRequestedEvent()
    {
        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        var playlist = new Playlist { Id = "pl-1", Name = "My List" };
        var item1 = new PlaylistItem
        {
            Id = "pi-1",
            MediaItem = new MediaItem("C:\\Videos\\Part1.mp4") { Title = "Part 1" }
        };
        var item2 = new PlaylistItem
        {
            Id = "pi-2",
            MediaItem = new MediaItem("C:\\Videos\\Part2.mp4") { Title = "Part 2" }
        };

        vm.PlaylistItems.Add(item1);
        vm.PlaylistItems.Add(item2);

        IReadOnlyList<string>? queuePaths = null;
        int queueStart = -1;
        vm.PlayQueueRequested += (s, req) =>
        {
            queuePaths = req.items;
            queueStart = req.startIndex;
        };

        vm.PlayPlaylist(playlist);

        queuePaths.Should().NotBeNull();
        queuePaths.Should().HaveCount(2);
        queuePaths![0].Should().Be("C:\\Videos\\Part1.mp4");
        queuePaths[1].Should().Be("C:\\Videos\\Part2.mp4");
        queueStart.Should().Be(0);
    }

    [Fact]
    public async Task ClearHistory_ShouldClearPlaybackHistory()
    {
        _mockRepo.Setup(r => r.ClearHistoryAsync()).ReturnsAsync(Result.Success());

        using var vm = new LibraryViewModel(_mockRepo.Object, _mockScanner.Object, _mockPlaylistService.Object);
        vm.PlaybackHistory.Add(new PlaybackHistoryEntry { Title = "Old Item" });

        await vm.ClearHistoryAsync();

        _mockRepo.Verify(r => r.ClearHistoryAsync(), Times.Once);
        vm.PlaybackHistory.Should().BeEmpty();
    }
}
