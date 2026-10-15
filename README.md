<div align="center">
  <img src="assets/icon.png" width="128" alt="Duskbar icon">

  # Duskbar

  **Protects OLED monitors from taskbar burn-in, without hiding the taskbar.**
</div>

---

The Windows taskbar is one of the most persistent static elements on screen and a classic cause of burn-in on
OLED monitors. Windows' auto-hide helps, but it gets in the way in everyday use.

**Duskbar** puts a black, semi-transparent, click-through overlay on top of the taskbar whenever you're not
using it. Move the mouse to it, press the Windows key or open the Start menu, and it's back at full brightness
instantly.

## Features

- 🌙 **Dims automatically** after a configurable idle time (2–60 s)
- 🔒 **"Always dimmed" mode**: the taskbar stays dimmed at all times, even under the mouse
- ⚡ **Instant reveal** on mouse contact, Windows key, Start menu, search, system tray or window previews
- 🖱️ **Click-through**: the taskbar stays fully usable while dimmed
- 🖥️ **Multi-monitor**: every taskbar is handled separately
- 🎬 **Fullscreen detection**: nothing is overlaid while videos or games run fullscreen
- 🎚️ **Dim levels**: 50 %, 70 %, 85 %, 95 % or 100 %
- 🚀 **Start with Windows** in one click
- 🌐 **English & German** UI (English by default, switchable in the tray menu)
- 🪶 No installer, no admin rights, just a tray app

## Usage

After launch, the Duskbar icon appears in the system tray. Right-click it for the menu:

| Menu item | What it does |
|---|---|
| **Double-click** the icon | Turn dimming on/off |
| **Enabled** | Turn dimming on/off |
| **Mode** | *Dim when idle* or *Always dimmed* |
| **Dim level** | How dark the taskbar gets (50–100 %) |
| **Delay** | Idle time before dimming (only in *Dim when idle* mode) |
| **Start with Windows** | Toggle autostart (`HKCU\...\Run`) |
| **Language** | English or Deutsch |
| **Exit** | Quit Duskbar |

Settings are stored in `%APPDATA%\Duskbar\settings.json`. The fade durations (`FadeOutMs`, `FadeInMs`) can be
tweaked there as well.

> **Tip:** At 85–100 % the clock and notification badges are only visible when you touch the taskbar.
> 50–70 % is a good trade-off between readability and protection.

## Download

Grab **`Duskbar.exe`** from the [latest release](https://github.com/StfnGe/duskbar/releases/latest).
It's a single file with everything included: no installer, no .NET runtime required.

1. Download `Duskbar.exe` and put it somewhere permanent (e.g. `%LOCALAPPDATA%\Duskbar`).
2. Run it. The icon appears in the system tray.
3. Right-click the icon → **Start with Windows** to launch it automatically.

> **Windows SmartScreen:** The EXE isn't code-signed, so Windows may show *"Windows protected your PC"*
> on first launch. Click **More info → Run anyway**.

Requires Windows 10 or 11 (x64).

## Building from source

### Requirements

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build & run

```powershell
git clone https://github.com/StfnGe/duskbar.git
cd duskbar
dotnet build -c Release
.\bin\Release\net10.0-windows\Duskbar.exe
```

### Single-file EXE (no runtime required)

```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

### Creating a release

Releases are built by GitHub Actions ([`.github/workflows/release.yml`](.github/workflows/release.yml)).
Push a version tag and the workflow builds `Duskbar.exe` and attaches it to a new GitHub release:

```powershell
git tag v1.0.0
git push github v1.0.0
```

## How it works

```
┌────────────────────────────── Screen ──────────────────────────────┐
│                                                                    │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ ███████████  Overlay (black, alpha 0–100 %, click-through)         │ ← Duskbar
│ ⊞ 🔍 📁 🌐 …                                         🔊 📶 12:34   │ ← Windows taskbar
└────────────────────────────────────────────────────────────────────┘
```

- For every taskbar (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`) Duskbar creates a borderless window with
  `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`.
  It passes all mouse clicks through, doesn't show up in Alt+Tab and never steals focus.
- A 30 ms timer checks the cursor position, the Windows key and the foreground window, and smoothly fades the
  overlay between 0 and the selected dim level.
- If the foreground window covers the whole monitor a taskbar is on, the overlay on that monitor is hidden.

## Project structure

```
├── .github/workflows/
│   └── release.yml      # Builds Duskbar.exe and publishes a GitHub release on version tags
├── assets/
│   ├── app.ico          # App and tray icon (16–256 px)
│   └── icon.png         # Preview for this README
├── Program.cs           # Entry point, single-instance mutex
├── TrayApp.cs           # Tray menu, activity & fullscreen detection, fade logic
├── OverlayForm.cs       # The dimming overlay window
├── Settings.cs          # Settings (JSON in %APPDATA%)
├── Strings.cs           # UI texts (English/German)
├── Native.cs            # Win32 P/Invoke
└── Duskbar.csproj
```

## More tips against OLED burn-in

- Use Windows dark mode and an opaque, dark taskbar
- Hide taskbar elements you don't need (Widgets, search box, Task View)
- Keep your monitor's panel care features enabled (pixel shift, pixel refresh, logo/taskbar detection)
- Let the monitor turn off when idle instead of using a screensaver

## Credits

Duskbar, including its code, icon and this README, was created with the help of
[Claude](https://claude.com/claude-code) by Anthropic.
