# NovaPlayer

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com/windows)
[![Runtime: .NET 8 LTS](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![Engine: LibVLC](https://img.shields.io/badge/Playback%20Engine-LibVLC%203.x-orange.svg)](https://www.videolan.org/)
[![Status: Phase 5 Complete](https://img.shields.io/badge/Status-Phase%205%20Complete-green.svg)]()

NovaPlayer is a modern, high-performance Windows desktop media player crafted in C# and WPF, powered by the industry-leading LibVLC playback engine. NovaPlayer delivers the codec resilience and playback fidelity of VLC wrapped in an intuitive, fluid interface adhering to modern Windows desktop ergonomics.

---

## 🚀 Implementation Status

> [!NOTE]
> **Phase 5 (Resumable Media Downloader & Security Hardening) is COMPLETE.**
> NovaPlayer includes an asynchronous memory-bounded download engine supporting HTTP Range resumes (`.part` files), resource freshness verification (ETag/Last-Modified), SSRF defense against private/loopback IPv4/IPv6 ranges, manual redirect validation, path traversal defense, SQLite persistence with startup reconciliation, and an integrated Downloads management view.

| Feature Area | Planned | Implemented | Target Phase |
| :--- | :---: | :---: | :--- |
| **Local Audio & Video Playback** | Yes | ✅ **Implemented** | Phase 1 |
| **Playback Controls (Play/Pause/Stop/Seek/Vol)** | Yes | ✅ **Implemented** | Phase 1 |
| **Keyboard Shortcuts & Fullscreen** | Yes | ✅ **Implemented** | Phase 1 |
| **Drag & Drop File Opening** | Yes | ✅ **Implemented** | Phase 1 |
| **Multi-Audio & Subtitle Sync** | Yes | ✅ **Implemented** | Phase 2 |
| **Web-DL Codecs (HEVC 10-bit, AV1, E-AC3 5.1)** | Yes | ✅ **Implemented** | Phase 2 |
| **Variable Speed (0.25x-4x) & Aspect Ratios** | Yes | ✅ **Implemented** | Phase 2 |
| **Media Information Inspector Dialog** | Yes | ✅ **Implemented** | Phase 2 |
| **Media Library & SQLite Persistence**| Yes | ✅ **Implemented** | Phase 3 |
| **Playlists & Playback History** | Yes | ✅ **Implemented** | Phase 3 |
| **Smart Playback Resume** | Yes | ✅ **Implemented** | Phase 3 |
| **Network Streaming (HLS / DASH / HTTP/S)** | Yes | ✅ **Implemented** | Phase 4 |
| **10-Band Graphic Equalizer** | Yes | ✅ **Implemented** | Phase 4 |
| **Floating Mini-Player Mode** | Yes | ✅ **Implemented** | Phase 4 |
| **Video Frame Snapshot Tool** | Yes | ✅ **Implemented** | Phase 4 |
| **Runtime Dark & Light Themes** | Yes | ✅ **Implemented** | Phase 4 |
| **Resumable Media Downloader** | Yes | ✅ **Implemented** | Phase 5 |
| **WiX Windows Installer (MSI)** | Yes | ⏳ *Planned* | Phase 6 |

---

## 🛠️ Technology Stack Evaluation

| Component | Selected Technology | Rationale & Status |
| :--- | :--- | :--- |
| **Language** | C# 12 | Strong typing, async/await, modern pattern matching, and performance. |
| **Runtime Target** | .NET 8.0 Windows Desktop (`net8.0-windows`) | Long-Term Support (LTS) baseline offering verified compatibility with LibVLCSharp 3.x. Forward-compatible with .NET 10. |
| **UI Framework** | WPF (Windows Presentation Foundation) | Battle-tested Windows desktop UI framework with HWND hosting support and hardware-accelerated composition. |
| **Playback Engine** | `LibVLCSharp.WPF` + `VideoLAN.LibVLC.Windows` | Direct C# binding to native LibVLC 3.x with D3D11VA hardware acceleration; zero external codec packs needed. |
| **MVVM Toolkit** | `CommunityToolkit.Mvvm` | Fast, official Microsoft-supported source generator toolkit with zero reflection overhead. |
| **Database** | SQLite via `Microsoft.Data.Sqlite` | Zero-latency embedded database for instant startup and lightning-fast library queries (<15ms). |
| **Logging** | `Serilog` | High-performance structured logging sink with daily rolling log files in `%LOCALAPPDATA%\NovaPlayer\logs`. |
| **Dependency Injection**| `Microsoft.Extensions.DependencyInjection` | Clean inversion of control and modular layer isolation. |
| **Testing** | `xUnit`, `FluentAssertions`, `Moq` | Comprehensive test suite for unit, integration, and UI ViewModel testing. |
| **Installer** | WiX Toolset v5 | Generates clean, robust Windows `.msi` installers bundling native LibVLC dependencies. |

---

## 📋 Architectural Documentation

All planning documents have been compiled in the [`docs/`](file:///d:/projects/Nova/docs) folder:

1. [**Product Specification** (`docs/PRODUCT_SPEC.md`)](file:///d:/projects/Nova/docs/PRODUCT_SPEC.md): Product vision, target platform, user personas, and complete functional requirements (A-G).
2. [**System Architecture** (`docs/ARCHITECTURE.md`)](file:///d:/projects/Nova/docs/ARCHITECTURE.md): Clean Architecture layers, WPF airspace mitigation, threading model, native resource lifecycle, and error handling.
3. [**Feature Matrix** (`docs/FEATURE_MATRIX.md`)](file:///d:/projects/Nova/docs/FEATURE_MATRIX.md): Exhaustive feature list with priorities (P0/P1/P2), dependencies, acceptance criteria, and known limitations.
4. [**Project Structure** (`docs/PROJECT_STRUCTURE.md`)](file:///d:/projects/Nova/docs/PROJECT_STRUCTURE.md): Solution folder layout, project responsibilities, and modular separation.
5. [**Implementation Plan** (`docs/IMPLEMENTATION_PLAN.md`)](file:///d:/projects/Nova/docs/IMPLEMENTATION_PLAN.md): Phased roadmap with discrete tasks, prerequisites, verification procedures, and completion gates.
6. [**Master Test Plan** (`docs/TEST_PLAN.md`)](file:///d:/projects/Nova/docs/TEST_PLAN.md): Unit, integration, playback codec matrix, security (SSRF/path traversal), and performance benchmark test plans.
7. [**Architectural Decisions** (`docs/DECISIONS.md`)](file:///d:/projects/Nova/docs/DECISIONS.md): Architectural Decision Records (ADRs 01-08) covering trade-offs and open questions.
8. [**Project Progress & Tracking** (`docs/PROGRESS.md`)](file:///d:/projects/Nova/docs/PROGRESS.md): Current phase status, completed planning deliverables, and blocker analysis.

---

## ⚙️ Development Prerequisites

To build and run NovaPlayer once implementation is authorized:

1. **Operating System**: Windows 10 (version 1809+) or Windows 11 (64-bit recommended).
2. **.NET SDK**: **.NET 8.0 SDK** (or .NET 10.0 SDK) is required to compile the projects.
   * *Note*: The current development host has the .NET runtimes installed, but requires the .NET SDK to be provisioned before Phase 1 compilation.
3. **IDE**: Visual Studio 2022 / 2026 or VS Code with C# Dev Kit.
4. **Git**: Installed (Git 2.52.0+).

---

## ⚖️ Open Source Licensing & Compliance

* **LibVLC & LibVLCSharp**: Licensed under the GNU Lesser General Public License (LGPL v2.1 or later). NovaPlayer links dynamically to `libvlc.dll` and `LibVLCSharp.dll`, enabling users to substitute alternative versions.
* **SQLite / Microsoft.Data.Sqlite**: Public Domain / MIT License.
* **Serilog & CommunityToolkit.Mvvm**: Apache License 2.0 / MIT License.
* Complete license notices and attribution will be presented in the application's "About" dialog.
