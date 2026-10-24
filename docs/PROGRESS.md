# NovaPlayer — Project Progress & Tracking

## 1. Project Phase Status Overview

| Phase | Description | Status | Completion Target |
| :--- | :--- | :--- | :--- |
| **Phase 0** | Architecture, Planning & Environment Setup | **COMPLETED** | Oct 2026 |
| **Phase 1** | Foundation, Solution Setup & Baseline Playback (MVP) | **COMPLETED** | Oct 2026 |
| **Phase 2** | Advanced Playback, Web-DL Codecs, Audio/Subtitles | **COMPLETED** | Oct 2026 |
| **Phase 3** | Media Library, SQLite Persistence & Resume Playback | **COMPLETED** | Oct 2026 |
| **Phase 4** | **Network Playback, HLS Streaming, Equalizer & Theming** | **COMPLETED** | **Immediate** |
| **Phase 5** | Resumable Downloader & Security Hardening | *Awaiting Authorization* | Milestone 5 |
| **Phase 6** | Packaging (WiX Installer), Testing & Release Gate | *Not Started* | Milestone 6 |

---

## 2. Environment Verification & Prerequisite Resolution

* **.NET SDK Resolution**: Installed Microsoft .NET 8.0 SDK (`v8.0.426`, x64). `DOTNET_ROOT` configured and active.
* **Target Framework**: `net8.0-windows` for UI and native interop; `net8.0` for domain core.
* **FFmpeg Tooling**: Verified FFmpeg version N-123778 with hardware acceleration and encoders (`libx264`, `libx265`, `libsvtav1`, `eac3`, `libopus`).
* **Database Driver**: `Microsoft.Data.Sqlite` v8.0.10 configured under Central Package Management (`Directory.Packages.props`).
* **LibVLC Version**: LibVLCSharp 3.9.5 with native `VideoLAN.LibVLC.Windows` v3.0.21.

---

## 3. Phase 4 Implementation Summary

### 3.1 Task 4.1 & 4.2 — Network Playback, HLS & MPEG-DASH Streaming
* **URL Validation & Security**:
  * Implemented `UrlValidator` in `Nova.Core.Common`.
  * Enforces strict scheme checking: only `http` and `https` allowed; blocks local file path injection (`file://`), `ftp://`, `smb://`, `javascript:`, and malformed URIs.
  * Stream detection helpers: `IsHlsStream` (`.m3u8`) and `IsDashStream` (`.mpd`).
* **Engine Playback Options**:
  * Added `:network-caching=1500` and `:live-caching=1500` to native LibVLC media parameters to buffer network jitter and avoid micro-stutters.
  * Native LibVLC demuxers handle HLS adaptive playlists (`.m3u8`) and MPEG-DASH manifests (`.mpd`) seamlessly.
* **UI Stream Dialog**:
  * Added `OpenUrlDialog` with real-time validation, error feedback, Enter key submission, and URL pre-filling.
  * Shortcut: `U` or top navigation bar `🌐 Stream URL`.
  * Safe cleanup: allows stopping network playback and smoothly returning to local playback.

### 3.2 Task 4.3 — Buffering Telemetry & State Telemetry
* **Native Buffering Interop**:
  * Wired `_mediaPlayer.Buffering` event to track cache filling progress (0% to 100%).
  * Exposed `BufferingProgressChanged` event across `IPlaybackService` and `PlayerViewModel`.
* **Visual Telemetry**:
  * Displayed floating center buffering indicator with percentage: `⏳ Buffering: {0:0}%`.
  * Transitions cleanly to `PlaybackState.Playing` upon buffer fill.

### 3.3 Task 4.4 — 10-Band Graphic Audio Equalizer
* **Acoustic Model**:
  * Implemented `EqualizerSettings` model in `Nova.Core.Models`.
  * 10 ISO frequency bands: [32Hz, 64Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, 16kHz].
  * Gain range: -20.0 dB to +20.0 dB per band; Preamp range: -20.0 dB to +20.0 dB.
  * Presets: Flat, Rock, Pop, Jazz, Bass Boost, Vocal, Classical, Electronic, and Custom.
* **LibVLC Native Equalizer Interop**:
  * Integrated native LibVLC `Equalizer` APIs via `SetEqualizerAsync` and `DisableEqualizerAsync`.
  * Safe null-unsetting interop with zero memory leaks.
* **Equalizer UI Dialog**:
  * `EqualizerDialog` featuring 10 vertical sliders with dB readout, preamp slider, enable/disable checkbox, preset combobox, and reset button.
  * Automatic switch to "Custom" preset when manually tweaking individual sliders.
* **Equalizer Persistence**:
  * Equalizer state serialized to JSON and stored in SQLite `app_settings` (`equalizer_config`).
  * Auto-restored on application startup and applied to all subsequent media.

