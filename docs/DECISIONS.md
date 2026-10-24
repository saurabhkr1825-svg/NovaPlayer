# NovaPlayer — Architectural Decision Records (ADRs)

This document chronicles significant architectural and technical decisions made for NovaPlayer, including the context, evaluated alternatives, selected choices, rationale, and open questions requiring technical lead/product owner guidance.

---

## ADR-01: Target .NET Version Selection (.NET 8.0 LTS vs .NET 10.0 LTS)

### Context
NovaPlayer is being planned in 2026. The host system currently has .NET 8.0, 9.0, and 10.0 desktop runtimes installed, but no .NET SDK installed yet. We need to choose the target .NET runtime version for implementation.

### Alternatives Considered
1. **.NET 8.0 (LTS)**:
   * *Pros*: Broadest ecosystem maturity; battle-tested compatibility with `LibVLCSharp.WPF` 3.x and `VideoLAN.LibVLC.Windows`; supported through November 2026; established tooling in CI/CD.
   * *Cons*: Approaching the final period of its 3-year LTS cycle.
2. **.NET 10.0 (LTS)**:
   * *Pros*: Current active LTS (supported through November 2028); latest C# 13 features; JIT performance and memory optimizations.
   * *Cons*: Bleeding edge; some third-party native wrappers may exhibit minor edge-case packaging quirks or require updated SDK tooling.

### Decision
* **Primary Target**: Target `net8.0-windows` as the initial verified baseline for Phase 1 to guarantee flawless LibVLCSharp 3.x native interop, while structuring project files with forward-compatibility for multi-targeting or a smooth upgrade to `net10.0-windows`.
* **Prerequisite**: Developer/host machine must install the .NET 8.0 or .NET 10.0 SDK prior to starting Phase 1 implementation.

---

## ADR-02: Media Engine Selection (LibVLCSharp vs Alternatives)

### Context
A desktop media player requires comprehensive format support (MKV, Web-DL, HEVC, AV1, multi-track audio, ASS subtitles, network streams) without requiring the user to install third-party DirectShow codec packs.

### Alternatives Considered
1. **WPF `MediaElement` / `MediaPlayer`**:
   * *Pros*: Built-in to WPF framework.
   * *Cons*: Relies entirely on Windows Media Foundation; fails on many MKV containers, ASS subtitles, FLAC, Vorbis, and obscure codecs out of the box without system-wide codec pack installation.
2. **FFme (FFmpeg-based WPF control)**:
   * *Pros*: Deep format support; renders directly into WPF `WriteableBitmap` solving airspace natively.
   * *Cons*: High CPU overhead on high-bitrate 4K 60fps video due to bitmap copy to UI thread; maintenance has slowed down compared to LibVLC.
3. **LibVLCSharp (`LibVLCSharp.WPF` + `VideoLAN.LibVLC.Windows`)**:
   * *Pros*: Direct official C# binding to VLC (the gold standard for desktop playback); native GPU hardware acceleration (D3D11VA, DXVA2); automatic support for MKV, Web-DL, E-AC3, ASS subtitles, HLS, DASH; active maintenance; cross-architecture support (x64, x86).
   * *Cons*: Airspace issue in WPF requires careful overlay design; native unmanaged DLLs must be packaged with installer.

### Decision
* Adopt **`LibVLCSharp.WPF`** backed by **`VideoLAN.LibVLC.Windows`**. It delivers the highest playback fidelity and reliability identical to VLC.

---

## ADR-03: UI Framework (WPF vs WinUI 3 vs Avalonia)

### Context
The application is a dedicated Windows desktop player with custom themes, high-performance video rendering, and system integration.

### Alternatives Considered
1. **WinUI 3 / Windows App SDK**:
   * *Pros*: Modern Windows 11 controls and Fluent styling out of the box.
   * *Cons*: Packaging overhead; more volatile API churn; native window handle integration with LibVLC has historically experienced edge-case rendering bugs compared to mature WPF.
2. **Avalonia UI**:
   * *Pros*: Cross-platform; modern XAML.
   * *Cons*: Cross-platform is not requested (NovaPlayer is explicitly a Windows desktop player); WPF offers deeper, battle-tested native Windows shell integration and established LibVLCSharp.WPF controls.
