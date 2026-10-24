# NovaPlayer — System Architecture Document

## 1. Architectural Principles & Overview

NovaPlayer follows a modern, modular **Clean Architecture** combined with the **MVVM (Model-View-ViewModel)** design pattern. It enforces strict separation of concerns, loose coupling through dependency injection (`Microsoft.Extensions.DependencyInjection`), deterministic resource disposal, and non-blocking asynchronous I/O across all layers.

### High-Level Architectural Diagram

```mermaid
graph TD
    subgraph Presentation Layer ["1. Presentation Layer (WPF / XAML)"]
        UI_Views["Views & Controls (MainWindow, VideoView, LibraryView, Dialogs)"]
        UI_Styles["Themes, Resource Dictionaries, Converters"]
        UI_VM["ViewModels (PlayerViewModel, LibraryViewModel, DownloadViewModel)"]
    end

    subgraph Application Layer ["2. Application / Core Service Layer"]
        App_Commands["User Commands & Orchestrators"]
        App_Interfaces["Service Interfaces (IPlayerService, ILibraryService, IDownloadService)"]
        App_DTOs["Data Transfer Objects, Events & Notifications"]
    end

    subgraph Infrastructure Layer ["3. Infrastructure Layer"]
        Infra_Playback["Playback Engine (LibVLCSharp Wrapper, Hardware Accel Config)"]
        Infra_Data["Persistence (SQLite / Microsoft.Data.Sqlite Repository)"]
        Infra_Download["Download Engine (HttpClient, Resumable Chunk Manager)"]
        Infra_IO["File System Scanner & Metadata Parser"]
        Infra_Logging["Serilog Structured Logger & Diagnostics"]
    end

    subgraph Native Layer ["4. Native Runtime & External Dependencies"]
        Native_LibVLC["LibVLC Native Binaries (libvlc.dll, libvlccore.dll, /plugins/)"]
        Native_SQLite["e_sqlite3.dll"]
    end

    UI_Views --> UI_VM
    UI_VM --> App_Interfaces
    App_Interfaces <|.. Infra_Playback
    App_Interfaces <|.. Infra_Data
    App_Interfaces <|.. Infra_Download
    App_Interfaces <|.. Infra_IO
    App_Interfaces <|.. Infra_Logging

    Infra_Playback --> Native_LibVLC
    Infra_Data --> Native_SQLite
```

---

## 2. Layer Responsibilities & Dependency Direction

### 2.1 Dependency Inversion Principle (DIP)
Dependencies flow inward toward domain abstractions. The UI and Infrastructure layers depend upon Core abstractions (`Nova.Core`), never directly upon each other.

| Layer | Project Assembly | Key Responsibilities | Allowed Dependencies |
| :--- | :--- | :--- | :--- |
| **Presentation** | `Nova.App` | XAML Views, custom user controls, value converters, view models, application bootstrapper. | `Nova.Core`, `CommunityToolkit.Mvvm`, `LibVLCSharp.WPF` |
| **Core (Domain/App)** | `Nova.Core` | Entities, value objects, service contracts (`IPlaybackService`, `ILibraryRepository`), domain events, DTOs, result types. | None (Clean POCO, standard library only) |
| **Infrastructure** | `Nova.Infrastructure` | LibVLC adapter implementation, SQLite repositories, HttpClient downloader, metadata extraction, logging configuration. | `Nova.Core`, `LibVLCSharp`, `Microsoft.Data.Sqlite`, `Serilog` |

---

## 3. Playback Architecture & WPF Airspace Solution

### 3.1 The Airspace Challenge in WPF
WPF renders using a DirectX composition tree managed by `DirectWrite` and `milcore`. LibVLC's default high-performance video sink (`direct3d11` or `direct3d9`) outputs directly to an OS window handle (HWND). In standard WPF, an HWND hosted via `HwndHost` cuts through the WPF visual tree (the notorious "airspace problem"), rendering all sibling WPF elements underneath the video surface invisible.

### 3.2 NovaPlayer Airspace Mitigation Strategy
NovaPlayer utilizes a two-tier strategy:

```mermaid
flowchart LR
    A[LibVLC Video Frame Output] --> B{Rendering Mode}
    B -->|Mode 1: Primary HWND with Overlay Host| C[LibVLCSharp.WPF VideoView]
    C --> D[Child Content Overlay in Floating HWND Child Window]
    D --> E[Zero-Copy GPU Passthrough, 4K/60FPS Flawless]
    
    B -->|Mode 2: Fallback In-Tree DirectX Texture| F[LibVLC D3D11 Callback to D3DImage]
    F --> G[Direct WPF Composition Layering]
    G --> H[Allows Complex Alpha Blending & WPF Effects]
```

1. **Primary Mode (High Performance HWND Overlay)**:
   * Uses `LibVLCSharp.WPF.VideoView`.
   * UI controls (OSD, seek bar, play/pause buttons, track selectors) are nested directly inside `<vlc:VideoView>...</vlc:VideoView>`.
   * `LibVLCSharp.WPF` automatically positions a transparent child window containing the nested WPF elements precisely over the video HWND, synchronized with window move/resize events.
   * Delivers maximum performance, zero memory-copy video rendering, and native GPU hardware acceleration for 4K HEVC/AV1.

2. **Window Message Forwarding**:
   * Mouse and keyboard events on the video window are intercepted and routed to the `PlayerViewModel` so that double-clicking for fullscreen, mouse-wheel volume adjustment, and hotkeys function seamlessly across the entire surface.

---

## 4. Threading Model & Concurrency

