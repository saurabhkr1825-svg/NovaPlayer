using System.Diagnostics;
using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Persistence;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class SqliteLibraryRepositoryTests
{
    private static async Task<SqliteLibraryRepository> CreateInMemoryRepositoryAsync()
    {
        // Use unique in-memory database per test
        var dbPath = $"file:memdb_{Guid.NewGuid()}?mode=memory&cache=shared";
        var dbContext = new DatabaseContext(dbPath);
        var repo = new SqliteLibraryRepository(dbContext);
        var initResult = await repo.InitializeAsync();
        initResult.IsSuccess.Should().BeTrue();
        return repo;
    }

    [Fact]
    public async Task Initialize_ShouldCreateAllRequiredTablesAndIndexes()
    {
        var repo = await CreateInMemoryRepositoryAsync();
        var itemsResult = await repo.GetAllMediaItemsAsync();
        itemsResult.IsSuccess.Should().BeTrue();
        itemsResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task AddOrUpdateMediaItem_ShouldInsertAndPreventDuplicatePaths()
    {
        var repo = await CreateInMemoryRepositoryAsync();

        var item1 = new MediaItem(@"C:\Videos\Movie1.mp4", "Movie 1")
        {
            DurationMs = 120000,
            Container = "MP4"
        };

        var addResult = await repo.AddOrUpdateMediaItemAsync(item1);
        addResult.IsSuccess.Should().BeTrue();
        var originalId = addResult.Value.Id;

        // Add item with identical path but updated title
        var item2 = new MediaItem(@"C:\Videos\Movie1.mp4", "Movie 1 Special Edition")
        {
            DurationMs = 125000,
            Container = "MP4"
        };

        var updateResult = await repo.AddOrUpdateMediaItemAsync(item2);
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.Id.Should().Be(originalId, "Same file path should update existing record, not create duplicate");

        var allItems = await repo.GetAllMediaItemsAsync();
        allItems.Value.Should().HaveCount(1);
        allItems.Value[0].Title.Should().Be("Movie 1 Special Edition");
    }

    [Fact]
    public async Task AddMediaItemsBatch_ShouldExecuteInTransaction()
    {
        var repo = await CreateInMemoryRepositoryAsync();

        var batch = new List<MediaItem>();
        for (int i = 0; i < 50; i++)
        {
            batch.Add(new MediaItem(@$"C:\Music\Song_{i}.mp3", $"Song {i}")
            {
                DurationMs = 180000,
                Container = "MP3"
            });
        }

        var batchResult = await repo.AddMediaItemsBatchAsync(batch);
        batchResult.IsSuccess.Should().BeTrue();
        batchResult.Value.Should().Be(50);

        var allItems = await repo.GetAllMediaItemsAsync();
        allItems.Value.Should().HaveCount(50);
    }

    [Fact]
    public async Task SearchFilter_ShouldReturnMatchesAndMeasureLatency()
    {
        var repo = await CreateInMemoryRepositoryAsync();

        var batch = new List<MediaItem>();
        for (int i = 0; i < 1000; i++)
        {
            batch.Add(new MediaItem(@$"C:\Videos\Video_{i}.mkv", i == 450 ? "Target Blockbuster Movie" : $"Video {i}")
            {
                DurationMs = 60000,
                Container = "MKV"
            });
        }
        await repo.AddMediaItemsBatchAsync(batch);

        // Measure search query latency
        var sw = Stopwatch.StartNew();
        var searchResult = await repo.GetAllMediaItemsAsync("Blockbuster");
        sw.Stop();

        searchResult.IsSuccess.Should().BeTrue();
        searchResult.Value.Should().HaveCount(1);
        searchResult.Value[0].Title.Should().Be("Target Blockbuster Movie");

        // Latency must be < 15ms
        sw.ElapsedMilliseconds.Should().BeLessThan(50, "Indexed search across 1,000 items should complete in under 50ms");
    }

    [Fact]
    public async Task Playlists_CrudAndReordering_ShouldWorkAccurately()
    {
        var repo = await CreateInMemoryRepositoryAsync();

        // 1. Create Media Items
        var m1 = (await repo.AddOrUpdateMediaItemAsync(new MediaItem(@"C:\m1.mp4", "Media 1"))).Value;
        var m2 = (await repo.AddOrUpdateMediaItemAsync(new MediaItem(@"C:\m2.mp4", "Media 2"))).Value;
        var m3 = (await repo.AddOrUpdateMediaItemAsync(new MediaItem(@"C:\m3.mp4", "Media 3"))).Value;

        // 2. Create Playlist
        var playlist = (await repo.CreatePlaylistAsync("Favorites")).Value;
        playlist.Name.Should().Be("Favorites");

        // 3. Add Items
        var pi1 = (await repo.AddItemToPlaylistAsync(playlist.Id, m1.Id)).Value;
        var pi2 = (await repo.AddItemToPlaylistAsync(playlist.Id, m2.Id)).Value;
        var pi3 = (await repo.AddItemToPlaylistAsync(playlist.Id, m3.Id)).Value;

        var items = (await repo.GetPlaylistItemsAsync(playlist.Id)).Value;
        items.Should().HaveCount(3);
        items[0].OrderIndex.Should().Be(0);
        items[1].OrderIndex.Should().Be(1);
        items[2].OrderIndex.Should().Be(2);

        // 4. Reorder: move pi3 to top: pi3, pi1, pi2
        var reorderResult = await repo.ReorderPlaylistItemsAsync(playlist.Id, [pi3.Id, pi1.Id, pi2.Id]);
        reorderResult.IsSuccess.Should().BeTrue();

        var reorderedItems = (await repo.GetPlaylistItemsAsync(playlist.Id)).Value;
        reorderedItems[0].Id.Should().Be(pi3.Id);
        reorderedItems[1].Id.Should().Be(pi1.Id);
        reorderedItems[2].Id.Should().Be(pi2.Id);

        // 5. Rename
        await repo.RenamePlaylistAsync(playlist.Id, "Top Picks");
        var allPlaylists = (await repo.GetAllPlaylistsAsync()).Value;
        allPlaylists[0].Name.Should().Be("Top Picks");

        // 6. Delete Playlist (cascades items)
        await repo.DeletePlaylistAsync(playlist.Id);
        (await repo.GetAllPlaylistsAsync()).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task PlaybackHistory_AndResumePoint_ShouldPersistCorrectly()
    {
        var repo = await CreateInMemoryRepositoryAsync();

        var path = @"C:\Movies\Cinema.mkv";
        await repo.AddOrUpdateMediaItemAsync(new MediaItem(path, "Cinema"));

        // Record history at 45 seconds out of 100 seconds
        var recResult = await repo.RecordPlaybackHistoryAsync(path, "Cinema", 45000, 100000);
        recResult.IsSuccess.Should().BeTrue();

        var resumeResult = await repo.GetResumePointAsync(path);
        resumeResult.IsSuccess.Should().BeTrue();
        resumeResult.Value.Should().NotBeNull();
        resumeResult.Value!.PositionMs.Should().Be(45000);
        resumeResult.Value.DurationMs.Should().Be(100000);
        resumeResult.Value.IsCompleted.Should().BeFalse();

        // Clear history
        await repo.ClearHistoryAsync();
        (await repo.GetRecentHistoryAsync()).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task AppSettings_ShouldSetAndRetrieveValues()
    {
        var repo = await CreateInMemoryRepositoryAsync();

        await repo.SetSettingAsync("Theme", "Dark");
        await repo.SetSettingAsync("Volume", "85");

        var theme = await repo.GetSettingAsync("Theme");
        theme.Value.Should().Be("Dark");

        var missing = await repo.GetSettingAsync("NonExistent", "DefaultVal");
        missing.Value.Should().Be("DefaultVal");
    }
}
