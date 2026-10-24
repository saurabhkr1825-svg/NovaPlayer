using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Persistence;
using Nova.Infrastructure.Playlists;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class M3uPlaylistServiceTests
{
    private static async Task<(SqliteLibraryRepository repo, M3uPlaylistService service)> CreatePlaylistFixtureAsync()
    {
        var dbPath = $"file:memdb_m3u_{Guid.NewGuid()}?mode=memory&cache=shared";
        var dbContext = new DatabaseContext(dbPath);
        var repo = new SqliteLibraryRepository(dbContext);
        await repo.InitializeAsync();
        var service = new M3uPlaylistService(repo);
        return (repo, service);
    }

    [Fact]
    public async Task ImportAndExportM3u_WithRelativePaths_OperatesCorrectly()
    {
        var (repo, service) = await CreatePlaylistFixtureAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "NovaM3uTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var song1 = Path.Combine(tempDir, "song1.mp3");
            var song2 = Path.Combine(tempDir, "song2.mp3");
            File.WriteAllText(song1, "audio content");
            File.WriteAllText(song2, "audio content");

            // Write M3U file
            var m3uContent = @"#EXTM3U
#EXTINF:180,Song One Title
song1.mp3
#EXTINF:240,Song Two Title
song2.mp3
";
            var m3uPath = Path.Combine(tempDir, "MyMix.m3u");
            await File.WriteAllTextAsync(m3uPath, m3uContent);

            // Import
            var importResult = await service.ImportM3uAsync(m3uPath);
            importResult.IsSuccess.Should().BeTrue();
            var playlist = importResult.Value;
            playlist.Name.Should().Be("MyMix");

            var items = (await repo.GetPlaylistItemsAsync(playlist.Id)).Value;
            items.Should().HaveCount(2);
            items[0].MediaItem!.Title.Should().Be("Song One Title");
            items[1].MediaItem!.Title.Should().Be("Song Two Title");

            // Export to a new M3U file
            var exportPath = Path.Combine(tempDir, "ExportedMix.m3u");
            var exportResult = await service.ExportM3uAsync(playlist.Id, exportPath, useRelativePaths: true);
            exportResult.IsSuccess.Should().BeTrue();

            File.Exists(exportPath).Should().BeTrue();
            var exportedLines = await File.ReadAllLinesAsync(exportPath);
            exportedLines.Should().Contain("#EXTM3U");
            exportedLines.Should().Contain(l => l.Contains("Song One Title"));
            exportedLines.Should().Contain("song1.mp3");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ImportM3u_WithNonExistentFile_FailsGracefully()
    {
        var (_, service) = await CreatePlaylistFixtureAsync();

        var result = await service.ImportM3uAsync(@"C:\NonExistent\FakePlaylist.m3u");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Shuffle_ShouldPreserveAllItemsAndChangeOrder()
    {
        var (_, service) = await CreatePlaylistFixtureAsync();

        var originalList = new List<PlaylistItem>();
        for (int i = 0; i < 20; i++)
        {
            originalList.Add(new PlaylistItem("pid", $"media_{i}", i));
        }

        var shuffled = service.Shuffle(originalList);

        shuffled.Should().HaveCount(originalList.Count);
        shuffled.Select(i => i.MediaItemId).Should().BeEquivalentTo(originalList.Select(i => i.MediaItemId));
    }
}