NovaPlayer operates on a strict multi-threaded architecture to guarantee that the UI thread never freezes.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant UI as WPF UI Thread
    participant VM as PlayerViewModel
    participant Service as PlaybackService (Async)
    participant LibVLC as LibVLC Native Threadpool
    participant DB as SQLite Task Queue

    User->>UI: Clicks Play on Media Item
    UI->>VM: Execute PlayCommand(MediaItem)
    VM->>Service: PlayAsync(path, resumePosition)
    Note over Service,LibVLC: Task.Run (Offload to background thread)
    Service->>LibVLC: mediaPlayer.Play(Media)
    LibVLC-->>Service: Playback Started
    LibVLC->>UI: Native Event (TimeChanged, LengthChanged)
    Note over UI: Dispatched via SynchronizationContext
    UI->>VM: Update CurrentTime & SeekBar
    
    Note over VM,DB: Periodic Throttle (every 5 seconds)
    VM->>DB: SavePlaybackPositionAsync(mediaId, timeMs)
```

### Threading Rules:
1. **UI Thread (WPF Dispatcher)**: Strictly reserved for rendering, animations, user input events, and ViewModel property updates. Never perform file I/O, database access, or blocking network calls on this thread.
2. **LibVLC Native Threadpool**: Native C threads created by LibVLC manage decoding, demuxing, and hardware presentation. LibVLC events (`TimeChanged`, `Buffering`, `EndReached`) fire on native threads.
3. **Synchronization Context**: The `PlaybackService` intercepts LibVLC events and dispatches them to the WPF `Dispatcher` (or UI `SynchronizationContext`) using throttled updates (e.g., seekbar updates capped at 10 Hz) to eliminate Dispatcher saturation.
4. **I/O & Persistence Threads**: SQLite queries and media file indexing execute via async/await using `Task.Run` and `IAsyncEnumerable`.
5. **Download Workers**: The `DownloadService` manages background tasks with thread-safe cancellation tokens, progress reporting throttled to 4 Hz, and asynchronous buffered stream writes.

---

## 5. Resource Lifecycle & Deterministic Disposal

LibVLC utilizes unmanaged C pointers. Failure to dispose of native objects in the correct order results in memory leaks or native access violations (`0xC0000005`).

### 5.1 Object Hierarchy & Disposal Order
```mermaid
graph TD
    A[LibVLC Core Engine (Singleton per App Domain)] --> B[MediaPlayer Instance (Scoped to Player Session)]
    B --> C[Media Instance (Transient per Playable Item)]
    C --> D[Native Buffers & Decoder Contexts]
```

* **`LibVLC` (Core Engine)**: Initialized once at application startup via `LibVLCSharp.Shared.Core.Initialize()`. Wrapped as a singleton service in DI (`ISingletonVlcInstance`). Disposed only upon application shutdown.
* **`MediaPlayer`**: Created once per player lifetime. Bound to the `VideoView`. Reused across media tracks. Disposed when closing the application or when tearing down the player view.
* **`Media`**: Instantiated when a new track or URL is requested. The previous `Media` object is explicitly disposed **before** creating or assigning the new `Media` object.
* **SQLite Connection**: Managed via a thread-safe connection factory with WAL (Write-Ahead Logging) enabled.

---

## 6. Error Handling Strategy & Fault Tolerance

NovaPlayer enforces structured error resilience across three distinct rings:

```mermaid
flowchart TD
    subgraph Ring 1 ["Ring 1: Outer Shell (Global Unhandled)"]
        G1[AppDomain.CurrentDomain.UnhandledException]
        G2[DispatcherUnhandledException]
        G3[TaskScheduler.UnobservedTaskException]
    end

    subgraph Ring 2 ["Ring 2: Service Layer (Result Pattern)"]
        S1[PlaybackResult.Failure(reason)]
        S2[DownloadResult.Failed(exception)]
        S3[DatabaseResult<T>]
    end

    subgraph Ring 3 ["Ring 3: Hardware & Native (LibVLC Log Sink)"]
        N1[LibVLC Native Log Callback]
        N2[Hardware Fallback: D3D11 -> DXVA2 -> Software]
    end

    G1 & G2 & G3 --> L[Serilog Structured File Log]
    S1 & S2 & S3 --> UI_Alert[User-Friendly Notification Toast / Dialog]
    N1 --> L
```

1. **Hardware Fallback Chain**:
   If hardware-accelerated decoding fails during playback initialization (e.g., driver incompatibility), LibVLC automatically attempts fallback to software decoding. If unrecoverable, `PlaybackService` reports a structured `PlaybackFailureException` and offers to switch default decoding mode to Software.
2. **Operation Result Pattern**:
   Services return `Result<T>` or `Result` objects containing explicit error codes and user-safe messages, avoiding raw exceptions for operational failures (file not found, network timeout, unsupported stream).
3. **Crash Reporting**:
   Unhandled crashes write an emergency minidump or exception trace to `%LOCALAPPDATA%\NovaPlayer\crashes\` and display a recovery dialog allowing safe restart.

---

## 7. Security Architecture

1. **Safe URL Parsing**:
   * Network streams and downloads only permit `http` and `https` schemes.
   * `file://` schemes are treated as local files and routed through filesystem permission checks.
   * `ftp://`, `gopher://`, `file://` over SMB, and internal IP address formats (127.0.0.1, 10.0.0.0/8, 192.168.0.0/16, 169.254.0.0/16) are blocked for download requests to prevent SSRF vulnerabilities.
2. **Input Sanitization**:
   * File paths received from external drops or playlists are normalized using `Path.GetFullPath` and verified against directory traversal (`..\`).
   * Downloaded filenames are sanitized against Windows illegal characters (`<>:"/\|?*`).
3. **No Dynamic Code Execution**:
   * Subtitles (ASS/SSA) are rendered via native LibVLC subtitle filter; no script execution is supported.
