# NovaPlayer — Project Structure & Solution Organization

## 1. Solution Architecture Overview

The solution adheres to clean, pragmatic engineering principles. We avoid excessive enterprise boilerplate (e.g., separate projects for every micro-concern) while enforcing clean architectural separation between domain logic, infrastructure implementations, and WPF presentation.

```
Nova/
├── .editorconfig                       # Code style and analyzer rules
├── .gitignore                          # Visual Studio and .NET gitignore
├── Directory.Build.props               # Centralized C# language version & compiler flags
├── Directory.Packages.props            # Central Package Management (CPM)
├── NovaPlayer.sln                      # Visual Studio Solution File
│
├── docs/                               # Architectural and planning documentation
│   ├── ARCHITECTURE.md
│   ├── DECISIONS.md
│   ├── FEATURE_MATRIX.md
│   ├── IMPLEMENTATION_PLAN.md
│   ├── PRODUCT_SPEC.md
│   ├── PROGRESS.md
│   ├── PROJECT_STRUCTURE.md
│   └── TEST_PLAN.md
│
├── src/
│   ├── Nova.Core/                      # Domain models, service contracts, result types
│   ├── Nova.Infrastructure/            # LibVLC wrapper, SQLite repositories, Downloader
│   └── Nova.App/                       # WPF Views, ViewModels, Themes, DI Bootstrapper
│
├── tests/
│   ├── Nova.Core.Tests/                # Unit tests for domain models, validation, DTOs
│   ├── Nova.Infrastructure.Tests/       # SQLite repositories, download engine, file parsing
│   └── Nova.App.Tests/                 # ViewModel logic, commands, state handling
│
└── packaging/
    └── Nova.Installer/                 # WiX Toolset v5 setup project / MSIX packaging
```

---

## 2. Project Breakdown & Module Responsibilities

