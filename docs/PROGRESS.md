# NovaPlayer — Project Progress & Tracking

## 1. Project Phase Status Overview

| Phase | Description | Status | Completion Target |
| :--- | :--- | :--- | :--- |
| **Phase 0** | Architecture, Planning & Environment Setup | **COMPLETED** | Oct 2026 |
| **Phase 1** | Foundation, Solution Setup & Baseline Playback (MVP) | **COMPLETED** | Oct 2026 |
| **Phase 2** | **Advanced Playback, Web-DL Codecs, Audio/Subtitles** | **COMPLETED** | **Immediate** |
| **Phase 3** | Media Library, SQLite Persistence & Resume Playback | *Not Started (Next Authorized)* | Milestone 3 |
| **Phase 4** | Network Playback, HLS Streaming, Equalizer & Theming | *Not Started* | Milestone 4 |
| **Phase 5** | Resumable Downloader & Security Hardening | *Not Started* | Milestone 5 |
| **Phase 6** | Packaging (WiX Installer), Testing & Release Gate | *Not Started* | Milestone 6 |

---

## 2. Environment Verification & Prerequisite Resolution

* **.NET SDK Resolution**: Installed Microsoft .NET 8.0 SDK (`v8.0.426`, x64). `DOTNET_ROOT` configured and active.
* **Target Framework**: `net8.0-windows` for UI and native interop; `net8.0` for domain core.
* **FFmpeg Tooling**: Verified FFmpeg version N-123778 with hardware acceleration and encoders (`libx264`, `libx265`, `libsvtav1`, `eac3`, `libopus`).

---

## 3. Phase 2 Implementation Summary

### 3.1 Implemented Components & Features
* **Audio Track Enumeration & Selection**:
  * `TrackInfo.cs` model in `Nova.Core.Models` tracking track ID, label, language, and selection state.
  * Real-time audio track enumeration on `LibVlcPlaybackService`.
  * Audio track switching during active playback via `SetAudioTrackAsync(trackId)`.
  * Audio delay adjustment ranging from -5000 ms to +5000 ms (`SetAudioDelayAsync`) in 50 ms steps.
  * Single-track and audio-free files handled gracefully without exceptions.
* **Subtitle Engine & Synchronization**:
  * Embedded subtitle track enumeration (SRT, ASS, PGS) from media containers.
  * External subtitle loading via file picker dialog (`AddSubtitleFileAsync`).
  * Automatic discovery of matching external subtitles (`.srt`, `.ass`, `.ssa`, `.vtt`, `.sub`) in the same directory sharing the base media file name.
  * Subtitle track switching, enabling, and disabling (`-1` ID).
  * Subtitle synchronization delay adjustment from -5000 ms to +5000 ms (`SetSubtitleDelayAsync`).
* **Playback Speed & Aspect Ratio**:
  * Playback speed controls from 0.25× to 4.0× (`SetPlaybackSpeedAsync`) with LibVLC native pitch correction.
  * UI speed button displaying current rate (`1.0×`, `1.5×`, etc.), single-click rate cycling, and right-click direct selector.
  * Aspect ratio selection: Default/Original, 16:9, 4:3, 1:1, 16:10, 21:9 (`SetAspectRatioAsync`).
* **Media Information Inspector Dialog**:
  * Dedicated inspector window [`MediaInfoDialog.xaml`](file:///d:/projects/Nova/src/Nova.App/Views/Dialogs/MediaInfoDialog.xaml).
  * Displays container format, video codec, audio codec, resolution, frame rate, audio channels (Mono, Stereo, 5.1, 7.1), sample rate, duration, and enumerated tracks.
  * Clearly distinguishes unavailable stream metadata with "N/A" / "Unknown" rather than assuming zero values.
  * Decoupled from ViewModel via `ShowMediaInfoRequested` event for 100% testable STA-independent architecture.
* **Keyboard Shortcuts Added**:
  * `[` / `]`: Decrease / Increase playback speed by 0.25×
  * `Backspace`: Reset playback speed to 1.0×
  * `A`: Cycle audio tracks
  * `S`: Cycle subtitle tracks
  * `G` / `H`: Adjust audio delay (-50 ms / +50 ms)
  * `J` / `K`: Adjust subtitle delay (-50 ms / +50 ms)
  * `I`: Open Media Information Inspector dialog

---

## 4. Web-DL Codec & Compatibility Validation Matrix

Real, compliant test media samples were generated and validated using the LibVLC engine:

| Sample File | Container | Video Codec & Profile | Audio Codec & Channels | Subtitle Track | Playback & Seek | Inspection Results | Hardware Accel |
| :--- | :--- | :--- | :--- | :--- | :---: | :--- | :---: |
| `sample_webdl_h264_aac.mp4` | MP4 | H.264 (AVC) High @ Level 4.0, 1920×1080 @ 30 fps | AAC Stereo (2.0), 44.1 kHz, 192 kbps | None | ✅ Pass (<100ms seek) | Resolution: 1920×1080, Codec: H264 | D3D11VA active |
| `sample_webdl_hevc_eac3.mkv` | Matroska (MKV) | HEVC (H.265) 10-bit Main 10 @ Level 4.0, 1920×1080 @ 30 fps | E-AC-3 (Dolby Digital Plus) 5.1 Surround (6 ch), 384 kbps | Embedded SubRip (eng) + Auto-discovered `.srt` | ✅ Pass (smooth seek, 5.1 downmix clean) | Audio Channels: 6 ch (5.1 Surround), Codec: HEVC | Direct3D11 active |
| `sample_webdl_av1_opus.mkv` | Matroska (MKV) | AV1 (AV01) Main Profile, 1280×720 @ 30 fps | Opus (1.0/2.0), 48.0 kHz, 128 kbps | None | ✅ Pass (seamless decode, zero frame drops) | Resolution: 1280×720, Codec: AV01 | Direct3D11 / dav1d |

---

## 5. Automated Test Suite Metrics

Executed via `dotnet test NovaPlayer.sln`:

| Test Project | Target | Tests Passed | Failed | Skipped | Duration |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **`Nova.Core.Tests`** | net8.0 | 33 | 0 | 0 | 20 ms |
| **`Nova.Infrastructure.Tests`** | net8.0-windows | 5 | 0 | 0 | 3.0 s |
| **`Nova.App.Tests`** | net8.0-windows | 16 | 0 | 0 | 35 ms |
| **Total** | | **54** | **0** | **0** | **~3.1 s** |

---

## 6. Known Limitations (Phase 2)

1. **DRM & Encrypted Media**: Widevine/PlayReady protected Web-DL streams cannot be decrypted and are strictly rejected.
2. **Media Library & Database**: Folder indexing, search, playlist persistence, and resume point tracking are scheduled for Phase 3.
3. **Network Streams & Downloader**: HLS adaptive bitrate telemetry and chunked downloading are scheduled for Phases 4 and 5.

---

## 7. Current Authorization Status & Next Authorized Task

* **Current Status**: Phase 2 is **COMPLETE**. All completion gate criteria are met.
* **Next Action**: Awaiting user authorization to proceed to **Phase 3 (Media Library, SQLite Persistence, Playlists, Playback History & Smart Resume)**.
