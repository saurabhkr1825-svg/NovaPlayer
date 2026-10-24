using Microsoft.Data.Sqlite;
using Nova.Core.Common;
using Serilog;

namespace Nova.Infrastructure.Persistence;

public class DatabaseContext
{
    private readonly string _connectionString;
    private readonly bool _isInMemory;

    public DatabaseContext(string? databasePath = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbDirectory = Path.Combine(localAppData, "NovaPlayer");
            if (!Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }
            databasePath = Path.Combine(dbDirectory, "novaplayer.db");
            _connectionString = $"Data Source={databasePath}";
            _isInMemory = false;
        }
        else if (databasePath.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            _connectionString = "Data Source=:memory:";
            _isInMemory = true;
        }
        else
        {
            _connectionString = $"Data Source={databasePath}";
            _isInMemory = false;
        }
    }

    public async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Configure PRAGMAs
        using var cmd = connection.CreateCommand();
        if (!_isInMemory)
        {
            cmd.CommandText = @"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;";
        }
        else
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
        }
        await cmd.ExecuteNonQueryAsync();

        return connection;
    }

    public async Task<Result> InitializeAndMigrateAsync()
    {
        try
        {
            using var connection = await CreateConnectionAsync();

            // 1. Create Migration Tracking Table
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        version INTEGER PRIMARY KEY,
                        applied_at TEXT NOT NULL
                    );";
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Query Current Version
            int currentVersion = 0;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    currentVersion = Convert.ToInt32(result);
                }
            }

            // 3. Apply Migrations Sequentially
            if (currentVersion < 1)
            {
                await ApplyMigrationV1Async(connection);
            }

            Log.Information("Database initialization and migrations completed successfully. Current version: {Version}", Math.Max(currentVersion, 1));
            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize or migrate database.");
            return Result.Failure($"Database initialization failure: {ex.Message}");
        }
    }

    private static async Task ApplyMigrationV1Async(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                -- Media Items Table
                CREATE TABLE IF NOT EXISTS media_items (
                    id TEXT PRIMARY KEY,
                    file_path TEXT NOT NULL UNIQUE,
                    title TEXT NOT NULL,
                    duration_ms INTEGER NOT NULL DEFAULT 0,
                    is_network_stream INTEGER NOT NULL DEFAULT 0,
                    container TEXT,
                    video_codec TEXT,
                    audio_codec TEXT,
                    width INTEGER,
                    height INTEGER,
                    file_size_bytes INTEGER NOT NULL DEFAULT 0,
                    date_added TEXT NOT NULL,
                    last_played_at TEXT,
                    last_position_ms INTEGER NOT NULL DEFAULT 0,
                    play_count INTEGER NOT NULL DEFAULT 0,
                    is_favorite INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_media_items_path ON media_items(file_path);
                CREATE INDEX IF NOT EXISTS idx_media_items_title ON media_items(title);
                CREATE INDEX IF NOT EXISTS idx_media_items_date ON media_items(date_added DESC);

                -- Playlists Table
                CREATE TABLE IF NOT EXISTS playlists (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                -- Playlist Items Table (Ordered)
                CREATE TABLE IF NOT EXISTS playlist_items (
                    id TEXT PRIMARY KEY,
                    playlist_id TEXT NOT NULL,
                    media_item_id TEXT NOT NULL,
                    order_index INTEGER NOT NULL,
                    FOREIGN KEY (playlist_id) REFERENCES playlists(id) ON DELETE CASCADE,
                    FOREIGN KEY (media_item_id) REFERENCES media_items(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_playlist_items_order ON playlist_items(playlist_id, order_index ASC);

                -- Playback History Table
                CREATE TABLE IF NOT EXISTS playback_history (
                    id TEXT PRIMARY KEY,
                    media_item_id TEXT,
                    title TEXT NOT NULL,
                    file_path TEXT NOT NULL,
                    position_ms INTEGER NOT NULL,
                    duration_ms INTEGER NOT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    played_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_playback_history_path ON playback_history(file_path, played_at DESC);

                -- Application Settings (Key-Value)
                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                -- Record Migration V1
                INSERT INTO schema_migrations (version, applied_at)
                VALUES (1, datetime('now'));";

            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            Log.Information("Applied schema migration V1.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
