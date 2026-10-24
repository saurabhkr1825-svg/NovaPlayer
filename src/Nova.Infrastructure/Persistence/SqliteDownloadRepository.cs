using System.Globalization;
using Microsoft.Data.Sqlite;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.Infrastructure.Persistence;

public class SqliteDownloadRepository : IDownloadRepository
{
    private readonly DatabaseContext _dbContext;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteDownloadRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<DownloadItem>>> GetAllDownloadsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT id, url, destination_path, temp_file_path, filename, state,
                       bytes_downloaded, total_bytes, etag, last_modified,
                       created_at, updated_at, error_message
                FROM downloads
                ORDER BY created_at DESC;";

            using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<DownloadItem>();

            while (await reader.ReadAsync())
            {
                items.Add(MapDownloadItem(reader));
            }

            return Result<IReadOnlyList<DownloadItem>>.Success(items.AsReadOnly());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load downloads from database.");
            return Result<IReadOnlyList<DownloadItem>>.Failure($"Failed to load downloads: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<DownloadItem?>> GetDownloadAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result<DownloadItem?>.Failure("Invalid download identifier.");

        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT id, url, destination_path, temp_file_path, filename, state,
                       bytes_downloaded, total_bytes, etag, last_modified,
                       created_at, updated_at, error_message
                FROM downloads
                WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Result<DownloadItem?>.Success(MapDownloadItem(reader));
            }

            return Result<DownloadItem?>.Success(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch download with id {Id}.", id);
            return Result<DownloadItem?>.Failure($"Failed to fetch download: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> AddDownloadAsync(DownloadItem item)
    {
        if (item == null) return Result.Failure("Download item cannot be null.");

        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO downloads (
                    id, url, destination_path, temp_file_path, filename, state,
                    bytes_downloaded, total_bytes, etag, last_modified,
                    created_at, updated_at, error_message
                ) VALUES (
                    @id, @url, @dest, @temp, @filename, @state,
                    @bytes_downloaded, @total_bytes, @etag, @last_modified,
                    @created_at, @updated_at, @error_message
                )
                ON CONFLICT(id) DO UPDATE SET
                    state = excluded.state,
                    bytes_downloaded = excluded.bytes_downloaded,
                    total_bytes = excluded.total_bytes,
                    etag = excluded.etag,
                    last_modified = excluded.last_modified,
                    updated_at = excluded.updated_at,
                    error_message = excluded.error_message;";

            BindParameters(cmd, item);

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add/update download with id {Id}.", item.Id);
            return Result.Failure($"Failed to save download: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> UpdateDownloadProgressAsync(
        string id,
        long bytesDownloaded,
        long? totalBytes,
        string? etag,
        string? lastModified)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE downloads
                SET bytes_downloaded = @bytes_downloaded,
                    total_bytes = COALESCE(@total_bytes, total_bytes),
                    etag = COALESCE(@etag, etag),
                    last_modified = COALESCE(@last_modified, last_modified),
                    updated_at = @updated_at
                WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@bytes_downloaded", bytesDownloaded);
            cmd.Parameters.AddWithValue("@total_bytes", (object?)totalBytes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@etag", (object?)etag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@last_modified", (object?)lastModified ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update download progress for id {Id}.", id);
            return Result.Failure($"Failed to update progress: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> UpdateDownloadStateAsync(string id, DownloadState state, string? errorMessage = null)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE downloads
                SET state = @state,
                    error_message = @error_message,
                    updated_at = @updated_at
                WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@state", (int)state);
            cmd.Parameters.AddWithValue("@error_message", (object?)errorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update download state for id {Id}.", id);
            return Result.Failure($"Failed to update state: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> DeleteDownloadAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "DELETE FROM downloads WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete download with id {Id}.", id);
            return Result.Failure($"Failed to delete download: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    private static DownloadItem MapDownloadItem(SqliteDataReader reader)
    {
        var item = new DownloadItem
        {
            Id = reader.GetString(0),
            Url = reader.GetString(1),
            DestinationPath = reader.GetString(2),
            TempFilePath = reader.GetString(3),
            FileName = reader.GetString(4),
            State = (DownloadState)reader.GetInt32(5),
            BytesDownloaded = reader.GetInt64(6),
            TotalBytes = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            ETag = reader.IsDBNull(8) ? null : reader.GetString(8),
            LastModified = reader.IsDBNull(9) ? null : reader.GetString(9),
            CreatedAt = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ErrorMessage = reader.IsDBNull(12) ? null : reader.GetString(12)
        };

        return item;
    }

    private static void BindParameters(SqliteCommand cmd, DownloadItem item)
    {
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@url", item.Url);
        cmd.Parameters.AddWithValue("@dest", item.DestinationPath);
        cmd.Parameters.AddWithValue("@temp", item.TempFilePath);
        cmd.Parameters.AddWithValue("@filename", item.FileName);
        cmd.Parameters.AddWithValue("@state", (int)item.State);
        cmd.Parameters.AddWithValue("@bytes_downloaded", item.BytesDownloaded);
        cmd.Parameters.AddWithValue("@total_bytes", (object?)item.TotalBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@etag", (object?)item.ETag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@last_modified", (object?)item.LastModified ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", item.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@updated_at", item.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@error_message", (object?)item.ErrorMessage ?? DBNull.Value);
    }
}