### 2.1 `src/Nova.Core` (Target: `net8.0`)
* **Project Type**: Class Library (Pure C#, zero UI dependencies, zero native platform dependencies).
* **Purpose**: Houses core enterprise abstractions, domain models, business validation rules, interfaces, and value types.
* **Key Directories**:
  * `Models/`:
    * `MediaItem.cs`: Represents audio or video media with duration, format, path/URL, resolution, bitrate.
    * `Playlist.cs` & `PlaylistItem.cs`: Playlist container and ordered entries.
    * `PlaybackHistoryEntry.cs`: Track record of played media, timestamp, and resume point.
    * `DownloadTask.cs`: State, progress, URLs, and destination paths for active/completed downloads.
    * `AudioTrackInfo.cs`, `SubtitleTrackInfo.cs`: Audio and subtitle track representations.
    * `EqualizerBand.cs`, `EqualizerPreset.cs`: Audio equalizer settings.
  * `Interfaces/`:
    * `IPlaybackService.cs`: Playback engine contract (Play, Pause, Stop, Seek, Volume, Tracks, Equalizer).
    * `ILibraryRepository.cs`: Data access contract for media items, playlists, and history.
    * `ISettingsService.cs`: Application settings persistence contract.
    * `IDownloadService.cs`: Background download manager contract.
    * `IMetadataExtractor.cs`: Contract for media tag and stream analysis.
  * `Common/`:
    * `Result.cs` & `Result<T>.cs`: Functional error handling monad (avoids unnecessary exceptions).
    * `Guard.cs`: Argument precondition validation helpers.
    * `Constants.cs`: Supported media extensions, default limits, timing constants.

### 2.2 `src/Nova.Infrastructure` (Target: `net8.0-windows`)
* **Project Type**: Class Library.
* **Purpose**: Implements all external services: native LibVLC interop, SQLite database management, HTTP download worker, and file system indexing.
* **Key Directories**:
  * `Playback/`:
    * `LibVlcPlaybackService.cs`: Implementation of `IPlaybackService` using LibVLCSharp.
    * `LibVlcInstanceFactory.cs`: Manages single initialization of native LibVLC runtime with command-line arguments (hardware acceleration flags, network caching).
    * `EqualizerAdapter.cs`: Maps domain equalizer presets to native LibVLC equalizer structs.
  * `Persistence/`:
    * `DatabaseContext.cs`: Manages SQLite connection string, migration scripts, and schema creation.
    * `SqliteLibraryRepository.cs`: High-performance SQLite implementation of `ILibraryRepository` using `Microsoft.Data.Sqlite` or Dapper.
    * `SqliteSettingsService.cs`: Key-value configuration store.
  * `Downloader/`:
    * `ResumableDownloadService.cs`: Asynchronous chunked HTTP downloader supporting pause/resume via `Range` headers.
    * `DownloadWorker.cs`: Individual task worker with byte counting, speed calculation, and safe file renaming.
    * `SecurityValidator.cs`: Validates URLs (rejects loopback/private IPs for downloads) and sanitizes file paths.
  * `Metadata/`:
    * `LibVlcMetadataExtractor.cs`: Background media parsing using LibVLC `Media.Parse()`.
  * `Logging/`:
    * `LoggingConfigurator.cs`: Serilog rolling file logger and trace listeners.

### 2.3 `src/Nova.App` (Target: `net8.0-windows`, WPF Application)
* **Project Type**: Windows Desktop Application (`<UseWPF>true</UseWPF>`).
* **Purpose**: User interface, presentation styling, XAML controls, and ViewModel orchestration.
* **Key Directories**:
  * `ViewModels/`:
    * `MainViewModel.cs`: Shell navigation, global hotkeys, status bar.
    * `PlayerViewModel.cs`: Core video playback, seek bar, time display, track toggles, full-screen trigger.
    * `LibraryViewModel.cs`: Folder import, media grid, search input, filtering.
    * `PlaylistViewModel.cs`: Playlist creation, ordering, drag-and-drop reordering.
    * `HistoryViewModel.cs`: Chronological playback log with one-click resume.
    * `DownloadViewModel.cs`: Download queue, active speed gauges, cancel/pause actions.
    * `SettingsViewModel.cs`: Theme selection, hardware acceleration toggle, paths.
  * `Views/`:
    * `MainWindow.xaml`: App shell with navigation rail and content host.
    * `PlayerView.xaml`: Hosts `LibVLCSharp.WPF.VideoView` and nested overlay controls.
    * `LibraryView.xaml`: Media library browser with virtualized data grid.
    * `PlaylistView.xaml`: Playlist manager view.
    * `DownloadView.xaml`: Download monitor table.
    * `SettingsView.xaml`: Configuration tabs.
    * `Dialogs/`: ErrorDialog, MediaInfoDialog, ResumePromptDialog.
  * `Controls/`:
    * `VideoOverlayBar.xaml`: Auto-hiding OSD with play, pause, volume, time slider.
    * `BufferingSpinner.xaml`: Modern circular progress indicator.
    * `VolumeSlider.xaml`: Custom slider with volume level feedback.
  * `Themes/`:
    * `DarkTheme.xaml`, `LightTheme.xaml`: Color palette, brushes, typography.
    * `Controls.xaml`: Custom templates for buttons, sliders, scrollbars, menus.
  * `Converters/`:
    * `TimeSpanToTextConverter.cs`, `BytesToSpeedConverter.cs`, `BooleanToVisibilityConverter.cs`.
  * `App.xaml.cs`:
    * Application entry point, DI container configuration (`ConfigureServices`), global exception interception.

---

## 3. Test Projects Structure

* **`tests/Nova.Core.Tests`**: Fast running pure C# unit tests. Verifies domain rules, playlist parsing (M3U parser), duration formatters, result types, and security validation without disk or native dependencies.
* **`tests/Nova.Infrastructure.Tests`**: Tests SQLite schema migrations, repository CRUD operations with in-memory SQLite, download worker retry logic using `MockHttpMessageHandler`, and file path sanitization.
* **`tests/Nova.App.Tests`**: Tests ViewModels using mocked services (e.g. `Mock<IPlaybackService>`), verifying command execution, state changes, and error reporting.

---

## 4. Packaging Structure (`packaging/`)

* **`packaging/Nova.Installer`**:
  * Contains WiX Toolset v5 `.wxs` source files.
  * Automatically bundles compiled output from `Nova.App` along with native LibVLC assemblies (`libvlc.dll`, `libvlccore.dll`, and `/plugins/`).
  * Creates clean Start Menu shortcuts, sets optional registry associations for video extensions, and writes an uninstaller that cleanly purges binaries.
