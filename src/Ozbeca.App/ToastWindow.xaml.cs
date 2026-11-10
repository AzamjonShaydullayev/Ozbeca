using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static Ozbeca.App.Native;

namespace Ozbeca.App;

/// <summary>Kursor yonidagi kichik xabar. Hech qachon fokusni olmaydi (WS_EX_NOACTIVATE).</summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private IntPtr _hwnd;

    public ToastWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => { _timer.Stop(); FadeTo(0, hideAfter: true); };
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        long ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE,
            new IntPtr(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_TOPMOST));
    }

    public void ShowMessage(string message)
    {
        MessageText.Text = message;
        _timer.Stop();
        if (!IsVisible) Show();
        UpdateLayout();
        PlaceNearCursor();
        FadeTo(1, hideAfter: false);
        _timer.Start();
    }

    private void PlaceNearCursor()
    {
        GetCursorPos(out var p);
        GetWindowRect(_hwnd, out var r);
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST), ref mi);
        var work = mi.rcWork;
        int x = Compat.Clamp(p.X + 16, work.Left, Math.Max(work.Left, work.Right - w));
        int y = p.Y + 20 + h <= work.Bottom ? p.Y + 20 : p.Y - h - 8;
        y = Compat.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - h));
        SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void FadeTo(double to, bool hideAfter)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(to > 0 ? 90 : 180));
        if (hideAfter) anim.Completed += (_, _) => { if (Opacity == 0) Hide(); };
        BeginAnimation(OpacityProperty, anim);
    }
}
