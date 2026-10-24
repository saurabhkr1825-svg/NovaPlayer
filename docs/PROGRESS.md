# NovaPlayer — Project Progress & Tracking

## 1. Project Phase Status Overview

| Phase | Description | Status | Completion Target |
| :--- | :--- | :--- | :--- |
| **Phase 0** | Architecture, Planning & Environment Setup | **COMPLETED** | Oct 2026 |
| **Phase 1** | Foundation, Solution Setup & Baseline Playback (MVP) | **COMPLETED** | Oct 2026 |
| **Phase 2** | Advanced Playback, Web-DL Codecs, Audio/Subtitles | **COMPLETED** | Oct 2026 |
| **Phase 3** | Media Library, SQLite Persistence & Resume Playback | **COMPLETED** | Oct 2026 |
| **Phase 4** | Network Playback, HLS Streaming, Equalizer & Theming | **COMPLETED** | Oct 2026 |
| **Phase 5** | **Resumable Downloader & Security Hardening** | **COMPLETED** | **Immediate** |
| **Phase 6** | Packaging (WiX Installer), Testing & Release Gate | *Awaiting Authorization* | Milestone 6 |

---

## 2. Environment Verification & Prerequisite Resolution

* **.NET SDK Resolution**: Installed Microsoft .NET 8.0 SDK (`v8.0.426`, x64). `DOTNET_ROOT` configured and active.
* **Target Framework**: `net8.0-windows` for UI and native interop; `net8.0` for domain core.
* **FFmpeg Tooling**: Verified FFmpeg version N-123778 with hardware acceleration and encoders (`libx264`, `libx265`, `libsvtav1`, `eac3`, `libopus`).
* **Database Driver**: `Microsoft.Data.Sqlite` v8.0.10 configured under Central Package Management (`Directory.Packages.props`).
* **LibVLC Version**: LibVLCSharp 3.9.5 with native `VideoLAN.LibVLC.Windows` v3.0.21.

---

## 3. Phase 5 Implementation Summary

### 3.1 Task 5.1 — Security Hardening & SSRF Protection
* **Protocol & Credential Security**:
  * Implemented `SecurityValidator.ValidateUrlStructure()`: strictly permits `http` and `https` schemes; immediately rejects embedded user credentials in URLs (`user:password@host`).
* **DNS Resolution & Prohibited IP Filtering**:
  * Implemented `SecurityValidator.ValidateDnsAndIpAsync()` and `IsProhibitedIpAddress()`.
  * Blocks all prohibited IPv4 and IPv6 address ranges (Loopback, RFC 1918 Private, Link-Local, Multicast, Broadcast, CGNAT, Documentation, and IPv4-mapped IPv6 ranges).
* **Manual Redirect Loop**:
  * Configured `HttpClientHandler.AllowAutoRedirect = false`.
  * Manually inspects every 301, 302, 303, 307, 308 redirect up to a strict limit of 5 hops.
  * Every redirect hop is re-validated through `SecurityValidator` to eliminate redirect-based SSRF into private networks.
