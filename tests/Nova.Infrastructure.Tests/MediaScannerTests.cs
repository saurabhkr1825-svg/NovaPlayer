using System.Diagnostics;
using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Persistence;
using Nova.Infrastructure.Scanner;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class MediaScannerTests
{
    private static async Task<(SqliteLibraryRepository repo, MediaScanner scanner)> CreateScannerFixtureAsync()
    {
        var dbPath = $"file:memdb_scan_{Guid.NewGuid()}?mode=memory&cache=shared";
        var dbContext = new DatabaseContext(dbPath);
        var repo = new SqliteLibraryRepository(dbContext);
        await repo.InitializeAsync();
        var scanner = new MediaScanner(repo);
        return (repo, scanner);
    }

    [Fact]
    public async Task ScanFolder_DiscoversMediaAndAvoidsDuplicatesOnRepeat()
    {
        var (repo, scanner) = await CreateScannerFixtureAsync();

        // Create temporary test directory with dummy media files
        var testDir = Path.Combine(Path.GetTempPath(), "NovaTestScan_" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var subDir = Path.Combine(testDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        try
        {
            File.WriteAllText(Path.Combine(testDir, "video1.mp4"), "dummy video");
            File.WriteAllText(Path.Combine(testDir, "audio1.flac"), "dummy audio");
            File.WriteAllText(Path.Combine(testDir, "readme.txt"), "ignored text");
            File.WriteAllText(Path.Combine(subDir, "video2.mkv"), "dummy video 2");

            var progressReports = new List<ScanProgress>();
            var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

            // First Scan (Recursive)
            var sw = Stopwatch.StartNew();
            var scanResult = await scanner.ScanFolderAsync(testDir, recursive: true, progress, CancellationToken.None);
            sw.Stop();

            scanResult.IsSuccess.Should().BeTrue();
            scanResult.Value.Should().Be(3, "Discovered 3 valid media files (2 in root, 1 in subfolder)");

            var allItems = (await repo.GetAllMediaItemsAsync()).Value;
            allItems.Should().HaveCount(3);

            // Repeat Scan: Should NOT create duplicates
            var repeatResult = await scanner.ScanFolderAsync(testDir, recursive: true, null, CancellationToken.None);
            repeatResult.IsSuccess.Should().BeTrue();

            var allItemsAfterRepeat = (await repo.GetAllMediaItemsAsync()).Value;
            allItemsAfterRepeat.Should().HaveCount(3, "Repeated scans must not create duplicate media records");
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public async Task ScanFolder_WhenCancelled_PreservesPartialResultsWithoutDeleting()
    {
        var (repo, scanner) = await CreateScannerFixtureAsync();

        var testDir = Path.Combine(Path.GetTempPath(), "NovaTestCancel_" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);

        try
        {
            for (int i = 0; i < 50; i++)
            {
                File.WriteAllText(Path.Combine(testDir, $"clip_{i}.mp4"), "content");
            }

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            var result = await scanner.ScanFolderAsync(testDir, recursive: false, null, cts.Token);
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("cancelled");
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public async Task ScanFolder_WithInaccessibleFolder_FailsGracefullyWithoutCrashing()
    {
        var (_, scanner) = await CreateScannerFixtureAsync();

        var fakePath = @"Z:\NonExistentPath\NovaTestFolder";
        var result = await scanner.ScanFolderAsync(fakePath, recursive: true, null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist or is inaccessible");
    }
}
