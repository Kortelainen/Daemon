# Daemon

A lightweight Windows desktop overlay that gives your machine a personality.
Ambient system stats, a reactive sprite companion, always on top — never in the way.

---

## What it does

Daemon sits transparently over your desktop and reacts to what your system is actually doing — app switches, CPU spikes, idle time, heat. It's not a widget panel. It's closer to a spirit that lives in the corner of your screen.

```
┌─────────────────────────────────────────────────────────────┐
│  Active App Name                                  Clock      │
│                                                              │
│                                                   Sprite     │
│                                                   + status   │
│                                                              │
│  Net ↑↓                                  CPU / GPU / RAM    │
└─────────────────────────────────────────────────────────────┘
```

All zones are sparse — anchored at screen edges, not filled.

---

## Features

**System metrics** — CPU, GPU, VRAM, RAM, Disk, Network. Each with a live sparkline and two-tone bar. App-level and system-level CPU/RAM shown side by side. Hardware temps via LibreHardwareMonitor.

**Sprite companion** — animated character that reacts to system state. Idle, sleep, wake, window events, CPU spikes, heat warnings. Priority-based state machine with randomised transitions.

**Status icons** — persistent corner indicators on the sprite for conditions that need attention (heat, load) without interrupting the animation.

**App name zone** — fades in the foreground window title on switch, holds, fades out.

**Clock** — minimal `HH:MM` with a glitch flicker on minute rollover.

**Accent color** — pick your color from the tray. Applies to all text, bars, and sparklines. Saved across restarts.

**Tray control** — pause, show/hide sprite, fullscreen-hide toggle, color theme. Settings persisted to `%APPDATA%\Daemon\settings.json`.

---

## Sprite states

| State | Trigger |
|-------|---------|
| Sleep | No input > 10 min |
| Idle / Curious | Default, mouse still > 30s |
| Wake Up | Input resumes after sleep |
| Window Switch / Open / Close | Foreground window events |
| CPU High | System CPU > 70% |
| App CPU High | Foreground app CPU > 50% |
| Heat Warn | Any sensor > 75°C |
| Heat Critical | Any sensor > 90°C |
| Smile / Stare | Randomised one-shots |

---

## Tech

- **C# / .NET 8 / WPF**
- **Rendering** — `CroppedBitmap` sprite frames, `FormattedText.BuildGeometry()` for outlined text, custom `FrameworkElement` controls for bars and sparklines
- **Win32 hooks** — `SetWinEventHook` for foreground, minimize, restore events; `GetLastInputInfo` for idle detection
- **Hardware sensors** — [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0)
- **Transparency** — `WS_EX_LAYERED | WS_EX_NOACTIVATE`, click-through

---

## Running from source

```bash
cd src/EbOverlay
dotnet run
```

Requires .NET 8 SDK. Windows only.

## Release build

```bash
dotnet restore -r win-x64
dotnet publish -p:PublishProfile=Release-x64
```

Produces a single self-contained `.exe` in `publish/` — no installer, no dependencies.

---

## Milestones

| # | Goal | Status |
|---|------|--------|
| M1 | Transparent click-through window, fullscreen detection | ✅ |
| M2 | App name hook + clock | ✅ |
| M3 | System, hardware, network metrics | ✅ |
| M4 | Sprite zone with animation state machine | ✅ |
| M5 | Behavioral rules engine wired to all triggers | ✅ |
| M6 | Scanline layer + tray icon asset | ⬜ |
| M7 | Window-aware sprite — walks active window edges | ⬜ |
| M8 | Keyboard control layer + zone config | ⬜ |