* **Filesystem Traversal & Sanitization**:
  * Implemented `SecurityValidator.SanitizeFileName()`: strips directory separators and traversal patterns (`../`), removes illegal characters, trims trailing dots/spaces, and neutralizes Windows reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`).
  * Enforced directory containment via `VerifyPathContainment()`.
  * Implemented non-colliding filename generation via `GetUniqueFilePath()` to prevent silent overwriting.

### 3.2 Task 5.2 — Resumable Download Engine
* **Memory-Bounded Streaming**:
  * Utilizes `HttpClient` with `HttpCompletionOption.ResponseHeadersRead` and 64 KB buffers (`new byte[65536]`).
  * Streams response directly to `.part` files on disk without whole-file memory buffering.
* **Resumable HTTP Range Mechanics**:
  * Detects and verifies `206 Partial Content` with `Content-Range: bytes START-END/TOTAL`.
  * Validates range alignment (`START == existingBytes`); restarts safely from byte 0 if mismatched.
  * Verifies resource freshness (`ETag` and `Last-Modified`); discards partial file and restarts from 0 if the remote resource has changed.
  * Handles servers lacking range support: cleanly falls back to full download on `200 OK` without appending corrupt bytes.
  * Handles `416 Range Not Satisfiable` by automatically restarting from 0.
* **Disk Space & Limits**:
  * Validates available free disk space on the target volume via `DriveInfo` before and during streaming.
* **Atomic Promotion**:
  * Flushes buffers, verifies total downloaded bytes, and atomically promotes `.part` to final destination using `File.Move(tempPath, destPath, overwrite: false)`.

### 3.3 Task 5.3 — Download Persistence & Startup Reconciliation
* **Database Schema Migration V2**:
  * Created `downloads` table tracking `id`, `url`, `destination_path`, `temp_file_path`, `filename`, `state`, `bytes_downloaded`, `total_bytes`, `etag`, `last_modified`, `created_at`, `updated_at`, `error_message`.
  * Added indexes on `state` and `created_at`.
* **Startup Reconciliation**:
  * On application boot, downloads in active `Downloading` state are reconciled to `Paused` (preserving partial progress without untrusted automatic resumption).
  * Completed downloads verify that the final file exists on disk.
  * Interrupted or deleted `.part` files automatically sync `bytes_downloaded` with actual disk size.

### 3.4 Task 5.4 — Downloads UI & Navigation
* **Global Navigation Tab 4**:
  * Added `⬇ Downloads` tab switcher and integrated into `MainWindow.xaml` and `MainWindow.xaml.cs`.
  * Hotkey: **`D`** to toggle Downloads tab.
* **Controls & Telemetry**:
  * Real-time progress bar (determinate percentage or indeterminate for unknown lengths).
  * Transfer speed calculation and estimated remaining time (ETA).
  * Action buttons: Pause, Resume, Cancel, Retry, Open File, Open Containing Folder, Remove, Delete Data.
  * Add Download Dialog: [`AddDownloadDialog`](file:///d:/projects/Nova/src/Nova.App/Views/Dialogs/AddDownloadDialog.xaml) with real-time URL validation, destination folder browser, and optional filename override.
  * Immediate integration with player: completed media files can be played directly with one click (`OpenFileCommand`).

---

## 4. Automated Test Suite Metrics

Executed via `dotnet test NovaPlayer.sln --no-restore -m:1 /nodeReuse:false`:

| Test Project | Target | Tests Passed | Failed | Skipped | Duration |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **`Nova.Core.Tests`** | net8.0 | 110 | 0 | 0 | 24 ms |
| **`Nova.Infrastructure.Tests`** | net8.0-windows | 32 | 0 | 0 | 3.1 s |
| **`Nova.App.Tests`** | net8.0-windows | 47 | 0 | 0 | 69 ms |
| **Total** | | **189** | **0** | **0** | **~3.2 s** |

### Test Suites Added in Phase 5:
1. `SecurityValidatorTests` (Nova.Core.Tests): Scheme validation, credential rejection, IPv4/IPv6 private/loopback/link-local/multicast classification, IPv4-mapped IPv6 normalization, filename sanitization, Windows device names, path containment, unique filename generation.
2. `SqliteDownloadRepositoryTests` (Nova.Infrastructure.Tests): Migration V2 execution, CRUD operations for downloads, progress tracking, state updates, cascade removal.
3. `ResumableDownloadServiceTests` (Nova.Infrastructure.Tests): SSRF blocking of private/loopback URLs, embedded credential rejection, directory escape sanitization, Range request execution with mock HTTP handler, partial file promotion, startup reconciliation of interrupted downloads.
4. `DownloadsViewModelTests` (Nova.App.Tests): Loading persisted items, enqueuing new downloads, pause/resume dispatching, progress and state changed updates (percentage, speed, ETA), Add Download dialog requests.

---

## 5. Phase 5 Completion Gate Verification

- [x] Memory-bounded streaming download service implemented without whole-file buffering.
- [x] Resumable downloads using HTTP Range requests with `.part` staging files.
- [x] Response validation: 206 Partial Content verified, 200 OK fallback handled safely, 416 restarted cleanly.
- [x] Freshness validation via ETag and Last-Modified headers before resuming.
- [x] Atomic promotion of completed downloads to final destination.
- [x] Full security hardening: HTTP/HTTPS only, embedded credentials blocked, IPv4 and IPv6 SSRF protection, manual redirect re-validation, path traversal protection, Windows reserved names neutralized.
- [x] SQLite schema migration V2 with download persistence and startup reconciliation.
- [x] Integrated Downloads view with real-time progress, speed, ETA, pause, resume, retry, and open controls.
- [x] 100% unit test pass rate across all 189 tests (0 failures).
- [x] Zero build warnings and zero compilation errors.

---

## 6. Current Authorization Status & Next Authorized Task

* **Current Status**: Phase 5 is **COMPLETE**. All completion gate criteria are met.
* **Next Action**: Awaiting user authorization to proceed to **Phase 6 (Packaging, WiX Windows Installer, Testing & Release Gate)**.
