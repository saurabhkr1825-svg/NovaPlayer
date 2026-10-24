# NovaPlayer 1.0.0 Release Notes

**Release Date:** October 2026  
**Platform:** Windows 10 / 11 (64-bit)  
**Distribution:** WiX v5 MSI Installer (`NovaPlayer-1.0.0-x64.msi`)  
**SHA-256 Checksum:** `755C94F361F3F914892B39B76BE301CC655B69900CB5738FB926B19FD8AF9FFE`

---

## Highlights

NovaPlayer 1.0.0 is the inaugural public release of NovaPlayer, a modern, high-performance Windows desktop media player engineered in C# 12 and WPF on .NET 8 LTS, backed by the industrial-strength LibVLC 3.0 multimedia engine.

### Core Features & Capabilities

- **VLC-Grade Playback Engine**:
  - Direct hardware-accelerated video decoding (Direct3D11 / DXVA2).
  - Broad container and codec support: MP4, MKV, AVI, MOV, WebM, FLV, TS, MP3, FLAC, AAC, Opus, Vorbis, and more.
  - Multi-track audio selection with language metadata and +/- 5000 ms audio delay synchronization.
  - Subtitle engine supporting embedded subtitle tracks (SRT, ASS/SSA, VTT) and external file loading with synchronization controls.
  - Aspect ratio switching (Original/Default, 16:9, 4:3, 1:1, 16:10, 21:9) and variable speed playback (0.25x to 4.0x).

- **Media Library & SQLite Persistence**:
  - Background asynchronous folder scanning with non-blocking UI dispatcher updates.
  - Persistent SQLite database in WAL (Write-Ahead Logging) mode with transactional migrations.
  - Full playlist management (.m3u/.m3u8 import, export, and interactive reordering).
  - Playback history and automatic resume prompt for partially watched media.

- **Network Streaming & Studio Audio Controls**:
  - Live network playback supporting direct HTTP/HTTPS URLs, HLS (.m3u8), and MPEG-DASH (.mpd).
  - 10-band graphic equalizer with pre-configured audio presets (Flat, Rock, Pop, Jazz, Classical, Bass Boost, Vocal, etc.) and custom band adjustment.
  - Clean video frame snapshot capture directly saved to user Pictures folder.
  - Floating, borderless mini-player mode with always-on-top window pin.
  - System-responsive Light / Dark UI themes with smooth transition brush resources.

- **Resumable Media Downloader & Security Hardening**:
  - Memory-bounded streaming downloads with HTTP Range byte-level resumption.
  - Anti-corruption `.part` staging files with atomic promotions.
  - Server-Side Request Forgery (SSRF) defense filtering private IP ranges, loopback addresses, multicast, and broadcast blocks across manual redirect chains.
  - Filesystem traversal defense and Windows reserved device name neutralization.

- **Enterprise-Grade Windows Packaging**:
  - Standalone, self-contained Windows x64 distribution (no .NET prerequisites required).
  - Windows Installer (.msi) produced via WiX Toolset v5.
  - Automatic Start Menu and Desktop shortcut creation.
  - Non-intrusive Windows Capabilities and Open With registration respecting user default player settings.
  - Full LGPL-2.1 dynamic linking compliance with user-replaceable LibVLC binaries and third-party license notices.

---

## Installation & System Requirements

- **Operating System:** Windows 10 (version 1809 or higher) or Windows 11 (64-bit).
- **Architecture:** x64 (AMD64 / Intel 64).
- **Hardware Acceleration:** DirectX 11 / Direct3D 11 capable graphics adapter recommended.
- **Disk Space:** ~200 MB for installation.

### Installation Instructions
1. Download `NovaPlayer-1.0.0-x64.msi`.
2. Double-click the installer and follow the setup wizard.
3. Review the license agreement and accept to proceed.
4. Launch NovaPlayer from the Start Menu or Desktop shortcut.

### Uninstallation
1. Open Windows **Settings > Apps > Installed apps** (or **Control Panel > Programs and Features**).
2. Locate **NovaPlayer** and click **Uninstall**.
3. *Note:* User playlists, media library database, and application settings stored in `%LOCALAPPDATA%\NovaPlayer` are preserved so you do not lose personal data upon upgrade or reinstallation.

---

## Known Limitations

1. **Architecture Limitation**: NovaPlayer 1.0.0 is released exclusively as a 64-bit (`win-x64`) package. 32-bit Windows systems are not supported.
2. **DRM & Protected Streams**: Encrypted or DRM-protected streams (e.g. Netflix, Amazon Prime, Widevine, PlayReady) are not supported.
3. **Platform Scrapers**: NovaPlayer intentionally does not contain unauthorized video scrapers for YouTube, Vimeo, or social networks; direct media stream URLs must be provided.
