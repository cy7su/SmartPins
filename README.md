<div align="center">

<img src="SmartPins/icon.ico" width="90" height="90" alt="SmartPins"/>

# SmartPins

**Keep any window on top — always.**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?style=flat-square&logo=windows)](https://github.com/switchxc/SmartPins/releases)
[![Framework](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Release](https://img.shields.io/github/v/release/switchxc/SmartPins?style=flat-square&color=39FF14)](https://github.com/switchxc/SmartPins/releases)
[![License](https://img.shields.io/badge/license-MIT-555?style=flat-square)](LICENSE)

</div>


## Features

| | |
|---|---|
| **Always-on-top toggle** | Pin any window with a hotkey, button or cursor click |
| **Cursor pin mode** | Click anywhere on a window to pin / unpin it |
| **Glow border** | Optional neon border on pinned windows |
| **Pin icon overlay** | Small pin icon in the corner of pinned windows |
| **Blacklist** | Hide apps from the list by process name (substring match) |
| **Custom hotkey** | Rebind the global shortcut from settings |
| **System tray** | Runs silently in the tray, double-click to show |

---

## Quick start

1. Download the latest `SmartPins.exe` from [Releases](https://github.com/switchxc/SmartPins/releases)
2. Run it — the app appears in the system tray
3. Press `Ctrl+Alt+P` to pin the active window

> Right-click the tray icon to access all actions.
> Middle-click to enter cursor pin mode.

---

## Build from source

```bash
git clone https://github.com/cy7su/SmartPins.git
cd SmartPins
dotnet build
dotnet run --project SmartPins
```

**Requirements:** .NET 8 SDK, Windows 10/11

---

## Usage

### Hotkey
Press `Ctrl+Alt+P` (or your custom hotkey) while a window is focused — it gets pinned on top.
Press again to unpin.

### Cursor mode
Click **"Pin with cursor"** or middle-click the tray icon.
Your cursor changes to a pin — click any window to toggle its pin state.

### Blacklist
Open **Settings → blacklist**.
Type a process name fragment (e.g. `discord`) and click `[ add ]`.
Or right-click any window in the list → **Add to blacklist**.

---

## Stack

- WPF / .NET 8
- [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon)
- Fira Code

---

<div align="center">
<sub>Built for people who hate alt-tabbing.</sub>
</div>
