# NovaPlayer — Master Test Plan & Quality Assurance Strategy

## 1. Test Objectives & Quality Standards

The NovaPlayer Quality Assurance strategy ensures high reliability, stability, format resilience, memory safety, and performance across all target Windows desktop environments.

### Quality Standard Targets
* **Unit Test Code Coverage**: Minimum 80% coverage on `Nova.Core` and non-UI logic in `Nova.Infrastructure`.
* **Zero UI Freezes**: All I/O, network streaming, database transactions, and native LibVLC calls must execute asynchronously without stalling the WPF Dispatcher.
* **Deterministic Native Resource Cleanup**: Zero memory leaks or unmanaged access violations (`0xC0000005`) during rapid media switching, window closing, or cancellation.
* **Robust Error Containment**: All corrupted files, network dropouts, and invalid URLs must be handled gracefully with user notifications, never terminating the process unexpectedly.

---

## 2. Test Categories & Scenarios

### 2.1 Unit Testing (`Nova.Core.Tests`)
* **Framework**: `xUnit`, `FluentAssertions`, `Moq`.
* **Test Focus**: Pure business rules, domain entities, validators, and formatters.
* **Key Test Suites**:
  1. `PlaylistTests`:
     * Parsing standard M3U/M3U8 headers and extended attributes (`#EXTINF`).
     * Reordering playlist entries and handling empty/corrupt playlist files.
     * Shuffle algorithm randomness and uniform distribution.
  2. `SecurityValidatorTests`:
     * Rejection of SSRF targets: `http://127.0.0.1`, `http://localhost`, `http://10.0.0.1`, `http://169.254.169.254`.
     * Rejection of dangerous schemes: `file:///`, `smb://`, `ftp://`.
     * Acceptance of valid public HTTP/HTTPS URLs.
     * Filename sanitization against directory traversal (`../../malicious.mp4` -> `malicious.mp4`) and illegal characters (`test:file?.mp4` -> `test_file_.mp4`).
  3. `ResultMonadTests`:
     * Success and failure state propagation.
     * Error code and message preservation.
  4. `TimeSpanFormatterTests`:
     * Formats durations into `hh:mm:ss` and `mm:ss` across zero, sub-hour, and multi-hour intervals.

### 2.2 Integration Testing (`Nova.Infrastructure.Tests`)
* **Test Focus**: Database persistence, HTTP download engine, file system scanner, and configuration storage.
* **Key Test Suites**:
  1. `SqliteLibraryRepositoryTests`:
     * In-memory SQLite (`Filename=:memory:`) testing schema creation, migrations, and index performance.
     * CRUD operations for `MediaItem`, `Playlist`, and `PlaybackHistoryEntry`.
     * Search query accuracy and speed (<15ms query execution for simulated 10,000 rows).
     * Concurrent read/write execution with SQLite WAL mode.
  2. `ResumableDownloadServiceTests`:
     * Utilizes `MockHttpMessageHandler` to simulate HTTP servers supporting and rejecting `Range` headers.
     * Partial download interruption and resumption verification (ensures bytes are appended, not duplicated).
     * Exponential backoff and retry behavior on transient HTTP 500/503 responses.
     * Proper disk cleanup on user-initiated download cancellation.
  3. `FileScannerTests`:
     * Deep recursive directory scanning of simulated test file hierarchies.
     * Filtering of unsupported file extensions and ignoring hidden system directories.

### 2.3 Playback Engine & Codec Verification Tests
* **Test Focus**: Real-world media decoding, audio track switching, subtitle sync, and hardware acceleration using native LibVLC binaries.
* **Test Suite Matrix**:

| Media Profile | Container | Video Codec | Audio Codec | Subtitles | Expected Behavior |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Standard 1080p** | MP4 | H.264 (AVC) | AAC Stereo | None | Hardware accelerated decode; smooth 60fps seek. |
| **Web-DL 4K HDR** | MKV | HEVC (H.265) 10-bit | E-AC3 5.1 | Embedded ASS | Tone mapping to SDR; downmix 5.1 to stereo; styled ASS render. |
| **Next-Gen Open** | WebM | AV1 | Opus | WebVTT | AV1 decode without frame drops; WebVTT subtitle sync. |
| **Multi-Audio Anime**| MKV | H.264 High 10 | FLAC Multi-ch | PGS Graphic | Dynamic switching between Japanese and English audio; graphic PGS overlay. |
| **Audio-Only Lossless**| FLAC | None | FLAC 24-bit 96kHz | Embedded tags | Album art extraction; 10-band equalizer adjustment audible. |
| **Adaptive Live Stream**| M3U8 | H.264 HLS | AAC | WebVTT | Buffering telemetry indicator (0-100%); smooth playback. |
| **Corrupted Media** | Truncated MP4 | Damaged header | Damaged | None | LibVLC handles error gracefully; player reports user dialog without crashing. |

### 2.4 UI & Interaction Testing (`Nova.App.Tests`)
* **Test Focus**: ViewModel logic, state transitions, command bindings, and hotkeys.
* **Key Test Suites**:
  1. `PlayerViewModelTests`:
     * `PlayCommand` updates player state to `Playing`.
     * `PauseCommand` updates player state to `Paused`.
     * `SeekCommand` dispatches position update to `IPlaybackService`.
     * `ToggleFullscreenCommand` notifies view via message or property change.
     * Inactivity timer auto-hides controls after 2.5 seconds of simulated mouse idle.
  2. `SettingsViewModelTests`:
     * Changing theme updates application resource dictionaries dynamically.
     * Changing hardware acceleration mode persists setting to database.

### 2.5 Security & Robustness Tests
* **SSRF Defense**: Automated test suite attempting downloads against loopback, LAN, and metadata service IP addresses.
* **Path Traversal Defense**: Simulated download attempts with crafted `Content-Disposition` headers containing `../../Windows/System32/evil.dll`.
* **Zero Crash Invariant**: Global exception handlers tested via deliberate fault injection on both UI Dispatcher and background worker threads.

### 2.6 Performance & Stress Benchmarks
* **Cold Startup**: App launch to interactive window < 1.8 seconds on standard NVMe SSD.
* **Seek Latency**: Time from user seek slider release to updated video frame rendering < 250 ms.
* **Library Query**: Full-text search over 10,000 media records returns results in < 15 ms.
* **Memory Leak Stress Test**: Rapidly switch 100 media tracks in a tight loop; measure process working set memory before and after to ensure native memory is reclaimed.

### 2.7 Packaging & Installer Verification Tests
* **Installation Smoke Test**: Install `NovaPlayerSetup.msi` on clean Windows 10 and Windows 11 machines.
* **Bundle Verification**: Confirm `libvlc.dll`, `libvlccore.dll`, and all `/plugins/*.dll` are deployed in the target application directory.
* **Shortcut & Registry**: Verify desktop shortcut, Start Menu shortcut, and file associations operate cleanly.
* **Uninstall Smoke Test**: Uninstall via Windows Settings; confirm application directory is completely removed while user library database in `%LOCALAPPDATA%` is preserved according to user choice.
