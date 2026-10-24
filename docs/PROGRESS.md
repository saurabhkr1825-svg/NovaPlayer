# NovaPlayer — Project Progress & Tracking

## 1. Project Phase Status Overview

| Phase | Description | Status | Completion Target |
| :--- | :--- | :--- | :--- |
| **Phase 0** | Architecture, Planning & Environment Setup | **COMPLETED** | Oct 2026 |
| **Phase 1** | Foundation, Solution Setup & Baseline Playback (MVP) | **COMPLETED** | Oct 2026 |
| **Phase 2** | Advanced Playback, Web-DL Codecs, Audio/Subtitles | **COMPLETED** | Oct 2026 |
| **Phase 3** | **Media Library, SQLite Persistence & Resume Playback** | **COMPLETED** | **Immediate** |
| **Phase 4** | Network Playback, HLS Streaming, Equalizer & Theming | *Not Started (Next Authorized)* | Milestone 4 |
| **Phase 5** | Resumable Downloader & Security Hardening | *Not Started* | Milestone 5 |
| **Phase 6** | Packaging (WiX Installer), Testing & Release Gate | *Not Started* | Milestone 6 |

---

## 2. Environment Verification & Prerequisite Resolution

* **.NET SDK Resolution**: Installed Microsoft .NET 8.0 SDK (`v8.0.426`, x64). `DOTNET_ROOT` configured and active.
* **Target Framework**: `net8.0-windows` for UI and native interop; `net8.0` for domain core.
* **FFmpeg Tooling**: Verified FFmpeg version N-123778 with hardware acceleration and encoders (`libx264`, `libx265`, `libsvtav1`, `eac3`, `libopus`).
* **Database Driver**: `Microsoft.Data.Sqlite` v8.0.10 configured under Central Package Management (`Directory.Packages.props`).

---

## 3. Phase 3 Implementation Summary

### 3.1 Task 3.1 — SQLite Persistence Architecture
* **Database Engine**: `Microsoft.Data.Sqlite` in WAL mode (`journal_mode = WAL`, `synchronous = NORMAL`, `foreign_keys = ON`).
* **Migration Mechanism**: Versioned table `schema_migrations` tracking sequential schema versions with transactional migrations.
* **Entities**:
  * `media_items`: Extended metadata (ID, path, title, container, codecs, dimensions, duration, size, dates, play count, favorite).
  * `playlists`: Playlist collection with names and timestamps.
  * `playlist_items`: Explicit order index and foreign key cascade to media items.
  * `playback_history`: History entries with played position, duration, and completion flag.
  * `app_settings`: Key-value configuration store for player preferences.
* **Data Access Layer**: `SqliteLibraryRepository` with parameterized SQL, connection pooling, and UI-thread isolation via `Task.Run` and `SemaphoreSlim`.

### 3.2 Task 3.2 — Background Media Scanner
* **Scanner Component**: `MediaScanner` in `Nova.Infrastructure.Scanner`.
* **Safe Traversal**: Recursive folder discovery with symbolic link and reparse point cycle protection; skips hidden/system folders.
* **Non-Blocking Operation**: Throttled UI progress reporting (max 10 Hz) using `IProgress<ScanProgress>` and cooperative cancellation via `CancellationToken`.
* **Atomic Batching**: Inserts and updates items in 100-record batches inside transactions; preserves partial scans on cancellation without deleting existing entries.

### 3.3 Task 3.3 — Media Library Interface
* **Navigation Switcher**: Smooth tab navigation between *Now Playing*, *Media Library*, *Playlists*, and *History*.
* **Airspace Isolation**: Automatically hides Win32 HWND `VideoView` during library views to ensure 100% WPF composition and GPU virtualization.
* **Features**:
  * Search bar with live debounced filtering.
  * Filter categories: All, Video, Audio, Favorites.
  * Virtualized `ListView` with metadata columns (Title, Duration, Format, Resolution, Codec, Size, Plays, Location).
  * Context actions: Play, Toggle Favorite, Open File Location, Remove from Library.
  * Folder scan dialog with real-time progress bar, status text, and cancel action.

