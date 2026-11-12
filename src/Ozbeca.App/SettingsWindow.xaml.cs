using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ozbeca.App;

public partial class SettingsWindow : Window
{
    private const string RepoUrl = "https://github.com/AzamjonShaydullayev/Ozbeca";
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)); // hero ustida xira qora

    private readonly App _app;
    private bool _capturing; // hotkey tanlanmoqda

    internal SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Theme.Apply(this);
        Theme.RoundClip(HeroClip, 16);

        EnabledToggle.IsChecked = app.Settings.Enabled;
        AutoTypeToggle.IsChecked = app.Settings.AutoType;
        ToastToggle.IsChecked = app.Settings.ShowToast;
        ApostropheToggle.IsChecked = app.Settings.RestoreApostrophes;
        AutostartToggle.IsChecked = Autostart.IsEnabled;
        PasteToggle.IsChecked = app.Settings.PasteConvert;
        AutoUpdateToggle.IsChecked = app.Settings.AutoUpdate;
        PauseToggle.IsChecked = app.Settings.PauseConvert;
        ReverseHint.Text = "Kirill matn ham o'giriladi.\nTeskari (ö → o'): " + string.Join("+", HotkeyService.Caps(app.Settings.ReverseHotkey));
        ShowExceptionsSummary();
        VersionText.Text = "Versiya " + Theme.Version;
        ShowAppsSummary();
        _ = AppCatalog.Refresh(); // ro'yxat oldindan yig'ilsin — "Tanlash" bosilganda darhol chiqsin
        ShowHotkey(app.Settings.Hotkey);
        UpdateStatus();

        // "−" bosilganda vazifalar paneliga emas, soat yonidagi Ö belgisiga yashirinadi (user so'rovi).
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Close(); };
    }

    internal void ShowStatus(string? message)
    {
        HotkeyStatusBox.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
        HotkeyStatus.Text = message ?? "";
        HotkeyStatusBox.Background = message == App.AltGrWarning ? WarningBrush : (Brush)FindResource("Danger");
    }

    private void ShowHotkey(string combo) => HotkeyCaps.ItemsSource = HotkeyService.Caps(combo);

    private void UpdateStatus()
    {
        bool on = EnabledToggle.IsChecked == true;
        StatusText.Text = on ? "Özbeça yoqilgan" : "Özbeça o'chirilgan";
        StatusDot.Fill = on ? (Brush)FindResource("Good") : (Brush)StatusDot.FindResource("ToggleOff");
        AutoTypeToggle.IsEnabled = on;
        PasteToggle.IsEnabled = PauseToggle.IsEnabled = on && AutoTypeToggle.IsChecked == true;
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

    private void AutoTypeToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.SetAutoType(AutoTypeToggle.IsChecked == true);
        UpdateStatus();
    }

    private void AutoUpdateToggle_Click(object sender, RoutedEventArgs e) => _app.SetAutoUpdate(AutoUpdateToggle.IsChecked == true);
    private void PasteToggle_Click(object sender, RoutedEventArgs e) => _app.SetPasteConvert(PasteToggle.IsChecked == true);
    private void PauseToggle_Click(object sender, RoutedEventArgs e) => _app.SetPauseConvert(PauseToggle.IsChecked == true);

    private void EditExceptions_Click(object sender, RoutedEventArgs e)
    {
        var w = new ExceptionsWindow(_app.Settings.Exceptions) { Owner = this };
        if (w.ShowDialog() != true || w.Result == null) return;
        _app.SetExceptions(w.Result);
        ShowExceptionsSummary();
    }

    private void ShowExceptionsSummary()
    {
        var list = _app.Settings.Exceptions;
        ExceptionsSummary.Text = list.Count == 0 ? "Ismlar, brendlar, loginlar — hech qachon o'zgarmaydi"
            : string.Join(", ", list.Take(4)) + (list.Count > 4 ? $" va yana {list.Count - 4} ta" : "");
    }

    private void ApostropheToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.Settings.RestoreApostrophes = ApostropheToggle.IsChecked == true;
        Ozbeca.Core.Transliterator.RestoreApostrophes = _app.Settings.RestoreApostrophes;
        _app.Settings.Save();
    }

    private void ToastToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.Settings.ShowToast = ToastToggle.IsChecked == true;
        _app.Settings.Save();
    }

    private void AutostartToggle_Click(object sender, RoutedEventArgs e) => Autostart.Set(AutostartToggle.IsChecked == true);

    private void ChooseApps_Click(object sender, RoutedEventArgs e)
    {
        new AppPickerWindow(_app) { Owner = this }.ShowDialog();
        ShowAppsSummary();
    }

    /// <summary>"Hamma dasturlarda" yoki "Telegram, Word va yana 2 ta".</summary>
    private void ShowAppsSummary()
    {
        var apps = _app.Settings.AutoTypeApps;
        AppsSummary.Text = apps.Count == 0 ? "Hamma dasturlarda ishlaydi"
            : string.Join(", ", apps.Take(2).Select(a => a.Name)) + (apps.Count > 2 ? $" va yana {apps.Count - 2} ta" : "");
    }

    private void Guide_Click(object sender, RoutedEventArgs e) => Open(RepoUrl + "#readme");
    private void Github_Click(object sender, RoutedEventArgs e) => Open(RepoUrl);

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Havola ochilmadi: " + ex.Message); }
    }
}
