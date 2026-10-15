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
        _icon = CreateIcon();
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "Taskbar Dimmer",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => SetEnabled(!_settings.Enabled);

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var enabled = new ToolStripMenuItem("Aktiv") { CheckOnClick = false };
        enabled.Click += (_, _) => SetEnabled(!_settings.Enabled);

        var mode = new ToolStripMenuItem("Modus");
        var modeIdle = new ToolStripMenuItem("Bei Inaktivität dimmen");
        modeIdle.Click += (_, _) => { _settings.AlwaysDim = false; _settings.Save(); };
        var modeAlways = new ToolStripMenuItem("Dauerhaft gedimmt");
        modeAlways.Click += (_, _) => { _settings.AlwaysDim = true; _settings.Save(); };
        mode.DropDownItems.AddRange(new ToolStripItem[] { modeIdle, modeAlways });

        var level = new ToolStripMenuItem("Abdunkelung");
        foreach (int pct in new[] { 50, 70, 85, 95, 100 })
        {
            var item = new ToolStripMenuItem($"{pct} %") { Tag = pct / 100.0 };
            item.Click += (_, _) => { _settings.DimLevel = (double)item.Tag!; _settings.Save(); };
            level.DropDownItems.Add(item);
        }

        var delay = new ToolStripMenuItem("Verzögerung");
        foreach (int s in new[] { 2, 5, 10, 30, 60 })
        {
            var item = new ToolStripMenuItem($"{s} Sekunden") { Tag = s };
            item.Click += (_, _) => { _settings.DelaySeconds = (int)item.Tag!; _settings.Save(); };
            delay.DropDownItems.Add(item);
        }

        var autostart = new ToolStripMenuItem("Mit Windows starten");
        autostart.Click += (_, _) => SetAutostart(!IsAutostart());

        var exit = new ToolStripMenuItem("Beenden");
        exit.Click += (_, _) => ExitThread();

        menu.Items.AddRange(new ToolStripItem[] { enabled, mode, level, delay, new ToolStripSeparator(), autostart, new ToolStripSeparator(), exit });

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
        _tray.Text = value ? "Taskbar Dimmer" : "Taskbar Dimmer (pausiert)";
    }

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