### 3.4 Task 3.4 — Playlist Management
* **M3U / M3U8 Integration**: `M3uPlaylistService` supporting standard and Extended M3U formats (`#EXTM3U`, `#EXTINF`), UTF-8 encoding (`.m3u8`), and relative path resolution.
* **UI Controls**:
  * Playlist list with New, Delete, Rename, Import M3U, Export M3U.
  * Track list with reorder up (`▲`), reorder down (`▼`), remove track (`✕`).
  * In-memory Fisher-Yates shuffle that updates stored order.
  * Play All & Play Selected Track.

### 3.5 Task 3.5 — Playback History & Smart Resume
* **History Logging**: Non-blocking throttled periodic position saving (every 5 seconds) and on state changes (pause, stop, ended).
* **Smart Resume**:
  * Detection of partially played content: threshold `> 10 seconds` and `< 95% complete`.
  * Non-intrusive floating overlay resume prompt: *"Resume from mm:ss?"* with `[▶ Resume]` and `[✕ Dismiss]` buttons.
  * Resume seeks accurately to last saved position.
* **Sequential Playback & Repeat Modes**:
  * Playback queue management with Next Track (`N` / `⏭`) and Previous Track (`P` / `⏮`).
  * Repeat mode cycling (`R` / `🔁`): `None`, `RepeatAll`, `RepeatOne`.
  * Auto-advance on track completion based on active repeat mode.

---

## 4. Automated Test Suite Metrics

Executed via `dotnet test NovaPlayer.sln --no-restore -m:1 /nodeReuse:false`:

| Test Project | Target | Tests Passed | Failed | Skipped | Duration |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **`Nova.Core.Tests`** | net8.0 | 33 | 0 | 0 | 25 ms |
| **`Nova.Infrastructure.Tests`** | net8.0-windows | 18 | 0 | 0 | 3.0 s |
| **`Nova.App.Tests`** | net8.0-windows | 31 | 0 | 0 | 33 ms |
| **Total** | | **82** | **0** | **0** | **~3.2 s** |

### Test Suites Added in Phase 3:
1. `SqliteLibraryRepositoryTests`: WAL mode, schema migrations, parameterized queries, batch insertion, playlist ordering, cascade deletes, history recording, settings.
2. `MediaScannerTests`: Candidate file discovery, recursive traversal, cancellation token honoring, junction skip.
3. `M3uPlaylistServiceTests`: M3U / M3U8 import, export with relative paths, Fisher-Yates shuffle.
4. `LibraryViewModelTests`: Load all, search filtering, category filtering, playlist CRUD, play events, history clearing.
5. `PlayerViewModelPhase3Tests`: Resume prompt display at >10s threshold, resume seeking, dismiss prompt, queue sequencing, repeat mode transitions.

---

## 5. Phase 3 Completion Gate Verification

- [x] Versioned SQLite schema and migrations implemented with `Microsoft.Data.Sqlite`.
- [x] WAL journal mode and foreign key enforcement enabled.
- [x] Background scanner implemented with cancellation tokens, progress reporting, and junction protection.
- [x] Virtualized Media Library UI integrated with search, filters, and context menus.
- [x] Playlist management with M3U / M3U8 import, export, reordering, and shuffle.
- [x] Playback history logging, smart resume prompt (>10s threshold), and queue playback with repeat modes.
- [x] 100% unit test pass rate across all 82 tests (0 failures).
- [x] Zero build warnings and zero compilation errors.

---

## 6. Current Authorization Status & Next Authorized Task

* **Current Status**: Phase 3 is **COMPLETE**. All completion gate criteria are met.
* **Next Action**: Awaiting user authorization to proceed to **Phase 4 (Network Playback, HLS / DASH Streaming, Audio Equalizer & Theming)**.
