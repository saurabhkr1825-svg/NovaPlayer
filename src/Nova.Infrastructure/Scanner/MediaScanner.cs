using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.Infrastructure.Scanner;

public class MediaScanner : IMediaScanner
{
    private readonly ILibraryRepository _libraryRepository;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".webm", ".mov", ".wmv", ".flv", ".ts",
        ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".opus", ".wma", ".aac"
    };

    public MediaScanner(ILibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository;
    }

    public async Task<Result<int>> ScanFolderAsync(string folderPath, bool recursive, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<int>.Failure("Scan was cancelled by user.");
        }

        try
        {
            return await Task.Run(async () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    {
                        return Result<int>.Failure($"Directory does not exist or is inaccessible: {folderPath}");
                    }

                    Log.Information("Starting media scan in {FolderPath} (Recursive={Recursive})", folderPath, recursive);

                    // 1. Discover all candidate files safely
                    var discoveredFilePaths = new List<string>();
                    DiscoverFilesSafely(folderPath, recursive, discoveredFilePaths, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Information("Media scan cancelled during file discovery.");
                        return Result<int>.Failure("Scan was cancelled by user.");
                    }

                int totalCount = discoveredFilePaths.Count;
                Log.Information("Discovered {Count} candidate media files in {FolderPath}", totalCount, folderPath);

                var scanProgress = new ScanProgress
                {
                    TotalFilesDiscovered = totalCount,
                    FilesProcessed = 0
                };
                progress?.Report(scanProgress);

                // 2. Process and extract metadata in batches
                int processed = 0;
                int addedTotal = 0;
                var batch = new List<MediaItem>(100);

                var lastProgressReport = DateTime.UtcNow;

                foreach (var filePath in discoveredFilePaths)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Commit partial batch before exiting
                        if (batch.Count > 0)
                        {
                            var saveResult = await _libraryRepository.AddMediaItemsBatchAsync(batch);
                            if (saveResult.IsSuccess) addedTotal += saveResult.Value;
                        }
                        Log.Information("Media scan cancelled by user. Persisted {Added} items before cancellation.", addedTotal);
                        return Result<int>.Failure("Scan was cancelled by user.");
                    }

                    try
                    {
                        var mediaItem = new MediaItem(filePath);
                        batch.Add(mediaItem);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to inspect file {FilePath}", filePath);
                    }

                    processed++;

                    // Commit batch when reaching 100 items
                    if (batch.Count >= 100)
                    {
                        var batchResult = await _libraryRepository.AddMediaItemsBatchAsync(batch);
                        if (batchResult.IsSuccess)
                        {
                            addedTotal += batchResult.Value;
                        }
                        batch.Clear();
                    }

                    // Throttle UI progress reports to max 10 Hz (every 100ms) or on completion
                    if ((DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 100 || processed == totalCount)
                    {
                        scanProgress.FilesProcessed = processed;
                        scanProgress.CurrentFileName = Path.GetFileName(filePath);
                        progress?.Report(scanProgress);
                        lastProgressReport = DateTime.UtcNow;
                    }
                }

                // Commit remaining batch
                if (batch.Count > 0)
                {
                    var finalResult = await _libraryRepository.AddMediaItemsBatchAsync(batch);
                    if (finalResult.IsSuccess)
                    {
                        addedTotal += finalResult.Value;
                    }
                }

                scanProgress.IsCompleted = true;
                scanProgress.FilesProcessed = processed;
                progress?.Report(scanProgress);

                Log.Information("Media scan finished successfully. Added/Verified {Count} media items.", addedTotal);
                return Result<int>.Success(addedTotal);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Media scan cancelled by user.");
                return Result<int>.Failure("Scan was cancelled by user.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error scanning folder {FolderPath}", folderPath);
                return Result<int>.Failure($"Media scan failure: {ex.Message}");
            }
        });
        }
        catch (OperationCanceledException)
        {
            Log.Information("Media scan cancelled by user.");
            return Result<int>.Failure("Scan was cancelled by user.");
        }
    }

    private static void DiscoverFilesSafely(string dir, bool recursive, List<string> discovered, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        try
        {
            var dirInfo = new DirectoryInfo(dir);
            // Guard against junctions, symlinks, or reparse points to avoid recursion cycles
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                Log.Debug("Skipping reparse point / junction directory: {Dir}", dir);
                return;
            }

            // Enumerate files
            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (ct.IsCancellationRequested) return;

                if (SupportedExtensions.Contains(file.Extension))
                {
                    discovered.Add(file.FullName);
                }
            }

            // Recurse subdirectories if enabled
            if (recursive)
            {
                foreach (var subDir in dirInfo.EnumerateDirectories())
                {
                    if (ct.IsCancellationRequested) return;

                    // Skip hidden or system folders (e.g. $RECYCLE.BIN, System Volume Information, .git)
                    if ((subDir.Attributes & FileAttributes.Hidden) != 0 ||
                        (subDir.Attributes & FileAttributes.System) != 0 ||
                        subDir.Name.StartsWith('.'))
                    {
                        continue;
                    }

                    DiscoverFilesSafely(subDir.FullName, recursive, discovered, ct);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug("Access denied to directory {Dir}: {Message}", dir, ex.Message);
        }
        catch (PathTooLongException ex)
        {
            Log.Warning("Path too long skipping {Dir}: {Message}", dir, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to enumerate directory {Dir}: {Message}", dir, ex.Message);
        }
    }
}
