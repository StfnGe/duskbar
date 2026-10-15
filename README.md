<div align="center">
  <img src="assets/icon.png" width="128" alt="Taskbar Dimmer Icon">

  # Taskbar Dimmer

  **Schützt OLED-Monitore vor eingebrannter Taskleiste, ohne sie auszublenden.**
</div>

---

Die Windows-Taskleiste ist eines der hartnäckigsten statischen Bildelemente und damit ein typischer
Kandidat für Burn-in auf OLED-Monitoren. Die automatische Ausblendung von Windows hilft zwar, ist im Alltag
aber oft lästig.

**Taskbar Dimmer** legt stattdessen eine schwarze, halbtransparente und klick-durchlässige Fläche über die
Taskleiste, sobald sie nicht benutzt wird. Fährst du mit der Maus hin, drückst die Win-Taste oder öffnest das
Startmenü, ist sie sofort wieder voll da.

## Features

- 🌙 **Automatisches Abdunkeln** nach einstellbarer Inaktivitätszeit (2–60 s)
- 🔒 **Modus „Dauerhaft gedimmt“**: Taskleiste bleibt immer abgedunkelt, auch unter der Maus
- ⚡ **Sofortiges Einblenden** bei Mauskontakt, Win-Taste, Startmenü, Suche, Infobereich oder Fenstervorschau
- 🖱️ **Klick-durchlässig**: Die Taskleiste bleibt auch im gedimmten Zustand voll bedienbar
- 🖥️ **Multi-Monitor**: Jede Taskleiste wird separat behandelt
- 🎬 **Vollbild-Erkennung**: Bei Videos und Spielen wird nichts überlagert
- 🎚️ **Stufen**: 50 %, 70 %, 85 %, 95 % oder 100 % Abdunkelung
- 🚀 **Autostart** mit Windows per Klick
- 🌐 **Englisch & Deutsch**: Standard ist Englisch, umschaltbar im Tray-Menü
- 🪶 Kein Installer, keine Admin-Rechte, nur eine Tray-App

## Bedienung

Nach dem Start erscheint das Symbol im Infobereich der Taskleiste.

| Menüeintrag (EN / DE) | Wirkung |
|---|---|
| **Doppelklick** auf das Symbol | Dimmer ein-/ausschalten |
| **Enabled** / *Aktiv* | Dimmer ein-/ausschalten |
| **Mode** / *Modus* | *Dim when idle / Bei Inaktivität dimmen* oder *Always dimmed / Dauerhaft gedimmt* |
| **Dim level** / *Abdunkelung* | Stärke der Abdunkelung (50–100 %) |
| **Delay** / *Verzögerung* | Wartezeit bis zum Abdunkeln (nur im Inaktivitäts-Modus) |
| **Start with Windows** / *Mit Windows starten* | Autostart an/aus (`HKCU\...\Run`) |
| **Language** / *Sprache* | English oder Deutsch (Standard: English) |
| **Exit** / *Beenden* | App schließen |

Die Einstellungen werden in `%APPDATA%\TaskbarDimmer\settings.json` gespeichert. Dort lassen sich zusätzlich
die Fade-Zeiten (`FadeOutMs`, `FadeInMs`) anpassen.

> **Tipp:** Bei 85–100 % sind Uhr und Benachrichtigungs-Badges nur sichtbar, wenn du die Taskleiste berührst.
> 50–70 % ist ein guter Kompromiss zwischen Lesbarkeit und Schutz.

## Installation

### Voraussetzungen

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) zum Bauen (bzw. .NET 10 Desktop Runtime zum Ausführen)

### Bauen & starten

```powershell
git clone https://gitlab.com/stfnge/taskbar-dimmer.git
cd taskbar-dimmer
dotnet build -c Release
.\bin\Release\net10.0-windows\TaskbarDimmer.exe
```

### Einzelne EXE erzeugen (ohne installierte Runtime)

```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Die fertige `publish\TaskbarDimmer.exe` kann an einen beliebigen Ort kopiert werden. Anschließend einmal starten
und im Tray-Menü **Mit Windows starten** aktivieren.

## Funktionsweise

```
┌──────────────────────────── Bildschirm ────────────────────────────┐
│                                                                    │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ ███████████  Overlay (schwarz, Alpha 0–100 %, klick-durchlässig)   │ ← TaskbarDimmer
│ ⊞ 🔍 📁 🌐 …                                         🔊 📶 12:34   │ ← Windows-Taskleiste
└────────────────────────────────────────────────────────────────────┘
```

- Pro Taskleiste (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`) wird ein randloses Fenster mit
  `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` erzeugt.
  Es lässt alle Mausklicks durch, taucht nicht in Alt+Tab auf und stiehlt nie den Fokus.
- Ein Timer (30 ms) prüft Mausposition, Win-Taste und Vordergrundfenster und blendet die Transparenz
  weich zwischen 0 und der gewählten Stufe über.
- Ist das Vordergrundfenster vollbildig auf dem Monitor einer Taskleiste, wird das Overlay dort ausgeblendet.

## Projektstruktur

```
├── assets/
│   ├── app.ico          # App-/Tray-Icon (16–256 px)
│   └── icon.png         # Vorschau für diese README
├── Program.cs           # Einstiegspunkt, Single-Instance-Mutex
├── TrayApp.cs           # Tray-Menü, Erkennung von Aktivität & Vollbild, Fade-Logik
├── OverlayForm.cs       # Das abdunkelnde Overlay-Fenster
├── Settings.cs          # Einstellungen (JSON in %APPDATA%)
├── Strings.cs           # UI-Texte Englisch/Deutsch
├── Native.cs            # Win32-P/Invoke
└── TaskbarDimmer.csproj
```

## Weitere Tipps gegen OLED-Burn-in

- Dunkles Windows-Design und eine nicht transparente, dunkle Taskleiste verwenden
- Unnötige Taskleisten-Elemente (Widgets, Suchfeld, Task-View) ausblenden
- Pflegefunktionen des Monitors aktiv lassen (Pixel Shift, Pixel Refresh, Logo-/Taskleisten-Erkennung)
- Monitor bei Inaktivität ausschalten lassen statt Bildschirmschoner
