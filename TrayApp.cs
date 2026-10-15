using System.Diagnostics;
using Microsoft.Win32;
using static TaskbarDimmer.Native;

namespace TaskbarDimmer;

/// <summary>
/// Tray application that dims every taskbar after a period of inactivity and
/// reveals it instantly when the mouse touches it or the shell UI is in use.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private const int TickMs = 30;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TaskbarDimmer";

    // Foreground windows that mean "the user is interacting with the taskbar / shell".
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland", "TaskListThumbnailWnd", "XamlExplorerHostIslandWindow",
    };

    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "StartMenuExperienceHost", "SearchHost", "SearchApp", "ShellExperienceHost", "ShellHost",
    };

    private static readonly HashSet<string> DesktopClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW",
    };

    private readonly Settings _settings = Settings.Load();
    private readonly Dictionary<IntPtr, OverlayForm> _overlays = new();
    private readonly Dictionary<uint, string> _processNames = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = TickMs };
    private readonly NotifyIcon _tray;
    private readonly Icon _icon;
    private DateTime _lastScan = DateTime.MinValue;
    private DateTime _lastTick = DateTime.Now;

    public TrayApp()
    {
        Strings.Language = _settings.Language;
        _icon = CreateIcon();
        _tray = new NotifyIcon { Icon = _icon, Visible = true };
        _tray.DoubleClick += (_, _) => SetEnabled(!_settings.Enabled);
        ApplyLanguage();

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var enabled = new ToolStripMenuItem(Strings.T("Active"));
        enabled.Click += (_, _) => SetEnabled(!_settings.Enabled);

        var mode = new ToolStripMenuItem(Strings.T("Mode"));
        var modeIdle = new ToolStripMenuItem(Strings.T("ModeIdle"));
        modeIdle.Click += (_, _) => { _settings.AlwaysDim = false; _settings.Save(); };
        var modeAlways = new ToolStripMenuItem(Strings.T("ModeAlways"));
        modeAlways.Click += (_, _) => { _settings.AlwaysDim = true; _settings.Save(); };
        mode.DropDownItems.AddRange(new ToolStripItem[] { modeIdle, modeAlways });

        var level = new ToolStripMenuItem(Strings.T("DimLevel"));
        foreach (int pct in new[] { 50, 70, 85, 95, 100 })
        {
            var item = new ToolStripMenuItem($"{pct} %") { Tag = pct / 100.0 };
            item.Click += (_, _) => { _settings.DimLevel = (double)item.Tag!; _settings.Save(); };
            level.DropDownItems.Add(item);
        }

        var delay = new ToolStripMenuItem(Strings.T("Delay"));
        foreach (int s in new[] { 2, 5, 10, 30, 60 })
        {
            var item = new ToolStripMenuItem(Strings.T("Seconds", s)) { Tag = s };
            item.Click += (_, _) => { _settings.DelaySeconds = (int)item.Tag!; _settings.Save(); };
            delay.DropDownItems.Add(item);
        }

        var autostart = new ToolStripMenuItem(Strings.T("Autostart"));
        autostart.Click += (_, _) => SetAutostart(!IsAutostart());

        // Language names are shown in their own language so they are always recognizable.
        var language = new ToolStripMenuItem(Strings.T("Language"));
        foreach (var (code, name) in new[] { (Strings.English, "English"), (Strings.German, "Deutsch") })
        {
            var item = new ToolStripMenuItem(name) { Tag = code, Checked = code == _settings.Language };
            item.Click += (_, _) => SetLanguage((string)item.Tag!);
            language.DropDownItems.Add(item);
        }

        var exit = new ToolStripMenuItem(Strings.T("Exit"));
        exit.Click += (_, _) => ExitThread();

        menu.Items.AddRange(new ToolStripItem[] { enabled, mode, level, delay, new ToolStripSeparator(), autostart, language, new ToolStripSeparator(), exit });

        menu.Opening += (_, _) =>
        {
            enabled.Checked = _settings.Enabled;
            autostart.Checked = IsAutostart();
            modeIdle.Checked = !_settings.AlwaysDim;
            modeAlways.Checked = _settings.AlwaysDim;
            delay.Enabled = !_settings.AlwaysDim;
            foreach (ToolStripMenuItem i in level.DropDownItems)
                i.Checked = Math.Abs((double)i.Tag! - _settings.DimLevel) < 0.001;
            foreach (ToolStripMenuItem i in delay.DropDownItems)
                i.Checked = (int)i.Tag! == _settings.DelaySeconds;
        };

        return menu;
    }

    private void SetEnabled(bool value)
    {
        _settings.Enabled = value;
        _settings.Save();
        UpdateTooltip();
    }

    private void SetLanguage(string language)
    {
        if (language == _settings.Language)
            return;
        _settings.Language = language;
        _settings.Save();
        Strings.Language = language;
        ApplyLanguage();
    }

    /// <summary>Rebuilds the context menu and tooltip in the current language.</summary>
    private void ApplyLanguage()
    {
        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildMenu();
        // The old menu may still be closing when a language item was clicked.
        if (old is { IsHandleCreated: true })
            old.BeginInvoke(old.Dispose);
        else
            old?.Dispose();
        UpdateTooltip();
    }

    private void UpdateTooltip() =>
        _tray.Text = Strings.T(_settings.Enabled ? "Tooltip" : "TooltipPaused");

    private void Tick()
    {
        var now = DateTime.Now;
        double dt = Math.Max(1, (now - _lastTick).TotalMilliseconds);
        _lastTick = now;

        if ((now - _lastScan).TotalSeconds >= 2)
            ScanTaskbars(now);

        GetCursorPos(out var cursor);
        var fg = GetForegroundWindow();
        bool shellActive = IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN) || IsShellWindow(fg);
        var fullscreenMonitor = GetFullscreenMonitor(fg);

        foreach (var overlay in _overlays.Values)
        {
            var tb = overlay.Taskbar;
            if (!IsWindowVisible(tb) || !GetWindowRect(tb, out var rect) || rect.Width <= 0 || rect.Height <= 0
                || (fullscreenMonitor != IntPtr.Zero && GetMonitor(tb) == fullscreenMonitor))
            {
                // Taskbar hidden or a fullscreen app (video, game) owns this monitor.
                overlay.Conceal();
                overlay.LastActive = now;
                continue;
            }

            bool hot = !_settings.Enabled
                || (!_settings.AlwaysDim && (shellActive || rect.Contains(cursor, margin: 1)));
            if (hot)
                overlay.LastActive = now;

            bool dim = !hot && (_settings.AlwaysDim || (now - overlay.LastActive).TotalSeconds >= _settings.DelaySeconds);
            double target = dim ? _settings.DimLevel : 0;

            double alpha = overlay.Alpha;
            if (alpha < target)
                alpha = Math.Min(target, alpha + dt / Math.Max(1, _settings.FadeOutMs) * _settings.DimLevel);
            else if (alpha > target)
                alpha = Math.Max(target, alpha - dt / Math.Max(1, _settings.FadeInMs));

            if (alpha <= 0)
            {
                overlay.Conceal();
                continue;
            }

            overlay.PlaceOver(rect);
            overlay.SetAlpha(alpha);
        }
    }

    /// <summary>Finds the primary and all secondary taskbars and keeps one overlay per taskbar.</summary>
    private void ScanTaskbars(DateTime now)
    {
        _lastScan = now;
        _processNames.Clear();

        var found = new HashSet<IntPtr>();
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
            found.Add(primary);
        EnumWindows((hWnd, _) =>
        {
            if (GetClassName(hWnd) == "Shell_SecondaryTrayWnd")
                found.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        foreach (var stale in _overlays.Keys.Where(h => !found.Contains(h) || !IsWindow(h)).ToList())
        {
            _overlays[stale].Dispose();
            _overlays.Remove(stale);
        }

        foreach (var tb in found.Where(h => !_overlays.ContainsKey(h)))
        {
            var overlay = new OverlayForm(tb);
            _ = overlay.Handle; // create the window so alpha can be set before it is shown
            _overlays[tb] = overlay;
        }
    }

    private bool IsShellWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;
        if (ShellClasses.Contains(GetClassName(hWnd)))
            return true;
        return ShellProcesses.Contains(GetProcessName(hWnd));
    }

    /// <summary>Returns the monitor occupied by a fullscreen foreground window, or zero.</summary>
    private IntPtr GetFullscreenMonitor(IntPtr fg)
    {
        if (fg == IntPtr.Zero || DesktopClasses.Contains(GetClassName(fg)) || IsShellWindow(fg))
            return IntPtr.Zero;
        if (!GetWindowRect(fg, out var w))
            return IntPtr.Zero;

        var monitor = GetMonitor(fg);
        if (GetMonitorRect(monitor) is not { } m)
            return IntPtr.Zero;

        bool covers = w.Left <= m.Left && w.Top <= m.Top && w.Right >= m.Right && w.Bottom >= m.Bottom;
        return covers ? monitor : IntPtr.Zero;
    }

    private string GetProcessName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (_processNames.TryGetValue(pid, out var name))
            return name;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            name = p.ProcessName;
        }
        catch
        {
            name = "";
        }
        _processNames[pid] = name;
        return name;
    }

    private static bool IsAutostart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is string;
    }

    private static void SetAutostart(bool value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (value)
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    /// <summary>Loads the embedded app icon in the size the tray expects at the current DPI.</summary>
    private static Icon CreateIcon()
    {
        using var stream = typeof(TrayApp).Assembly.GetManifestResourceStream("TaskbarDimmer.app.ico")!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        foreach (var o in _overlays.Values)
            o.Dispose();
        _overlays.Clear();
        _tray.Visible = false;
        _tray.Dispose();
        _icon.Dispose();
        base.ExitThreadCore();
    }
}
