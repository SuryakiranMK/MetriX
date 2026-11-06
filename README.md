<p align="center">
  <img src="assets/icon_256.png" width="120" height="120" alt="metriX Icon" />
</p>

# metriX ⚡
> **Ultra-lightweight (<4 MB RAM, <0.01% CPU), standalone, 100% offline real-time network speed indicator & HUD for Windows 11.**

[![Release](https://img.shields.io/github/v/release/SuryakiranMK/MetriX?color=00E5FF&label=Release)](https://github.com/SuryakiranMK/MetriX/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20%7C%20Windows%2010-0078D4?logo=windows)](https://github.com/SuryakiranMK/MetriX)
[![Offline](https://img.shields.io/badge/Network%20Telemetry-0%20Internet%20Needed%20(100%25%20Offline)-brightgreen)](#-0-internet-required-for-monitoring-100-offline--private)
[![Binary Size](https://img.shields.io/badge/Binary%20Size-~221%20KB%20(Single%20.exe)-success)](#-performance-benchmarks)
[![RAM Usage](https://img.shields.io/badge/RAM%20Footprint-%3C%204--6%20MB-blue)](#-performance-benchmarks)
[![CPU Usage](https://img.shields.io/badge/CPU%20Usage-%3C%200.01%25-brightgreen)](#-performance-benchmarks)
[![License](https://img.shields.io/badge/License-MIT-purple.svg)](LICENSE)

---

## 🌟 Overview

**metriX** is engineered specifically for power users, developers, and gamers who demand real-time network speed and bandwidth visibility directly on their taskbar or desktop without the bloat, battery drain, or visual clutter of web-based widgets.

Built from scratch in pure C# with direct Win32 P/Invoke, metriX integrates natively into the Windows 11 shell with **true 32-bit layered alpha transparency**, allowing the taskbar's native dark acrylic and Mica glass material to shine through cleanly.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ [Start] [Explorer] [Terminal] ... [Apps]        [▲  1.4 MB/s] [^] [Tray] [ENG] [Clock] │
│                                                 [▼ 18.2 MB/s]                          │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 🛡️ 0 Internet Required for Monitoring (100% Offline & Private)

Unlike many monitoring tools that depend on web services, external telemetry servers, or cloud accounts, **metriX requires absolutely zero internet access for its real-time monitoring engine**:

- **Direct OS Kernel NDIS Telemetry**: Queries interface octet counters directly from Windows kernel network tables via `GetIfTable2` (`iphlpapi.dll`).
- **Air-Gapped & Offline Ready**: Fully functional in air-gapped workstations, airplane mode, offline LAN tournaments, and local homelab networks.
- **100% Telemetry Privacy**: Generates **0 network packets** during monitoring. No telemetry, no analytics, no user tracking, and no cloud servers. What happens on your machine stays on your machine.
- *(Note: The optional built-in Speed Test and Update Checker only connect to the internet when you explicitly trigger them).*

---

## ✨ Features at a Glance

### 👁️ Display-Only (Click-Through HUD) Mode
- **Zero-Interference Gaming/Work Overlay**: Make the indicator completely click-through (`WS_EX_TRANSPARENT`). All mouse clicks, double-clicks, and gestures pass cleanly through to the underlying application or game.
- **Global Unlock Hotkey**: Press **`Ctrl + \``** (Ctrl + Backquote / Tilde, default) from anywhere in Windows to instantly toggle interactivity back on.
- **Customizable Shortcut**: Reconfigure your preferred unlock combination (`Ctrl + \``, `Ctrl + Shift + \``, `Ctrl + Alt + M`, `Ctrl + F12`, etc.) in Settings.
- **Tray Toast Notification**: Visual balloon confirms mode transitions and reminds you of your active shortcut.

### 📍 Direct Dragging & Detachable Floating Widget
- **Direct Taskbar Left-Click Drag**: Click and drag the indicator directly with your left mouse button to position it anywhere along your taskbar—no complex alignment math needed.
- **Detachable Floating Desktop Widget**: Detach the meter from the taskbar into a draggable desktop HUD widget.
- **Widget Sizing & Lock**: Scale from 80% (compact) to 150% (presentation), toggle Always-on-Top, and lock position with one click (`🔒 Lock Position`).

### 🖥️ Multi-Monitor Taskbar Mirroring
- Automatically identifies all active displays and secondary taskbar surfaces (`Shell_SecondaryTrayWnd`).
- Renders synchronized, DPI-aware speed indicators across every connected monitor with independent per-display positioning.

### 📈 Full-Bleed High-DPI Flyout Window
- **Live Throughput Waveforms**: 60 FPS double-buffered real-time download and upload graphs with selectable **30s**, **60s**, or **5m** timeframes.
- **Historical Data Usage Tracking**: Real-time persisted tracking of data consumed **Today**, **This Week**, and **This Month** with automatic calendar boundary rollover.
- **Monthly Data Quota Bar**: Set an optional monthly data allowance and monitor usage with a dynamic visual warning bar.
- **High-DPI Full-Bleed Layout**: Automatically expands to fill 100% of window width on 100%, 125%, 150%, and 200% scaling displays with zero right-side dead space.
- **Native Caption Close Button**: Flush top-right corner `✕` button with Windows 11 Fluent hover styling.
- **Custom Dark Scrollbar**: Clean 6px Fluent dark scrollbar replacing stark white Win32 scrollbars.

### 🔍 Per-App Bandwidth Attribution & Grouping
- **Consolidated Process Telemetry**: Automatically aggregates multi-process applications (e.g. Chrome, Discord, Steam, Antigravity) into single rows.
- **Real-Time I/O Attribution**: Queries active socket connections and process I/O counters via Win32 IP Helper and `GetProcessIoCounters`.
- **Configurable Process Limit**: Choose **3**, **5 (Recommended)**, **8**, **10**, or **Auto (All active consumers)** directly from Settings.

### ⚡ Integrated Speed Test Benchmark Engine
- **Multi-Threaded CDN Streaming**: Measure ping latency, download bandwidth, and upload throughput using multi-threaded edge CDN chunk streaming.
- **Configurable Assessment Duration**: Choose assessment duration (**5s**, **10s Recommended**, **15s**, or **30s**) with live progress feedback and speedometer gauge.
- **Local Benchmark History**: Saves recent test results locally for quick comparison.

### 🎨 Deep Customization & Windows 11 Aesthetics
- **Typography**: Rendered in crisp **Segoe UI Variable Text** with ClearType and tabular numerals to eliminate number jitter.
- **Layout Modes**: Two-Row Stacked (calibrated to the exact height of the Windows 11 clock) or Single-Row Horizontal.
- **Dynamic Auto-Theming**: Automatically synchronizes colors with your Windows 11 accent theme, or choose custom RGB colors with one-click presets.
- **Arrow Styles**: Triangles (`▲ / ▼`), Arrows (`↑ / ↓`), Letters (`U: / D:`), or None.
- **Unit Formats**: Auto Bytes (`B/s`, `KB/s`, `MB/s`, `GB/s`), Auto Bits (`bps`, `Kbps`, `Mbps`), or Fixed units.
- **Idle Dimming & Opacity**: Automatically dims to low contrast when throughput drops below an adjustable threshold.

### 🔄 In-House Updater & Silent Auto-Updates
- One-click update check with native dark release notes viewer and progress bar.
- Optional **Silent Auto-Update** mode for background, zero-prompt maintenance.

---

## 📊 Performance Benchmarks

| Metric | metriX | Traditional Electron Widgets | Python Taskbar Scripts | Rainmeter Skins |
| :--- | :--- | :--- | :--- | :--- |
| **CPU Utilization** | **< 0.01% (0.0% in Task Manager)** | 1.0% – 3.5% | 0.4% – 1.2% | 0.5% – 2.0% |
| **Physical Working Set (RAM)** | **~4 – 6 MB** | 150 – 300 MB | 60 – 120 MB | 45 – 90 MB |
| **Telemetry Privacy** | **100% Offline (0 Internet Needed)** | Often phones home | Varies | Varies |
| **Binary Architecture** | **~221 KB (Single .exe)** | ~120 MB (Chromium + Node) | ~70 MB (Python + DLLs) | Multiple files / DLLs |
| **Kernel Query Duration** | **~1.5 µs (`GetIfTable2`)** | 20 – 50 ms | 5 – 15 ms | 2 – 8 ms |
| **Startup Time** | **< 35 ms** | 3000 – 5000 ms | 2000 – 4000 ms | 1500 – 3000 ms |
| **External Dependencies** | **0 (Pure Win32 / .NET 4.8)** | Node.js, Chromium, npm | Python runtime, PyQt/wx | Rainmeter engine, C++ DLLs |

---

## ⌨️ Controls & Shortcuts Cheat Sheet

| Action | Control / Gesture |
| :--- | :--- |
| **Direct Repositioning** | Left-click and drag the indicator anywhere along the taskbar |
| **Open Flyout (Graph & Speed Test)** | Click the meter (or right-click ➔ Quick Action **📈 Flyout**) |
| **Quick Action Bar** | Right-click meter ➔ Top 3-column bar (**📈 Flyout** / **⚙️ Settings** / **🔄 Updates**) |
| **Toggle Display-Only (Click-Through)** | Right-click meter ➔ **👁️ Display Only Mode** |
| **Unlock Display-Only Mode** | Global Hotkey: **`Ctrl + \``** *(customizable in Settings)* |
| **Detach / Dock Floating Widget** | Right-click meter ➔ **📌 Float Widget** / **Taskbar Dock** |
| **Lock / Unlock Position** | Right-click meter ➔ **🔒 Lock Position** |
| **Open Settings & Customization** | Right-click meter ➔ **⚙️ Settings** (or double-click tray icon) |

---

## 🚀 Quick Start

### Running metriX
1. Download the latest release: [**metriX-v4.1.1-windows-x64.zip**](https://github.com/SuryakiranMK/MetriX/releases/latest).
2. Extract and run `metriX.exe` (no installation required).
3. The speed indicator immediately attaches to your taskbar right next to the system tray.

### Settings & About
- Right-click the indicator and select **⚙️ Settings & Customization...**.
- Explore the **Position**, **Typography**, **Spacing**, **System**, and **About** pages.
- Visit the **About** page to review offline status, memory footprint, and check for updates.

---

## 🛠️ Building from Source

metriX builds in seconds with the included PowerShell script using the Windows built-in C# compiler (`csc.exe`):

```powershell
# Clone the repository
git clone https://github.com/SuryakiranMK/MetriX.git
cd MetriX

# Build the release binary
.\build.ps1
```

The resulting standalone executable is generated at `bin\Release\metriX.exe`.

Alternatively, build using MSBuild:
```powershell
msbuild metriX.csproj /p:Configuration=Release /p:Platform=x64
```

---

## ⚙️ Configuration Schema (`config.json`)

Settings are stored cleanly in `config.json` next to the executable:

```json
{
  "Position": "RightOfApps",
  "OffsetX": 0,
  "OffsetY": 0,
  "Layout": "Stacked",
  "FontFamily": "Segoe UI Variable Text",
  "FontSize": 9.0,
  "UnitFontSize": 7.5,
  "FontBold": false,
  "AutoTheme": true,
  "UploadColor": "#4CC2FF",
  "DownloadColor": "#6CCB5F",
  "LabelColor": "#D0D0D0",
  "ArrowStyle": "Triangle",
  "UnitMode": "AutoBytes",
  "DecimalPlaces": 1,
  "PaddingX": 8,
  "PaddingY": 2,
  "ItemSpacing": 4,
  "ShowHoverHighlight": true,
  "IdleDimming": true,
  "IdleThresholdKb": 1.0,
  "IdleOpacityPercent": 45,
  "OverallOpacityPercent": 100,
  "UpdateIntervalMs": 1000,
  "SelectedAdapterAlias": "",
  "StartWithWindows": true,
  "LeftClickAction": "None",
  "ShowOnAllMonitors": true,
  "MonthlyDataQuotaGb": 0.0,
  "EnableAutoUpdateCheck": true,
  "EnableSilentAutoUpdate": false,
  "IsFloatingMode": false,
  "FloatingX": -1,
  "FloatingY": -1,
  "FloatingAlwaysOnTop": true,
  "FloatingScale": 1.00,
  "FloatingLocked": false,
  "TopAppsLimit": 5,
  "SpeedTestDurationSeconds": 10,
  "DisplayOnlyMode": false,
  "DisplayOnlyHotkey": "Ctrl + `"
}
```

---

## 📄 License

Licensed under the [MIT License](LICENSE).  
Copyright © 2026 **Suryakiran MK**. Free to use, modify, and distribute.
