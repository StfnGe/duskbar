using static TaskbarDimmer.Native;

namespace TaskbarDimmer;

/// <summary>
/// Black, click-through, topmost window laid over a single taskbar.
/// </summary>
internal sealed class OverlayForm : Form
{
    public IntPtr Taskbar { get; }
    public double Alpha { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DateTime LastActive { get; set; } = DateTime.Now;

    private RECT _lastRect;
    private DateTime _lastPlaced;

    public OverlayForm(IntPtr taskbar)
    {
        Taskbar = taskbar;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Bounds = new Rectangle(-32000, -32000, 1, 1);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetLayeredWindowAttributes(Handle, 0, 0, LWA_ALPHA);
    }

    public void SetAlpha(double value)
    {
        value = Math.Clamp(value, 0, 1);
        if (Math.Abs(value - Alpha) < 0.001)
            return;
        Alpha = value;
        SetLayeredWindowAttributes(Handle, 0, (byte)Math.Round(value * 255), LWA_ALPHA);
    }

    /// <summary>
    /// Moves the overlay over the taskbar and re-asserts topmost order, because the
    /// taskbar is topmost as well and jumps above us whenever it gets activated.
    /// </summary>
    public void PlaceOver(RECT r)
    {
        var now = DateTime.Now;
        bool moved = r.Left != _lastRect.Left || r.Top != _lastRect.Top || r.Width != _lastRect.Width || r.Height != _lastRect.Height;
        if (!moved && Visible && (now - _lastPlaced).TotalMilliseconds < 500)
            return;

        _lastRect = r;
        _lastPlaced = now;
        if (!Visible)
            Show();
        SetWindowPos(Handle, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public void Conceal()
    {
        SetAlpha(0);
        if (Visible)
            Hide();
    }
}