### 3.4 Task 4.5 — Floating Mini-Player Mode
* **Zero-Glitch Single-Window Transform**:
  * Rather than re-parenting or creating a secondary window that causes LibVLC HWND destruction and audio stutters, `MainWindow` transforms its own window chrome into a lightweight floating mini-player:
    * `WindowStyle = WindowStyle.None`, `ResizeMode = ResizeMode.CanResizeWithGrip`, `Topmost = true`.
    * Compact footprint (420×240) positioned near bottom-right screen work area.
    * Draggable header bar allowing smooth repositioning anywhere on desktop.
    * Embedded overlay controls: Play/Pause, Mini seekbar, Time readout, Mute, Restore full window (`🗖`), and Close.
  * Full restoration: reverts back to normal size, position, and window style without interrupting active video or audio playback.
  * Keyboard shortcut: `F9` to toggle, `Escape` to exit mini-player mode.

### 3.5 Task 4.6 — Video Frame Snapshot Tool
* **High-Precision Frame Capture**:
  * Integrated LibVLC native frame capture: `_mediaPlayer.TakeSnapshot(0, targetPath, 0, 0)`.
  * Synchronously saves active video frame to disk in PNG format.
  * Default destination: `%USERPROFILE%\Pictures\NovaPlayer\Snapshot_{Timestamp}.png`.
  * Automatic directory creation and sanitized filename formatting.
* **Triggers**:
  * Top navigation bar `📷 Snapshot` button.
  * Global keyboard shortcut: `Shift + S`.
  * Real-time status bar notification confirming saved image path.

### 3.6 Task 4.7 — Runtime Theming (Dark & Light Themes)
* **WPF Merged Resource Dictionaries**:
  * Created `DarkTheme.xaml` and `LightTheme.xaml` color palettes with matching keys:
    * `AppBackgroundBrush`, `SurfaceBrush`, `SurfaceAltBrush`, `OverlayBackgroundBrush`, `AccentBrush`, `AccentHoverBrush`, `TextPrimaryBrush`, `TextSecondaryBrush`, `BorderBrush`.
  * Converted UI element references from static to `DynamicResource`.
* **ThemeManager Service**:
  * Runtime dictionary swapping via `ThemeManager.ApplyTheme()` and `ThemeManager.ToggleTheme()`.
  * Seamless theme toggle with **zero application restart**.
  * Persistence: theme preference saved to SQLite `app_settings` (`theme`) and restored on launch.
  * Shortcut: `T` or top bar `🌓 Theme` button.

---

## 4. Automated Test Suite Metrics

Executed via `dotnet test NovaPlayer.sln --no-restore -m:1 /nodeReuse:false`:

| Test Project | Target | Tests Passed | Failed | Skipped | Duration |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **`Nova.Core.Tests`** | net8.0 | 65 | 0 | 0 | 27 ms |
| **`Nova.Infrastructure.Tests`** | net8.0-windows | 20 | 0 | 0 | 1.1 s |
| **`Nova.App.Tests`** | net8.0-windows | 42 | 0 | 0 | 28 ms |
| **Total** | | **127** | **0** | **0** | **~1.3 s** |

### Test Suites Added in Phase 4:
1. `UrlValidatorTests` (Nova.Core.Tests): Valid HTTP/HTTPS URLs, protocol restriction (blocking file/ftp/smb/javascript), malformed URLs, HLS (.m3u8) detection, MPEG-DASH (.mpd) detection.
2. `EqualizerSettingsTests` (Nova.Core.Tests): 10-band validation, gain limits (-20dB to +20dB), preset definitions and acoustic profile verification.
3. `NetworkPlaybackTests` (Nova.Infrastructure.Tests): Real HTTP streaming over embedded loopback server with HTTP 206 Partial Content / Range requests, live Equalizer manipulation during playback, video frame snapshotting, unreachable stream error handling.
4. `EqualizerViewModelTests` (Nova.App.Tests): Preset loading, manual band adjustment switching to Custom, reset to Flat, enable/disable engine dispatch, SQLite persistence.
5. `PlayerViewModelPhase4Tests` (Nova.App.Tests): Buffering percentage and state telemetry, URL validation and stream dispatch, mini-player toggle and exit, snapshot execution, dialog events.

---

## 5. Phase 4 Completion Gate Verification

- [x] Direct HTTP/HTTPS network playback implemented with URL validation and security protocol checks.
- [x] Evaluated and verified HLS and MPEG-DASH streaming options with LibVLC network caching.
- [x] Buffering telemetry exposed to UI with percentage progress indicator.
- [x] 10-band graphic audio equalizer implemented with presets, preamp, and SQLite persistence.
- [x] Floating mini-player mode implemented with zero-glitch window transform, topmost state, and draggable chrome.
- [x] Video frame snapshot tool implemented saving to Pictures directory with `Shift+S` shortcut.
- [x] Dark and Light theme runtime switching implemented without application restart.
- [x] 100% unit test pass rate across all 127 tests (0 failures).
- [x] Zero build warnings and zero compilation errors.

---

## 6. Current Authorization Status & Next Authorized Task

* **Current Status**: Phase 4 is **COMPLETE**. All completion gate criteria are met.
* **Next Action**: Awaiting user authorization to proceed to **Phase 5 (Resumable Media Downloader & Security Hardening)**.
