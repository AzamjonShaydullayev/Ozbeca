using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ozbeca.App;

public partial class SettingsWindow : Window
{
    private const string RepoUrl = "https://github.com/AzamjonShaydullayev/Ozbeca";
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));

    private readonly App _app;
    private bool _capturing; // hotkey tanlanmoqda

    internal SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Theme.Apply(this);

        EnabledToggle.IsChecked = app.Settings.Enabled;
        AutoTypeToggle.IsChecked = app.Settings.AutoType;
        ToastToggle.IsChecked = app.Settings.ShowToast;
        AutostartToggle.IsChecked = Autostart.IsEnabled;
        DelayBox.Text = app.Settings.RestoreDelayMs.ToString();
        VersionText.Text = "Versiya " + Theme.Version;
        ShowHotkey(app.Settings.Hotkey);
        UpdateStatus();

        // "−" bosilganda vazifalar paneliga emas, soat yonidagi Ö belgisiga yashirinadi (user so'rovi).
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Close(); };
    }

    internal void ShowStatus(string? message)
    {
        HotkeyStatus.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
        HotkeyStatus.Text = message ?? "";
        HotkeyStatus.Foreground = message == App.AltGrWarning ? (Brush)FindResource("Muted") : ErrorBrush;
    }

    private void ShowHotkey(string combo) =>
        HotkeyCaps.ItemsSource = combo.Split('+').Select(k => k.Trim() == "Space" ? "Probel" : k.Trim()).ToArray();

    private void UpdateStatus()
    {
        bool on = EnabledToggle.IsChecked == true;
        StatusText.Text = on ? "Yoqilgan" : "O'chirilgan";
        StatusDot.Fill = on ? (Brush)FindResource("Good") : (Brush)FindResource("ToggleOff");
        AutoTypeToggle.IsEnabled = on;
    }

    private void EnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus(_app.SetEnabled(EnabledToggle.IsChecked == true));
        UpdateStatus();
    }

    // ---- Hotkey tanlash ----

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing) { EndCapture(); return; }
        _capturing = true;
        _app.SuspendHotkey(); // aks holda joriy kombinatsiyani bosish WM_HOTKEY bo'lib ketadi
        ChangeHotkeyButton.Content = "Bekor qilish";
        HotkeyHint.Text = "Yangi kombinatsiyani bosing, masalan Ctrl + Alt + O.";
        HotkeyCaps.ItemsSource = new[] { "…" };
        ShowStatus(null);
    }

    private void EndCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        ChangeHotkeyButton.Content = "O'zgartirish";
        HotkeyHint.Text = "Matnni belgilang va shu tugmalarni bosing.";
        ShowHotkey(_app.Settings.Hotkey);
        string? error = _app.ResumeHotkey();
        if (error != null) ShowStatus(error);
    }

    private void Window_Deactivated(object sender, EventArgs e) => EndCapture();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { EndCapture(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.ImeProcessed or Key.DeadCharProcessed)
            return; // faqat modifier — kombinatsiya hali tugamagan

        var mods = Keyboard.Modifiers;
        bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
        if (mods == ModifierKeys.None && !win)
        {
            ShowStatus("Kamida bitta qo'shimcha tugma kerak: Ctrl, Alt, Shift yoki Win.");
            return;
        }

        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (win) parts.Add("Win");
        parts.Add(key.ToString());
        string combo = string.Join("+", parts);

        string? error = _app.ChangeHotkey(combo);
        if (error != null && error != App.AltGrWarning)
        {
            ShowStatus(error); // band — boshqasini bosib ko'rsin, tanlash davom etadi
            _app.SuspendHotkey();
            return;
        }
        _capturing = false;
        ChangeHotkeyButton.Content = "O'zgartirish";
        HotkeyHint.Text = "Matnni belgilang va shu tugmalarni bosing.";
        ShowHotkey(combo);
        ShowStatus(error); // AltGr ogohlantirishi yoki null
    }

    // ---- Qolgan sozlamalar ----

    private void AutoTypeToggle_Click(object sender, RoutedEventArgs e) => _app.SetAutoType(AutoTypeToggle.IsChecked == true);

    private void ToastToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.Settings.ShowToast = ToastToggle.IsChecked == true;
        _app.Settings.Save();
    }

    private void AutostartToggle_Click(object sender, RoutedEventArgs e) => Autostart.Set(AutostartToggle.IsChecked == true);

    private void DelayBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (int.TryParse(DelayBox.Text, out int ms))
        {
            _app.Settings.RestoreDelayMs = Compat.Clamp(ms, 0, 5000);
            _app.ApplyDelay();
            _app.Settings.Save();
        }
        DelayBox.Text = _app.Settings.RestoreDelayMs.ToString();
    }

    private void Guide_Click(object sender, RoutedEventArgs e) => Open(RepoUrl + "#readme");
    private void Github_Click(object sender, RoutedEventArgs e) => Open(RepoUrl);

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Havola ochilmadi: " + ex.Message); }
    }
}