3. **WPF (.NET 8 Windows Desktop)**:
   * *Pros*: Extremely mature, rock-solid stability; extensive ecosystem for custom window chrome; native HWND hosting support; official, mature `LibVLCSharp.WPF.VideoView` control with overlay support; zero runtime bloat.
   * *Cons*: Airspace issue with native HWNDs (resolved via LibVLCSharp's nested overlay model).

### Decision
* Adopt **WPF on .NET 8/10**.

---

## ADR-04: WPF Airspace Handling Strategy

### Context
LibVLC renders video to a native OS window handle (HWND). In standard WPF, an HWND sits above the WPF DirectX rendering surface, occluding any WPF elements placed on top of it.

### Alternatives Considered
1. **Software Memory Callbacks to `WriteableBitmap`**:
   * *Pros*: Solves airspace completely; controls can be placed over video using standard WPF layout.
   * *Cons*: High CPU/RAM overhead; requires copying every 4K/60fps video frame through system RAM to GPU, causing frame drops and battery drain.
2. **`LibVLCSharp.WPF` Built-In Floating Overlay**:
   * *Pros*: Video renders via direct hardware acceleration to HWND; `LibVLCSharp.WPF` transparently hosts the nested XAML content on a linked transparent child window layered above the HWND; zero performance loss.
   * *Cons*: Requires nesting controls inside `<vlc:VideoView>...</vlc:VideoView>`.

### Decision
* Use the **`LibVLCSharp.WPF` built-in nested overlay model**. Controls are nested within `<vlc:VideoView>` ensuring hardware acceleration is preserved while enabling custom player OSD controls.

---

## ADR-05: Database & Persistence Layer (SQLite + Microsoft.Data.Sqlite vs EF Core)

### Context
NovaPlayer stores library media items, search indexes, custom playlists, playback history, resume points, and application settings.

### Alternatives Considered
1. **Entity Framework Core (EF Core)**:
   * *Pros*: High-level LINQ, change tracking, migrations.
   * *Cons*: Heavier binary size; slower cold-startup time; excessive overhead for a local desktop player with straightforward relational schema.
2. **`Microsoft.Data.Sqlite` + Lightweight Repository / Dapper**:
   * *Pros*: Minimal footprint; sub-millisecond query execution; lightning-fast cold start; direct control over SQLite PRAGMAs (WAL mode, synchronous=NORMAL, cache_size); simple embedded SQL scripts for schema migrations.
   * *Cons*: Requires writing explicit SQL statements for schema and CRUD queries.

### Decision
* Use **`Microsoft.Data.Sqlite`** with a custom lightweight repository pattern and SQLite Write-Ahead Logging (WAL) mode. This guarantees instant startup and sub-15ms search times over 10,000+ items.

---

## ADR-06: MVVM Architecture & State Management (CommunityToolkit.Mvvm)

### Context
WPF applications require an MVVM framework for observable properties, relay commands, and loose coupling.

### Alternatives Considered
1. **Prism / ReactiveUI**:
   * *Pros*: Extensive feature set.
   * *Cons*: Large dependency footprint; steep learning curve; excess boilerplate.
2. **`CommunityToolkit.Mvvm` (formerly Microsoft MVVM Toolkit)**:
   * *Pros*: Official Microsoft-supported toolkit; source-generator powered (`[ObservableProperty]`, `[RelayCommand]`); zero reflection overhead; extremely fast; minimal boilerplate; first-class dependency injection integration.
   * *Cons*: Requires C# Roslyn source generators (standard in modern .NET SDK).

### Decision
* Adopt **`CommunityToolkit.Mvvm`**.

---

## ADR-07: Installer & Delivery Format (WiX Toolset v5 vs MSIX)

### Context
NovaPlayer needs a frictionless Windows installer that bundles native LibVLC runtime dependencies, registers file associations, and creates desktop/start menu shortcuts.

### Alternatives Considered
1. **MSIX**:
   * *Pros*: Modern sandboxed deployment; clean uninstall; Microsoft Store compatible.
   * *Cons*: Requires code signing certificates to install without developer mode; file system virtualization can complicate direct SQLite access and external plugins.
2. **WiX Toolset v5 (MSI Installer)**:
   * *Pros*: Industry standard for enterprise and consumer Windows software; creates robust `.msi` installers; direct native dependency deployment; full registry and file association control; clean upgrade and uninstall sequences.
   * *Cons*: Requires WiX build tools during packaging phase.

### Decision
* Adopt **WiX Toolset v5** for the primary installer (`NovaPlayerSetup.msi`), with optional MSIX packaging evaluated for future Microsoft Store publishing.

---

## ADR-08: Licensing Compliance Strategy (LGPL v2.1)

### Context
* LibVLC is licensed under LGPL v2.1 (with some optional GPL plugins).
* LibVLCSharp is licensed under LGPL v2.1.
* NovaPlayer will be distributed as an open or proprietary-friendly application.

### Compliance Strategy
1. **Dynamic Linking**: NovaPlayer links to `LibVLCSharp.dll` and native `libvlc.dll` dynamically. No static linking is used.
2. **Relinking Requirement**: Because LibVLC and LibVLCSharp are dynamically loaded, end users can replace `libvlc.dll` or `LibVLCSharp.dll` with alternative versions without rebuilding NovaPlayer.
3. **License Attribution**: NovaPlayer will include a dedicated "Open Source Notices" tab in the "About" dialog displaying the LGPL v2.1 license, copyright notices, and instructions on accessing the LibVLC source code.
4. **Clean Boundary**: Domain logic (`Nova.Core`) has zero dependency on LibVLC, isolating LGPL interop entirely inside `Nova.Infrastructure`.

---

## 9. Open Decisions Requiring Technical Lead / User Input

The following design decisions are recorded as open and pending review:

1. **.NET SDK Installation**:
   * The host machine has runtimes for .NET 8 and 10, but no .NET SDK is installed in `C:\Program Files\dotnet\sdk`.
   * *Decision Needed*: Should the environment be provisioned with .NET 8.0 SDK (LTS) or .NET 10.0 SDK? (Recommended: .NET 8.0 SDK for maximum stability with LibVLCSharp 3.9).
2. **Metadata Extraction Engine**:
   * LibVLC can parse media metadata, but `TagLib#` offers specialized, comprehensive ID3v2, Vorbis, and MP4 tag extraction for audio files.
   * *Decision Needed*: Rely purely on LibVLC's built-in media parser, or add `TagLibSharp` for advanced audio library tags? (Recommended: LibVLC built-in parser for Phase 1-3 to avoid external dependencies; evaluate TagLibSharp if rich audio tag editing is prioritized).
3. **DirectShow / DVD Disc Playback**:
   * Disc playback (unencrypted physical DVD/CD) can be enabled via LibVLC, but adds UI complexity.
   * *Decision Needed*: Keep physical optical disc playback strictly out of scope for v1.0, or include an "Open Disc" menu item in Phase 4? (Recommended: Keep out of v1.0 scope).
