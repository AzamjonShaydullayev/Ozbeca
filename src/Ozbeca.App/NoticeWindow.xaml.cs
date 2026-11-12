using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static Ozbeca.App.Native;

namespace Ozbeca.App;

/// <summary>
/// Ekranning pastki o'ng burchagida (soat ustida) chiqadigan bildirishnoma — tray balloon o'rniga.
/// Fokusni olmaydi; bosilsa — dastur oynasi ochiladi (X — faqat yopadi); sichqoncha ustida turganda yopilmaydi.
/// </summary>
public partial class NoticeWindow : Window
{
    private static readonly Brush WarningStripe = new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x24));
    private readonly DispatcherTimer _timer = new();

    /// <summary>Kartochka bosildi (X dan tashqari) — dastur oynasini ochish uchun.</summary>
    public event Action? Clicked;

    public NoticeWindow()
    {
        InitializeComponent();
        Theme.Apply(this, useMica: false);
        _timer.Tick += (_, _) => Dismiss();
        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST));
    }

    public void ShowNotice(string title, string message, bool warning = false, int seconds = 6)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        Stripe.Background = warning ? WarningStripe : (Brush)FindResource("AccentGradient");
        bool fresh = !IsVisible || Opacity < 0.5;
        if (!IsVisible) Show();
        UpdateLayout();
        // Asosiy monitor (tray o'sha yerda), fizik piksellarda: PerMonitorV2'da SystemParameters.WorkArea va Left/Top
        // boshqa masshtabli monitorda mos kelmaydi. Ikki marta — masshtab o'zgarsa, WPF oynani qayta o'lchaydi.
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY), ref mi);
        for (int pass = 0; pass < 2; pass++)
        {
            GetWindowRect(hwnd, out var r);
            SetWindowPos(hwnd, IntPtr.Zero, mi.rcWork.Right - (r.Right - r.Left), mi.rcWork.Bottom - (r.Bottom - r.Top), 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        if (fresh)
            CardMove.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        _timer.Interval = TimeSpan.FromSeconds(seconds);
        _timer.Stop();
        _timer.Start();
    }

    private void Dismiss()
    {
        _timer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) => { if (Opacity == 0) Hide(); };
        BeginAnimation(OpacityProperty, fade);
        CardMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(24, TimeSpan.FromMilliseconds(220)));
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e) => _timer.Stop();
    private void Window_MouseLeave(object sender, MouseEventArgs e) { if (Opacity > 0) _timer.Start(); }
    private void Window_Click(object sender, MouseButtonEventArgs e)
    {
        Dismiss();
        Clicked?.Invoke();
    }

    private void Close_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // kartochka bosilishiga o'tmasin
        Dismiss();
    }
}
