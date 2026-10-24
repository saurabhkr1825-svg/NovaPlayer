using System.Globalization;
using Microsoft.Data.Sqlite;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.Infrastructure.Persistence;

public class SqliteLibraryRepository : ILibraryRepository
{
    private readonly DatabaseContext _dbContext;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteLibraryRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await _dbContext.InitializeAndMigrateAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<MediaItem>> AddOrUpdateMediaItemAsync(MediaItem item)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO media_items (
                    id, file_path, title, duration_ms, is_network_stream,
                    container, video_codec, audio_codec, width, height,
                    file_size_bytes, date_added, last_played_at, last_position_ms,
                    play_count, is_favorite
                ) VALUES (
                    @id, @file_path, @title, @duration_ms, @is_network_stream,
                    @container, @video_codec, @audio_codec, @width, @height,
                    @file_size_bytes, @date_added, @last_played_at, @last_position_ms,
                    @play_count, @is_favorite
                )
                ON CONFLICT(file_path) DO UPDATE SET
                    title = excluded.title,
                    duration_ms = CASE WHEN excluded.duration_ms > 0 THEN excluded.duration_ms ELSE media_items.duration_ms END,
                    container = COALESCE(excluded.container, media_items.container),
                    video_codec = COALESCE(excluded.video_codec, media_items.video_codec),
                    audio_codec = COALESCE(excluded.audio_codec, media_items.audio_codec),
                    width = COALESCE(excluded.width, media_items.width),
                    height = COALESCE(excluded.height, media_items.height),
                    file_size_bytes = CASE WHEN excluded.file_size_bytes > 0 THEN excluded.file_size_bytes ELSE media_items.file_size_bytes END;
                
                SELECT id FROM media_items WHERE file_path = @file_path;";

            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@file_path", item.FilePathOrUrl);
            cmd.Parameters.AddWithValue("@title", item.Title);
            cmd.Parameters.AddWithValue("@duration_ms", item.DurationMs);
            cmd.Parameters.AddWithValue("@is_network_stream", item.IsNetworkStream ? 1 : 0);
            cmd.Parameters.AddWithValue("@container", (object?)item.Container ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@video_codec", (object?)item.VideoCodec ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@audio_codec", (object?)item.AudioCodec ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@width", (object?)item.Width ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@height", (object?)item.Height ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@file_size_bytes", item.FileSizeBytes);
            cmd.Parameters.AddWithValue("@date_added", item.DateAdded.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@last_played_at", item.LastPlayedAt.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@last_position_ms", item.LastPositionMs);
            cmd.Parameters.AddWithValue("@play_count", item.PlayCount);
            cmd.Parameters.AddWithValue("@is_favorite", item.IsFavorite ? 1 : 0);

            var finalId = await cmd.ExecuteScalarAsync();
            if (finalId != null)
            {
                item.Id = finalId.ToString()!;
            }

            return Result<MediaItem>.Success(item);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add or update media item {Path}", item.FilePathOrUrl);
            return Result<MediaItem>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<int>> AddMediaItemsBatchAsync(IEnumerable<MediaItem> items)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var transaction = conn.BeginTransaction();

            int count = 0;
            foreach (var item in items)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO media_items (
                        id, file_path, title, duration_ms, is_network_stream,
                        container, video_codec, audio_codec, width, height,
                        file_size_bytes, date_added, last_played_at, last_position_ms,
                        play_count, is_favorite
                    ) VALUES (
                        @id, @file_path, @title, @duration_ms, @is_network_stream,
                        @container, @video_codec, @audio_codec, @width, @height,
                        @file_size_bytes, @date_added, @last_played_at, @last_position_ms,
                        @play_count, @is_favorite
                    )
                    ON CONFLICT(file_path) DO NOTHING;";

                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@file_path", item.FilePathOrUrl);
                cmd.Parameters.AddWithValue("@title", item.Title);
                cmd.Parameters.AddWithValue("@duration_ms", item.DurationMs);
                cmd.Parameters.AddWithValue("@is_network_stream", item.IsNetworkStream ? 1 : 0);
                cmd.Parameters.AddWithValue("@container", (object?)item.Container ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@video_codec", (object?)item.VideoCodec ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@audio_codec", (object?)item.AudioCodec ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@width", (object?)item.Width ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@height", (object?)item.Height ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@file_size_bytes", item.FileSizeBytes);
                cmd.Parameters.AddWithValue("@date_added", item.DateAdded.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@last_played_at", item.LastPlayedAt.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@last_position_ms", item.LastPositionMs);
                cmd.Parameters.AddWithValue("@play_count", item.PlayCount);
                cmd.Parameters.AddWithValue("@is_favorite", item.IsFavorite ? 1 : 0);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0) count++;
            }

