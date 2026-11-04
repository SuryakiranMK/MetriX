# metriX — Release Roadmap & Schedule ⚡

> **Core Philosophy**: metriX must remain the **lightest**, **fastest**, and **most dependable** network speed indicator for Windows.
> - **Zero bloat**: Built-in Windows components only (Win32 / GDI+ / Direct OS Kernel APIs).
> - **Zero background drag**: < 0.01% CPU, ~6–8 MB RAM, zero battery drain.
> - **Clean codebase**: No dead code, no redundant libraries, strict resource management.

---

## 📌 Development & Release Protocol

1. **Schedule-First**: Any bug, UI problem, or feature request must first be cataloged here with an appropriate version number and slot before any code is modified.
2. **Sequential Execution**: Code changes and GitHub releases are performed strictly in the table's defined order.
3. **Dynamic Priority & Cascading**: When a high-priority hotfix is introduced, it is inserted at the active slot and subsequent version numbers are dynamically cascaded.
4. **SemVer Convention**:
   - `+0.0.1` (Patch): Bug fixes, hotfixes, small performance or visual refinements.
   - `+0.1.0` (Minor): New feature modules (e.g. flyout popup, speed test engine, major UI overhauls).
   - `+1.0.0` (Major): Architectural milestones (multi-monitor mirroring, per-app tracking).

---

## 📊 Master Release Tracking Table

| Version | Increment | Status | Target / Release Date | Type | Highlights & Key Deliverables |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`v1.0.0`** | Initial Release | ✅ **Released** | 2026-11-04 | Core | Initial standalone Windows 11 taskbar speed indicator (~55 KB, <0.01% CPU) |
| **`v1.0.1`** | `+0.0.1` (Patch) | ✅ **Released** | 2026-11-04 | Brand & Fix | **Official Fluent App Icon (`app.ico`)** embedded + Anti-Flicker / TopMost stability fix |
| **`v1.0.2`** | `+0.0.1` (Patch) | 🚀 **Next Up** | Upcoming | Customization | Extended styling: dynamic Windows Dark/Light theme matcher, idle dimming, unit typography |
| **`v1.1.0`** | `+0.1.0` (Minor) | ⏳ **Scheduled** | Upcoming | UI / Feature | **Taskbar-Anchored Flyout Popup** right above meter (live graph, session stats) & Fluent Cards redesign |
| **`v1.2.0`** | `+0.1.0` (Minor) | ⏳ **Scheduled** | Upcoming | Feature | **Built-in Network Speed Test Engine** (latency, download, upload) with animated gauge & history |
| **`v2.0.0`** | `+1.0.0` (Major) | ⏳ **Scheduled** | Future | Major | **Multi-Monitor Secondary Taskbars** & per-app real-time bandwidth attribution |

---

## 📋 Release Details & Scope

### `v1.0.2` (Patch: +0.0.1) — *Extended Customization & Windows Theme Matcher*
- **Dynamic Theme Matching**: Auto-detect Windows Light/Dark theme (`AppsUseLightTheme`) and adjust text colors dynamically.
- **Idle Traffic Dimming**: Automatically dim font opacity when bandwidth is idle (< 1 KB/s) to maintain a minimal, non-distracting presence.
- **Independent Typography**: Configure font size and style separately for digits vs units.
- **Window Opacity Slider**: Adjustable background transparency from 50% to 100%.

### `v1.1.0` (Minor: +0.1.0) — *Taskbar-Anchored Flyout & Modern Fluent UI*
- **Anchored Mini Flyout (Click Pop-Up)**:
  - Dynamic positioning: Anchors **directly above the meter** regardless of position (left, right, center, or custom drag).
  - Light-dismiss: Closes seamlessly when clicking anywhere outside.
  - Live 30-second bandwidth sparkline graph.
  - Session data usage counters (Total Uploaded / Downloaded).
  - Active adapter hardware details (IP, link speed, connection type).
- **Settings UI Redesign**:
  - Full Windows 11 Fluent card redesign with modern toggle switches and sliders.

### `v1.2.0` (Minor: +0.1.0) — *Built-in Network Speed Test Engine*
- **Accurate Latency / Ping Test**: Multi-packet jitter and ping measurement.
- **Multi-Threaded Bandwidth Test**: Parallel chunk download and upload testing.
- **Animated Speedometer**: Visual test gauge integrated directly into the flyout.
- **Local Test History**: Timestamped test logs stored in lightweight `history.json`.

### `v2.0.0` (Major: +1.0.0) — *Multi-Monitor & App Bandwidth Attribution*
- **Multi-Monitor Taskbars**: Display indicators across all connected monitors.
- **Per-App Attribution**: Real-time process breakdown (e.g. Chrome, Steam, Discord).
