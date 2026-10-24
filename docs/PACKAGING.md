# NovaPlayer Packaging, Distribution, and Installer Architecture

## 1. Overview

NovaPlayer is distributed as an independent, standalone Windows x64 desktop package. The release distribution contains two primary outputs:
1. **Self-Contained Application Bundle (`dist/app/`)**: A self-contained .NET 8 LTS desktop runtime environment paired with native LibVLC 3.0.21 x64 engine binaries, requiring no pre-installed .NET runtimes on target machines.
2. **Windows Installer (`dist/installer/NovaPlayer-1.0.0-x64.msi`)**: A WiX Toolset v5-authored MSI package providing standard Windows installation, Start Menu / Desktop shortcuts, clean uninstall, safe in-place upgrades, and non-intrusive file type registration via Windows Capabilities.

---

## 2. Release Configuration & Build Parameters

The build pipeline compiles the solution in `Release` configuration targeting `net8.0-windows` and `win-x64`:

```powershell
dotnet publish src/Nova.App/Nova.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o dist/app `
  /p:Version=1.0.0 `
  /p:AssemblyVersion=1.0.0.0 `
  /p:FileVersion=1.0.0.0
```

### Build Optimization & Pruning Rules
- **Redundant Architecture Pruning**: `VideoLAN.LibVLC.Windows` bundles both 32-bit (`win-x86`) and 64-bit (`win-x64`) runtimes. The build engine removes `dist/app/libvlc/win-x86`, saving ~75 MB of unneeded binaries from the 64-bit package.
- **Debug Symbol Stripping**: PDB debug symbols are stripped from the distribution folder before MSI packaging to maintain minimal package size.
- **License File Bundling**: Plaintext `THIRD_PARTY_LICENSES.txt` is verified in the application root next to `NovaPlayer.exe`.

---

## 3. Native LibVLC Packaging & Discovery

NovaPlayer relies on native LibVLC shared libraries dynamically linked via P/Invoke.

### Layout within the Distributed Bundle
```text
dist/app/
├── NovaPlayer.exe
├── NovaPlayer.dll
├── THIRD_PARTY_LICENSES.txt
├── libvlc/
│   └── win-x64/
│       ├── libvlc.dll
│       ├── libvlccore.dll
│       ├── plugins/
│       │   ├── access/
│       │   ├── audio_filter/
│       │   ├── audio_output/
│       │   ├── codec/
│       │   ├── demux/
│       │   ├── video_filter/
│       │   └── video_output/ (320 plugin DLLs)
│       ├── lua/
│       └── hrtfs/
└── [Core .NET 8 Runtime & WPF Assemblies]
```

### Dynamic Initialization
At startup, `LibVlcPlaybackService.EnsureCoreInitialized()` resolves the runtime path:
1. Evaluates `AppDomain.CurrentDomain.BaseDirectory`.
2. Locates `libvlc\win-x64\libvlc.dll`.
3. Calls `LibVLCSharp.Shared.Core.Initialize(libvlcDir)`.
4. In accordance with LGPL 2.1 Section 6, users have full technical capability to inspect, patch, or replace the DLLs and plugins in `libvlc\win-x64\` with custom builds.

---

## 4. WiX Toolset v5 MSI Architecture

The installer is authored in `installer/NovaPlayer.wxs` and compiled using WiX Toolset v5 (`wix build`):

### Key Specifications
- **Package Id / Scope**: `perMachine` (installs to `C:\Program Files\NovaPlayer`).
- **Upgrade Code**: `A8E148B4-66F5-46D9-BA7D-1F05F9A64582` (fixed GUID ensuring automatic upgrade detection and removal of earlier releases).
- **Compression**: High-compression CAB file embedded directly inside the MSI.
- **Shortcuts**:
  - Start Menu: `[ProgramMenuFolder]\NovaPlayer\NovaPlayer.lnk`
  - Desktop: `[DesktopFolder]\NovaPlayer.lnk`
- **User Data Preservation**:
  - NovaPlayer writes its SQLite database (`novaplayer.db`), user settings (`settings.json`), and logs to `%LOCALAPPDATA%\NovaPlayer\`.
  - The MSI installer exclusively manages `Program Files` and registry keys. Upgrades or uninstalls never touch user libraries, playback history, or downloads.
- **Non-Intrusive File Association Registration**:
  - Registers ProgID `NovaPlayer.AssocFile`.
  - Registers Windows Capabilities under `HKLM\Software\NovaPlayer\Capabilities` and `HKLM\Software\RegisteredApplications`.
  - Registers `OpenWithProgids` for supported media containers (`.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.webm`, `.mp3`, `.flac`, `.wav`, `.aac`, `.ogg`, `.m4a`, `.m3u`, `.m3u8`).
  - Does **not** violently hijack default file handlers; allows users to select NovaPlayer via Windows "Open with" or Windows Settings > Default Apps.

---

## 5. Automated Build Instructions

To reproduce the complete Release binaries and MSI installer from scratch:

```powershell
# 1. Install WiX v5 toolset (if not already installed)
dotnet tool install --global wix --version 5.0.2
wix extension add WixToolset.UI.wixext/5.0.2 -g
wix extension add WixToolset.Util.wixext/5.0.2 -g

# 2. Run automated release build script
powershell -ExecutionPolicy Bypass -File installer/build-installer.ps1
```

The compiled installer will be generated at:
`dist/installer/NovaPlayer-1.0.0-x64.msi`