            await transaction.CommitAsync();
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to insert media items batch.");
            return Result<int>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<IReadOnlyList<MediaItem>>> GetAllMediaItemsAsync(string? searchQuery = null)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                cmd.CommandText = "SELECT * FROM media_items ORDER BY date_added DESC;";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT * FROM media_items
                    WHERE title LIKE @query OR file_path LIKE @query OR container LIKE @query
                    ORDER BY date_added DESC;";
                cmd.Parameters.AddWithValue("@query", $"%{searchQuery.Trim()}%");
            }

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<MediaItem>();
            while (await reader.ReadAsync())
            {
                list.Add(ReadMediaItem(reader));
            }

            return Result<IReadOnlyList<MediaItem>>.Success(list.AsReadOnly());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve media items.");
            return Result<IReadOnlyList<MediaItem>>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<MediaItem?>> GetMediaItemByPathAsync(string path)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM media_items WHERE file_path = @path LIMIT 1;";
            cmd.Parameters.AddWithValue("@path", path);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Result<MediaItem?>.Success(ReadMediaItem(reader));
            }

            return Result<MediaItem?>.Success(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve media item by path {Path}", path);
            return Result<MediaItem?>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<MediaItem?>> GetMediaItemByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM media_items WHERE id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Result<MediaItem?>.Success(ReadMediaItem(reader));
            }

            return Result<MediaItem?>.Success(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve media item by id {Id}", id);
            return Result<MediaItem?>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> DeleteMediaItemAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM media_items WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete media item {Id}", id);
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<IReadOnlyList<Playlist>>> GetAllPlaylistsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM playlists ORDER BY name ASC;";

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Playlist>();
            while (await reader.ReadAsync())
            {
                list.Add(new Playlist
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")), CultureInfo.InvariantCulture),
                    UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at")), CultureInfo.InvariantCulture)
                });
            }

            return Result<IReadOnlyList<Playlist>>.Success(list.AsReadOnly());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get playlists.");
            return Result<IReadOnlyList<Playlist>>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<Playlist>> CreatePlaylistAsync(string name)
    {
        await _lock.WaitAsync();
        try
        {
            var playlist = new Playlist(name.Trim());
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO playlists (id, name, created_at, updated_at)
                VALUES (@id, @name, @created_at, @updated_at);";

            cmd.Parameters.AddWithValue("@id", playlist.Id);
            cmd.Parameters.AddWithValue("@name", playlist.Name);
            cmd.Parameters.AddWithValue("@created_at", playlist.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@updated_at", playlist.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
            return Result<Playlist>.Success(playlist);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create playlist {Name}", name);
            return Result<Playlist>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> RenamePlaylistAsync(string playlistId, string newName)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE playlists
                SET name = @name, updated_at = @updated_at
                WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", playlistId);
            cmd.Parameters.AddWithValue("@name", newName.Trim());
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to rename playlist {Id}", playlistId);
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> DeletePlaylistAsync(string playlistId)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM playlists WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", playlistId);
            await cmd.ExecuteNonQueryAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete playlist {Id}", playlistId);
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<PlaylistItem>> AddItemToPlaylistAsync(string playlistId, string mediaItemId)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();

            // 1. Get next order index
            int nextOrder = 0;
            using (var orderCmd = conn.CreateCommand())
            {
                orderCmd.CommandText = "SELECT COALESCE(MAX(order_index) + 1, 0) FROM playlist_items WHERE playlist_id = @pid;";
                orderCmd.Parameters.AddWithValue("@pid", playlistId);
                var val = await orderCmd.ExecuteScalarAsync();
                if (val != null && val != DBNull.Value)
                {
                    nextOrder = Convert.ToInt32(val);
                }
            }

            // 2. Insert item
            var item = new PlaylistItem(playlistId, mediaItemId, nextOrder);
            using (var insertCmd = conn.CreateCommand())
            {
                insertCmd.CommandText = @"
                    INSERT INTO playlist_items (id, playlist_id, media_item_id, order_index)
                    VALUES (@id, @playlist_id, @media_item_id, @order_index);";
                insertCmd.Parameters.AddWithValue("@id", item.Id);
                insertCmd.Parameters.AddWithValue("@playlist_id", item.PlaylistId);
                insertCmd.Parameters.AddWithValue("@media_item_id", item.MediaItemId);
                insertCmd.Parameters.AddWithValue("@order_index", item.OrderIndex);

                await insertCmd.ExecuteNonQueryAsync();
            }

            return Result<PlaylistItem>.Success(item);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add item {MediaId} to playlist {PlaylistId}", mediaItemId, playlistId);
            return Result<PlaylistItem>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> RemoveItemFromPlaylistAsync(string playlistId, string playlistItemId)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM playlist_items WHERE id = @id AND playlist_id = @pid;";
            cmd.Parameters.AddWithValue("@id", playlistItemId);
            cmd.Parameters.AddWithValue("@pid", playlistId);
            await cmd.ExecuteNonQueryAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to remove item from playlist.");
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> ReorderPlaylistItemsAsync(string playlistId, IReadOnlyList<string> orderedPlaylistItemIds)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var transaction = conn.BeginTransaction();

            for (int i = 0; i < orderedPlaylistItemIds.Count; i++)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE playlist_items
                    SET order_index = @order_index
                    WHERE id = @id AND playlist_id = @pid;";

                cmd.Parameters.AddWithValue("@order_index", i);
                cmd.Parameters.AddWithValue("@id", orderedPlaylistItemIds[i]);
                cmd.Parameters.AddWithValue("@pid", playlistId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reorder playlist items.");
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<IReadOnlyList<PlaylistItem>>> GetPlaylistItemsAsync(string playlistId)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT pi.id AS pi_id, pi.playlist_id, pi.media_item_id, pi.order_index,
                       m.id, m.file_path, m.title, m.duration_ms, m.is_network_stream,
                       m.container, m.video_codec, m.audio_codec, m.width, m.height,
                       m.file_size_bytes, m.date_added, m.last_played_at, m.last_position_ms,
                       m.play_count, m.is_favorite
                FROM playlist_items pi
                JOIN media_items m ON pi.media_item_id = m.id
                WHERE pi.playlist_id = @pid
                ORDER BY pi.order_index ASC;";

            cmd.Parameters.AddWithValue("@pid", playlistId);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<PlaylistItem>();
            while (await reader.ReadAsync())
            {
                var item = new PlaylistItem
                {
                    Id = reader.GetString(reader.GetOrdinal("pi_id")),
                    PlaylistId = reader.GetString(reader.GetOrdinal("playlist_id")),
                    MediaItemId = reader.GetString(reader.GetOrdinal("media_item_id")),
                    OrderIndex = reader.GetInt32(reader.GetOrdinal("order_index")),
                    MediaItem = ReadMediaItem(reader)
                };
                list.Add(item);
            }

            return Result<IReadOnlyList<PlaylistItem>>.Success(list.AsReadOnly());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get playlist items for {PlaylistId}", playlistId);
            return Result<IReadOnlyList<PlaylistItem>>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> RecordPlaybackHistoryAsync(string filePathOrUrl, string title, long positionMs, long durationMs)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            bool isCompleted = durationMs > 0 && positionMs >= (durationMs * 0.95);

            cmd.CommandText = @"
                INSERT INTO playback_history (
                    id, title, file_path, position_ms, duration_ms, is_completed, played_at
                ) VALUES (
                    @id, @title, @file_path, @position_ms, @duration_ms, @is_completed, @played_at
                );

                UPDATE media_items
                SET last_position_ms = @position_ms,
                    last_played_at = @played_at,
                    play_count = play_count + 1
                WHERE file_path = @file_path;";

            cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@file_path", filePathOrUrl);
            cmd.Parameters.AddWithValue("@position_ms", positionMs);
            cmd.Parameters.AddWithValue("@duration_ms", durationMs);
            cmd.Parameters.AddWithValue("@is_completed", isCompleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@played_at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to record playback history for {Path}", filePathOrUrl);
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<PlaybackHistoryEntry?>> GetResumePointAsync(string filePathOrUrl)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT * FROM playback_history
                WHERE file_path = @path
                ORDER BY played_at DESC
                LIMIT 1;";
            cmd.Parameters.AddWithValue("@path", filePathOrUrl);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var entry = new PlaybackHistoryEntry
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    FilePathOrUrl = reader.GetString(reader.GetOrdinal("file_path")),
                    PositionMs = reader.GetInt64(reader.GetOrdinal("position_ms")),
                    DurationMs = reader.GetInt64(reader.GetOrdinal("duration_ms")),
                    IsCompleted = reader.GetInt32(reader.GetOrdinal("is_completed")) == 1,
                    PlayedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("played_at")), CultureInfo.InvariantCulture)
                };
                return Result<PlaybackHistoryEntry?>.Success(entry);
            }

            return Result<PlaybackHistoryEntry?>.Success(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve resume point for {Path}", filePathOrUrl);
            return Result<PlaybackHistoryEntry?>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<IReadOnlyList<PlaybackHistoryEntry>>> GetRecentHistoryAsync(int limit = 50)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT * FROM playback_history
                ORDER BY played_at DESC
                LIMIT @limit;";
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<PlaybackHistoryEntry>();
            while (await reader.ReadAsync())
            {
                list.Add(new PlaybackHistoryEntry
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    FilePathOrUrl = reader.GetString(reader.GetOrdinal("file_path")),
                    PositionMs = reader.GetInt64(reader.GetOrdinal("position_ms")),
                    DurationMs = reader.GetInt64(reader.GetOrdinal("duration_ms")),
                    IsCompleted = reader.GetInt32(reader.GetOrdinal("is_completed")) == 1,
                    PlayedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("played_at")), CultureInfo.InvariantCulture)
                });
            }

            return Result<IReadOnlyList<PlaybackHistoryEntry>>.Success(list.AsReadOnly());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve recent playback history.");
            return Result<IReadOnlyList<PlaybackHistoryEntry>>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> ClearHistoryAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM playback_history;";
            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clear history.");
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> DeleteHistoryEntryAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM playback_history WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete history entry {Id}", id);
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> SetSettingAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO app_settings (key, value, updated_at)
                VALUES (@key, @value, @updated_at)
                ON CONFLICT(key) DO UPDATE SET
                    value = excluded.value,
                    updated_at = excluded.updated_at;";

            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set setting {Key}", key);
            return Result.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<string?>> GetSettingAsync(string key, string? defaultValue = null)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = await _dbContext.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key LIMIT 1;";
            cmd.Parameters.AddWithValue("@key", key);

            var val = await cmd.ExecuteScalarAsync();
            if (val != null && val != DBNull.Value)
            {
                return Result<string?>.Success(val.ToString());
            }

            return Result<string?>.Success(defaultValue);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get setting {Key}", key);
            return Result<string?>.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static MediaItem ReadMediaItem(SqliteDataReader reader)
    {
        return new MediaItem
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            FilePathOrUrl = reader.GetString(reader.GetOrdinal("file_path")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            DurationMs = reader.GetInt64(reader.GetOrdinal("duration_ms")),
            IsNetworkStream = reader.GetInt32(reader.GetOrdinal("is_network_stream")) == 1,
            Container = reader.IsDBNull(reader.GetOrdinal("container")) ? null : reader.GetString(reader.GetOrdinal("container")),
            VideoCodec = reader.IsDBNull(reader.GetOrdinal("video_codec")) ? null : reader.GetString(reader.GetOrdinal("video_codec")),
            AudioCodec = reader.IsDBNull(reader.GetOrdinal("audio_codec")) ? null : reader.GetString(reader.GetOrdinal("audio_codec")),
            Width = reader.IsDBNull(reader.GetOrdinal("width")) ? null : reader.GetInt32(reader.GetOrdinal("width")),
            Height = reader.IsDBNull(reader.GetOrdinal("height")) ? null : reader.GetInt32(reader.GetOrdinal("height")),
            FileSizeBytes = reader.GetInt64(reader.GetOrdinal("file_size_bytes")),
            DateAdded = DateTime.Parse(reader.GetString(reader.GetOrdinal("date_added")), CultureInfo.InvariantCulture),
            LastPlayedAt = reader.IsDBNull(reader.GetOrdinal("last_played_at")) ? DateTime.UtcNow : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_played_at")), CultureInfo.InvariantCulture),
            LastPositionMs = reader.GetInt64(reader.GetOrdinal("last_position_ms")),
            PlayCount = reader.GetInt32(reader.GetOrdinal("play_count")),
            IsFavorite = reader.GetInt32(reader.GetOrdinal("is_favorite")) == 1
        };
    }
}
