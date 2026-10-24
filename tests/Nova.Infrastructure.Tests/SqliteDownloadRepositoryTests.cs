using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Persistence;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class SqliteDownloadRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _dbContext;
    private readonly SqliteDownloadRepository _repo;

    public SqliteDownloadRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"nova_dl_test_{Guid.NewGuid():N}.db");
        _dbContext = new DatabaseContext(_tempDbPath);
        _dbContext.InitializeAndMigrateAsync().GetAwaiter().GetResult();
        _repo = new SqliteDownloadRepository(_dbContext);
    }

    [Fact]
    public async Task AddAndGetDownload_ReturnsPersistedItem()
    {
        var item = new DownloadItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = "https://example.com/media/video.mp4",
            DestinationPath = @"C:\Downloads\video.mp4",
            TempFilePath = @"C:\Downloads\video.mp4.part",
            FileName = "video.mp4",
            State = DownloadState.Queued,
            BytesDownloaded = 0,
            TotalBytes = 1048576,
            ETag = "\"abc123etag\"",
            LastModified = "Wed, 21 Oct 2026 07:28:00 GMT"
        };

        var addResult = await _repo.AddDownloadAsync(item);
        addResult.IsSuccess.Should().BeTrue();

        var getResult = await _repo.GetDownloadAsync(item.Id);
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().NotBeNull();

        var loaded = getResult.Value!;
        loaded.Id.Should().Be(item.Id);
        loaded.Url.Should().Be(item.Url);
        loaded.DestinationPath.Should().Be(item.DestinationPath);
        loaded.FileName.Should().Be(item.FileName);
        loaded.State.Should().Be(DownloadState.Queued);
        loaded.TotalBytes.Should().Be(1048576);
        loaded.ETag.Should().Be("\"abc123etag\"");
    }

    [Fact]
    public async Task UpdateDownloadProgress_UpdatesBytesAndEtag()
    {
        var item = new DownloadItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = "https://example.com/stream.mp4",
            DestinationPath = @"C:\Downloads\stream.mp4",
            TempFilePath = @"C:\Downloads\stream.mp4.part",
            FileName = "stream.mp4",
            State = DownloadState.Downloading,
            BytesDownloaded = 0
        };

        await _repo.AddDownloadAsync(item);

        var updateResult = await _repo.UpdateDownloadProgressAsync(
            item.Id,
            bytesDownloaded: 524288,
            totalBytes: 2097152,
            etag: "\"new_etag\"",
            lastModified: "Thu, 22 Oct 2026 10:00:00 GMT");

        updateResult.IsSuccess.Should().BeTrue();

        var getResult = await _repo.GetDownloadAsync(item.Id);
        getResult.IsSuccess.Should().BeTrue();
        var updated = getResult.Value!;
        updated.BytesDownloaded.Should().Be(524288);
        updated.TotalBytes.Should().Be(2097152);
        updated.ETag.Should().Be("\"new_etag\"");
    }

    [Fact]
    public async Task UpdateDownloadState_UpdatesStateAndErrorMessage()
    {
        var item = new DownloadItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = "https://example.com/audio.flac",
            DestinationPath = @"C:\Downloads\audio.flac",
            TempFilePath = @"C:\Downloads\audio.flac.part",
            FileName = "audio.flac",
            State = DownloadState.Downloading
        };

        await _repo.AddDownloadAsync(item);

        await _repo.UpdateDownloadStateAsync(item.Id, DownloadState.Failed, "Server returned 503 Service Unavailable");

        var getResult = await _repo.GetDownloadAsync(item.Id);
        getResult.IsSuccess.Should().BeTrue();
        var updated = getResult.Value!;
        updated.State.Should().Be(DownloadState.Failed);
        updated.ErrorMessage.Should().Be("Server returned 503 Service Unavailable");
    }

    [Fact]
    public async Task DeleteDownload_RemovesRecordFromDatabase()
    {
        var item = new DownloadItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = "https://example.com/del.mkv",
            DestinationPath = @"C:\Downloads\del.mkv",
            TempFilePath = @"C:\Downloads\del.mkv.part",
            FileName = "del.mkv",
            State = DownloadState.Completed
        };

        await _repo.AddDownloadAsync(item);

        var delResult = await _repo.DeleteDownloadAsync(item.Id);
        delResult.IsSuccess.Should().BeTrue();

        var getResult = await _repo.GetDownloadAsync(item.Id);
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().BeNull();
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try
            {
                File.Delete(_tempDbPath);
            }
            catch { }
        }
    }
}
