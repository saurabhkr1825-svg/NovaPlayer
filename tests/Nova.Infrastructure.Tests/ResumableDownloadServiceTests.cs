using System.Net;
using System.Text;
using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Downloads;
using Nova.Infrastructure.Persistence;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class ResumableDownloadServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempDbPath;
    private readonly DatabaseContext _dbContext;
    private readonly SqliteDownloadRepository _repo;

    public ResumableDownloadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nova_dl_svc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _tempDbPath = Path.Combine(_tempDir, "test.db");
        _dbContext = new DatabaseContext(_tempDbPath);
        _dbContext.InitializeAndMigrateAsync().GetAwaiter().GetResult();
        _repo = new SqliteDownloadRepository(_dbContext);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8080/secret.mp4")]
    [InlineData("http://192.168.1.50/private.mkv")]
    [InlineData("http://10.0.0.1/intranet.mp4")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public async Task EnqueueDownload_RestrictedIpUrls_AreBlockedBySsrfGuard(string restrictedUrl)
    {
        using var service = new ResumableDownloadService(_repo)
        {
            DefaultDownloadDirectory = _tempDir
        };

        var result = await service.EnqueueDownloadAsync(restrictedUrl);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("SSRF prevention");
    }

    [Fact]
    public async Task EnqueueDownload_UrlsWithEmbeddedCredentials_AreRejected()
    {
        using var service = new ResumableDownloadService(_repo)
        {
            DefaultDownloadDirectory = _tempDir
        };

        var result = await service.EnqueueDownloadAsync("http://admin:secret@example.com/video.mp4");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("embedded user credentials");
    }

    [Fact]
    public async Task EnqueueDownload_PathTraversalInFilename_IsSanitizedInsideDestination()
    {
        using var service = new ResumableDownloadService(_repo)
        {
            DefaultDownloadDirectory = _tempDir
        };

        // Note: example.com resolves to public IP, so it passes DNS/IP check
        var result = await service.EnqueueDownloadAsync(
            "https://example.com/video.mp4",
            destinationDirectory: _tempDir,
            customFileName: @"../../Windows/System32/cmd.exe");

        result.IsSuccess.Should().BeTrue();
        var item = result.Value;
        item.FileName.Should().Be("cmd.exe");
        item.DestinationPath.Should().StartWith(_tempDir);
        File.Exists(item.DestinationPath).Should().BeFalse();
    }

    [Fact]
    public async Task Download_WithRangeResumeSupport_CompletesAndPromotesPartFile()
    {
        var testData = Encoding.UTF8.GetBytes("NovaPlayer Full Video Content Streaming Payload 1234567890");
        int halfLength = testData.Length / 2;

        // Custom mock HttpMessageHandler that supports Range requests
        var mockHandler = new MockRangeHttpMessageHandler(testData);
        var mockClient = new HttpClient(mockHandler);

        using var service = new ResumableDownloadService(_repo, mockClient)
        {
            DefaultDownloadDirectory = _tempDir
        };

        var enqueueResult = await service.EnqueueDownloadAsync(
            "https://example.com/resumable_media.mp4",
            destinationDirectory: _tempDir,
            customFileName: "resumable_media.mp4");

        enqueueResult.IsSuccess.Should().BeTrue();
        var item = enqueueResult.Value;

        // Wait for download to complete
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (item.State != DownloadState.Completed && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        item.State.Should().Be(DownloadState.Completed);
        File.Exists(item.DestinationPath).Should().BeTrue();
        File.Exists(item.TempFilePath).Should().BeFalse("Part file must be promoted and removed");

        var downloadedBytes = await File.ReadAllBytesAsync(item.DestinationPath);
        downloadedBytes.Should().Equal(testData);
    }

    [Fact]
    public async Task InitializeAsync_ReconcilesInterruptedDownloadsToPaused()
    {
        var interruptedItem = new DownloadItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = "https://example.com/interrupted.mp4",
            DestinationPath = Path.Combine(_tempDir, "interrupted.mp4"),
            TempFilePath = Path.Combine(_tempDir, "interrupted.mp4.part"),
            FileName = "interrupted.mp4",
            State = DownloadState.Downloading,
            BytesDownloaded = 1000
        };

        await _repo.AddDownloadAsync(interruptedItem);

        using var service = new ResumableDownloadService(_repo)
        {
            DefaultDownloadDirectory = _tempDir
        };

        var initResult = await service.InitializeAsync();
        initResult.IsSuccess.Should().BeTrue();

        var loaded = service.Downloads.FirstOrDefault(d => d.Id == interruptedItem.Id);
        loaded.Should().NotBeNull();
        loaded!.State.Should().Be(DownloadState.Paused);
        loaded.ErrorMessage.Should().Contain("interrupted");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch { }
        }
    }

    private class MockRangeHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _data;

        public MockRangeHttpMessageHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range;
            if (range != null && range.Ranges.Count > 0)
            {
                var from = range.Ranges.First().From ?? 0;
                long length = _data.Length - from;

                var content = new ByteArrayContent(_data, (int)from, (int)length);
                content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, _data.Length - 1, _data.Length);
                content.Headers.ContentLength = length;

                var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = content
                };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag-test-123\"");
                return Task.FromResult(response);
            }
            else
            {
                var content = new ByteArrayContent(_data);
                content.Headers.ContentLength = _data.Length;

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag-test-123\"");
                return Task.FromResult(response);
            }
        }
    }
}
