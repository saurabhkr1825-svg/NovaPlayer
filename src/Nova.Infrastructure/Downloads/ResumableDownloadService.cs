using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.Infrastructure.Downloads;

public class ResumableDownloadService : IDownloadService
{
    private readonly IDownloadRepository _repository;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, DownloadItem> _downloads = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCts = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly object _queueLock = new();
    private bool _isDisposed;

    private const int MaxRedirects = 5;
    private const int BufferSize = 65536; // 64 KB

    public int MaxConcurrentDownloads { get; set; } = 3;
    public string DefaultDownloadDirectory { get; set; }

    public IReadOnlyList<DownloadItem> Downloads => _downloads.Values
        .OrderByDescending(d => d.CreatedAt)
        .ToList()
        .AsReadOnly();

    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
    public event EventHandler<DownloadStateChangedEventArgs>? StateChanged;

    public ResumableDownloadService(IDownloadRepository repository, HttpClient? httpClient = null)
    {
        _repository = repository;
        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);

        var downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "NovaPlayer");

        if (!Directory.Exists(downloadsFolder))
        {
            try
            {
                Directory.CreateDirectory(downloadsFolder);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not create default downloads folder {Path}", downloadsFolder);
                downloadsFolder = Path.GetTempPath();
            }
        }
        DefaultDownloadDirectory = downloadsFolder;

        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false, // Security: Manual redirect loop with SSRF validation
                AutomaticDecompression = DecompressionMethods.None
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
    }

    public async Task<Result> InitializeAsync()
    {
        try
        {
            var result = await _repository.GetAllDownloadsAsync();
            if (result.IsFailure) return Result.Failure(result.Error ?? "Failed to load downloads.");

            foreach (var item in result.Value)
            {
                // Reconcile download state with actual filesystem
                if (item.State == DownloadState.Downloading)
                {
                    // Interrupted state -> pause on restart
                    item.State = DownloadState.Paused;
                    item.ErrorMessage = "Download interrupted by application shutdown.";
                    await _repository.UpdateDownloadStateAsync(item.Id, DownloadState.Paused, item.ErrorMessage);
                }
                else if (item.State == DownloadState.Completed)
                {
                    // Verify that completed file actually exists
                    if (!File.Exists(item.DestinationPath))
                    {
                        item.ErrorMessage = "Completed file not found on disk.";
                    }
                }
                else if (item.State == DownloadState.Paused || item.State == DownloadState.Queued)
                {
                    if (File.Exists(item.TempFilePath))
                    {
                        item.BytesDownloaded = new FileInfo(item.TempFilePath).Length;
                    }
                    else
                    {
                        item.BytesDownloaded = 0;
                    }
                }

                _downloads[item.Id] = item;
            }

            Log.Information("Initialized ResumableDownloadService with {Count} persisted downloads.", _downloads.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize download service.");
            return Result.Failure($"Download service initialization failed: {ex.Message}");
        }
    }

    public async Task<Result<DownloadItem>> EnqueueDownloadAsync(
        string url,
        string? destinationDirectory = null,
        string? customFileName = null)
    {
        // 1. Validate URL structure & scheme
        var urlResult = SecurityValidator.ValidateUrlStructure(url);
        if (urlResult.IsFailure)
        {
            return Result<DownloadItem>.Failure(urlResult.Error ?? "Invalid URL.");
        }
        var uri = urlResult.Value;

        // 2. Validate DNS & IP (SSRF guard)
        var dnsResult = await SecurityValidator.ValidateDnsAndIpAsync(uri.Host);
        if (dnsResult.IsFailure)
        {
            return Result<DownloadItem>.Failure(dnsResult.Error ?? "DNS validation failed.");
        }

        // 3. Resolve destination directory
        var targetDir = !string.IsNullOrWhiteSpace(destinationDirectory)
            ? destinationDirectory
            : DefaultDownloadDirectory;

        try
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
        }
        catch (Exception ex)
        {
            return Result<DownloadItem>.Failure($"Invalid destination directory: {ex.Message}");
        }

        // 4. Derive and sanitize filename
        string rawFileName = customFileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            var pathSegment = uri.AbsolutePath.TrimEnd('/');
            rawFileName = Path.GetFileName(pathSegment);
            if (string.IsNullOrWhiteSpace(rawFileName))
            {
                rawFileName = "download.bin";
            }
        }

        var sanitizedFileName = SecurityValidator.SanitizeFileName(rawFileName);
        var finalPath = SecurityValidator.GetUniqueFilePath(targetDir, sanitizedFileName);

        // Verify path containment
        if (!SecurityValidator.VerifyPathContainment(targetDir, finalPath))
        {
            return Result<DownloadItem>.Failure("Resolved file path escapes the destination directory.");
        }

        var downloadItem = new DownloadItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = uri.AbsoluteUri,
            DestinationPath = finalPath,
            TempFilePath = $"{finalPath}.part",
            FileName = Path.GetFileName(finalPath),
            State = DownloadState.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var addResult = await _repository.AddDownloadAsync(downloadItem);
        if (addResult.IsFailure)
        {
            return Result<DownloadItem>.Failure(addResult.Error ?? "Failed to save download.");
        }

        _downloads[downloadItem.Id] = downloadItem;
        RaiseStateChanged(downloadItem, DownloadState.Queued, DownloadState.Queued);

        // Trigger queue processing
        ProcessQueue();

        return Result<DownloadItem>.Success(downloadItem);
    }

    public async Task<Result> PauseDownloadAsync(string id)
    {
        if (!_downloads.TryGetValue(id, out var item))
            return Result.Failure("Download not found.");

        if (item.State == DownloadState.Paused || item.State == DownloadState.Completed)
            return Result.Success();

        if (_activeCts.TryGetValue(id, out var cts))
        {
            cts.Cancel();
        }

        var oldState = item.State;
        item.State = DownloadState.Paused;
        item.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateDownloadStateAsync(id, DownloadState.Paused);
        RaiseStateChanged(item, oldState, DownloadState.Paused);

        ProcessQueue();
        return Result.Success();
    }

    public async Task<Result> ResumeDownloadAsync(string id)
    {
        if (!_downloads.TryGetValue(id, out var item))
            return Result.Failure("Download not found.");

        if (item.State == DownloadState.Downloading || item.State == DownloadState.Completed)
            return Result.Success();

        var oldState = item.State;
        item.State = DownloadState.Queued;
        item.ErrorMessage = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateDownloadStateAsync(id, DownloadState.Queued);
        RaiseStateChanged(item, oldState, DownloadState.Queued);

        ProcessQueue();
        return Result.Success();
    }

    public async Task<Result> CancelDownloadAsync(string id)
    {
        if (!_downloads.TryGetValue(id, out var item))
            return Result.Failure("Download not found.");

        if (_activeCts.TryGetValue(id, out var cts))
        {
            cts.Cancel();
        }

        var oldState = item.State;
        item.State = DownloadState.Cancelled;
        item.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateDownloadStateAsync(id, DownloadState.Cancelled);
        RaiseStateChanged(item, oldState, DownloadState.Cancelled);

        ProcessQueue();
        return Result.Success();
    }

    public async Task<Result> RetryDownloadAsync(string id)
    {
        if (!_downloads.TryGetValue(id, out var item))
            return Result.Failure("Download not found.");

        if (item.State == DownloadState.Downloading)
            return Result.Success();

        var oldState = item.State;
        item.State = DownloadState.Queued;
        item.ErrorMessage = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateDownloadStateAsync(id, DownloadState.Queued);
        RaiseStateChanged(item, oldState, DownloadState.Queued);

        ProcessQueue();
        return Result.Success();
    }

    public async Task<Result> RemoveDownloadAsync(string id, bool deleteFile = false)
    {
        if (_activeCts.TryGetValue(id, out var cts))
        {
            cts.Cancel();
        }

        if (_downloads.TryRemove(id, out var item))
        {
            await _repository.DeleteDownloadAsync(id);

            if (deleteFile)
            {
                try
                {
                    if (File.Exists(item.DestinationPath))
                    {
                        File.Delete(item.DestinationPath);
                    }
                    if (File.Exists(item.TempFilePath))
                    {
                        File.Delete(item.TempFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete files for download {Id}", id);
                }
            }

            ProcessQueue();
            return Result.Success();
        }

        return Result.Failure("Download not found.");
    }

    private void ProcessQueue()
    {
        lock (_queueLock)
        {
            var availableSlots = MaxConcurrentDownloads - _activeCts.Count;
            if (availableSlots <= 0) return;

            var queuedItems = _downloads.Values
                .Where(d => d.State == DownloadState.Queued)
                .OrderBy(d => d.CreatedAt)
                .Take(availableSlots)
                .ToList();

            foreach (var item in queuedItems)
            {
                var cts = new CancellationTokenSource();
                if (_activeCts.TryAdd(item.Id, cts))
                {
                    _ = Task.Run(() => ExecuteDownloadAsync(item, cts));
                }
            }
        }
    }

    private async Task ExecuteDownloadAsync(DownloadItem item, CancellationTokenSource cts)
    {
        var oldState = item.State;
        item.State = DownloadState.Downloading;
        item.ErrorMessage = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateDownloadStateAsync(item.Id, DownloadState.Downloading);
        RaiseStateChanged(item, oldState, DownloadState.Downloading);

        FileStream? fileStream = null;
        HttpResponseMessage? response = null;

        try
        {
            // Verify and prepare partial file
            long existingBytes = 0;
            if (File.Exists(item.TempFilePath))
            {
                existingBytes = new FileInfo(item.TempFilePath).Length;
            }

            item.BytesDownloaded = existingBytes;

            // Security: Execute request through manual redirect loop with SSRF validation
            response = await SendRequestWithRedirectValidationAsync(item.Url, existingBytes, item.ETag, cts.Token);

            var statusCode = response.StatusCode;
            bool isResume = false;

            if (statusCode == HttpStatusCode.PartialContent)
            {
                // Validate 206 Partial Content
                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange == null || !contentRange.HasRange || contentRange.From != existingBytes)
                {
                    Log.Warning("Content-Range mismatch for download {Id}. Restarting from 0.", item.Id);
                    existingBytes = 0;
                    if (File.Exists(item.TempFilePath)) File.Delete(item.TempFilePath);
                }
                else
                {
                    isResume = true;
                    if (contentRange.Length.HasValue)
                    {
                        item.TotalBytes = contentRange.Length.Value;
                    }
                }
            }
            else if (statusCode == HttpStatusCode.OK)
            {
                // Server does not support Range requests or resource was modified
                if (existingBytes > 0)
                {
                    Log.Information("Server returned 200 OK for partial request. Restarting download {Id} from 0.", item.Id);
                    existingBytes = 0;
                    if (File.Exists(item.TempFilePath)) File.Delete(item.TempFilePath);
                }

                if (response.Content.Headers.ContentLength.HasValue)
                {
                    item.TotalBytes = response.Content.Headers.ContentLength.Value;
                }
            }
            else if (statusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                Log.Warning("Server returned 416 Range Not Satisfiable for download {Id}. Restarting from 0.", item.Id);
                existingBytes = 0;
                if (File.Exists(item.TempFilePath)) File.Delete(item.TempFilePath);
                response.Dispose();

                // Re-request from 0
                response = await SendRequestWithRedirectValidationAsync(item.Url, 0, null, cts.Token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    item.TotalBytes = response.Content.Headers.ContentLength.Value;
                }
            }
            else
            {
                response.EnsureSuccessStatusCode();
            }

            // Capture validators for future resume verification
            item.ETag = response.Headers.ETag?.Tag ?? item.ETag;
            if (response.Content.Headers.LastModified.HasValue)
            {
                item.LastModified = response.Content.Headers.LastModified.Value.ToString("r");
            }

            // Disk space check
            CheckAvailableDiskSpace(item.DestinationPath, item.TotalBytes, existingBytes);

            // Open partial file stream
            var dir = Path.GetDirectoryName(item.TempFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            fileStream = new FileStream(
                item.TempFilePath,
                isResume ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                BufferSize,
                useAsync: true);

            // Stream response content to disk with memory bounding
            using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[BufferSize];
            int bytesRead;

            long sessionBytesDownloaded = 0;
            var stopwatch = Stopwatch.StartNew();
            var lastProgressReportTime = DateTime.UtcNow;

            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);

                existingBytes += bytesRead;
                sessionBytesDownloaded += bytesRead;
                item.BytesDownloaded = existingBytes;

                // Throttled progress reporting (every 250ms) to avoid overwhelming UI Dispatcher
                var now = DateTime.UtcNow;
                if ((now - lastProgressReportTime).TotalMilliseconds >= 250)
                {
                    lastProgressReportTime = now;
                    var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                    double bytesPerSec = elapsedSec > 0 ? sessionBytesDownloaded / elapsedSec : 0;
                    TimeSpan? eta = null;

                    if (item.TotalBytes.HasValue && item.TotalBytes.Value > existingBytes && bytesPerSec > 0)
                    {
                        var remainingBytes = item.TotalBytes.Value - existingBytes;
                        eta = TimeSpan.FromSeconds(remainingBytes / bytesPerSec);
                    }

                    _ = _repository.UpdateDownloadProgressAsync(
                        item.Id,
                        item.BytesDownloaded,
                        item.TotalBytes,
                        item.ETag,
                        item.LastModified);

                    ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(
                        item.Id,
                        item.BytesDownloaded,
                        item.TotalBytes,
                        bytesPerSec,
                        eta));
                }
            }

            await fileStream.FlushAsync(cts.Token);
            fileStream.Close();
            fileStream = null;

            // Verify completed size if total length was known
            if (item.TotalBytes.HasValue && item.TotalBytes.Value > 0 && item.BytesDownloaded < item.TotalBytes.Value)
            {
                throw new IOException($"Download stream terminated prematurely. Expected: {item.TotalBytes.Value} bytes, Got: {item.BytesDownloaded} bytes.");
            }

            // Atomic promotion of .part file to final destination
            if (File.Exists(item.DestinationPath))
            {
                // Avoid collision or overwrite
                item.DestinationPath = SecurityValidator.GetUniqueFilePath(
                    Path.GetDirectoryName(item.DestinationPath)!,
                    item.FileName);
            }

            File.Move(item.TempFilePath, item.DestinationPath, overwrite: false);

            if (!File.Exists(item.DestinationPath))
            {
                throw new IOException("Failed to promote temporary file to final destination.");
            }

            // Mark completed
            oldState = item.State;
            item.State = DownloadState.Completed;
            item.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateDownloadStateAsync(item.Id, DownloadState.Completed);

            RaiseProgress(item, 0, TimeSpan.Zero);
            RaiseStateChanged(item, oldState, DownloadState.Completed);
            Log.Information("Download {Id} ({FileName}) completed successfully.", item.Id, item.FileName);
        }
        catch (OperationCanceledException)
        {
            // Handled cleanly when paused or cancelled
            if (item.State == DownloadState.Downloading)
            {
                oldState = item.State;
                item.State = DownloadState.Paused;
                await _repository.UpdateDownloadStateAsync(item.Id, DownloadState.Paused);
                RaiseStateChanged(item, oldState, DownloadState.Paused);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Download {Id} failed: {Message}", item.Id, ex.Message);
            oldState = item.State;
            item.State = DownloadState.Failed;
            item.ErrorMessage = ex.Message;
            item.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateDownloadStateAsync(item.Id, DownloadState.Failed, ex.Message);
            RaiseStateChanged(item, oldState, DownloadState.Failed, ex.Message);
        }
        finally
        {
            fileStream?.Dispose();
            response?.Dispose();
            _activeCts.TryRemove(item.Id, out _);
            cts.Dispose();

            // Continue processing queue
            ProcessQueue();
        }
    }

    private async Task<HttpResponseMessage> SendRequestWithRedirectValidationAsync(
        string initialUrl,
        long rangeStart,
        string? etag,
        CancellationToken ct)
    {
        var currentUrl = initialUrl;
        int redirectCount = 0;

        while (redirectCount <= MaxRedirects)
        {
            // Validate URL structure & credentials
            var structResult = SecurityValidator.ValidateUrlStructure(currentUrl);
            if (structResult.IsFailure)
            {
                throw new InvalidOperationException($"Security validation failed: {structResult.Error}");
            }

            var uri = structResult.Value;

            // SSRF Guard: Validate DNS & IP
            var dnsResult = await SecurityValidator.ValidateDnsAndIpAsync(uri.Host, ct);
            if (dnsResult.IsFailure)
            {
                throw new InvalidOperationException($"SSRF prevention: {dnsResult.Error}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (rangeStart > 0)
            {
                request.Headers.Range = new RangeHeaderValue(rangeStart, null);
                if (!string.IsNullOrEmpty(etag))
                {
                    request.Headers.IfRange = new RangeConditionHeaderValue(etag);
                }
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            // Check for redirect status codes
            if (response.StatusCode is HttpStatusCode.MovedPermanently or
                HttpStatusCode.Found or
                HttpStatusCode.SeeOther or
                HttpStatusCode.TemporaryRedirect or
                (HttpStatusCode)308)
            {
                redirectCount++;
                if (redirectCount > MaxRedirects)
                {
                    response.Dispose();
                    throw new InvalidOperationException($"Too many redirects (exceeded limit of {MaxRedirects}).");
                }

                var location = response.Headers.Location;
                response.Dispose();

                if (location == null)
                {
                    throw new InvalidOperationException("Redirect response missing 'Location' header.");
                }

                // Resolve relative redirect URI against base
                var nextUri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                currentUrl = nextUri.AbsoluteUri;
                Log.Information("Following redirect #{Count} to {NextUrl}", redirectCount, currentUrl);
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("Max redirect limit exceeded.");
    }

    private static void CheckAvailableDiskSpace(string destinationPath, long? totalBytes, long existingBytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(destinationPath));
            if (string.IsNullOrEmpty(root)) return;

            var drive = new DriveInfo(root);
            long bytesNeeded = totalBytes.HasValue
                ? Math.Max(0, totalBytes.Value - existingBytes)
                : 20971520L; // 20 MB default buffer when total is unknown

            if (drive.AvailableFreeSpace < bytesNeeded)
            {
                throw new IOException($"Insufficient disk space on drive {drive.Name}. Required: {bytesNeeded / 1048576} MB, Available: {drive.AvailableFreeSpace / 1048576} MB.");
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not determine drive free space for {Path}", destinationPath);
        }
    }

    private void RaiseProgress(DownloadItem item, double speed, TimeSpan? eta)
    {
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(
            item.Id,
            item.BytesDownloaded,
            item.TotalBytes,
            speed,
            eta));
    }

    private void RaiseStateChanged(DownloadItem item, DownloadState previous, DownloadState current, string? error = null)
    {
        StateChanged?.Invoke(this, new DownloadStateChangedEventArgs(item.Id, previous, current, error));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        foreach (var cts in _activeCts.Values)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch { }
        }

        _activeCts.Clear();
        _concurrencySemaphore.Dispose();
    }
}
