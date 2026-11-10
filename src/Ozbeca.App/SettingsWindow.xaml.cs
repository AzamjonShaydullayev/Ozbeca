using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ozbeca.App;

public partial class SettingsWindow : Window
{
    private readonly App _app;

    internal SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Theme.Apply(this);
        HotkeyBox.Text = app.Settings.Hotkey;
        ToastBox.IsChecked = app.Settings.ShowToast;
        AutostartBox.IsChecked = Autostart.IsEnabled;
        DelayBox.Text = app.Settings.RestoreDelayMs.ToString();

        // "−" bosilganda vazifalar paneliga emas, soat yonidagi Ö belgisiga yashirinadi (user so'rovi).
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Close(); };
    }

    internal void ShowStatus(string? error)
    {
        HotkeyStatus.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
        HotkeyStatus.Text = error ?? "";
        HotkeyStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
    }

    // Tanlash paytida joriy hotkey o'chiriladi, aks holda uni bosish WM_HOTKEY bo'lib ketadi.
    private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e) => _app.SuspendHotkey();
    private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => ShowStatus(_app.ResumeHotkey());

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift) return; // fokus keyingi elementga o'tsin
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.ImeProcessed or Key.DeadCharProcessed)
            return; // faqat modifier — kombinatsiya hali tugamagan

        var mods = Keyboard.Modifiers;
        bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
        if (mods == ModifierKeys.None && !win)
        {
            ShowStatus("Kamida bitta modifier kerak: Ctrl, Alt, Shift yoki Win.");
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
        if (error == null) HotkeyBox.Text = combo;
        else if (error == App.AltGrWarning) HotkeyBox.Text = combo; // saqlandi, faqat ogohlantirish
        ShowStatus(error);
        _app.SuspendHotkey(); // fokus hali maydonda
    }

    private void ToastBox_Click(object sender, RoutedEventArgs e)
    {
        _app.Settings.ShowToast = ToastBox.IsChecked == true;
        _app.Settings.Save();
    }

    private void AutostartBox_Click(object sender, RoutedEventArgs e) => Autostart.Set(AutostartBox.IsChecked == true);

    private void DelayBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (int.TryParse(DelayBox.Text, out int ms))
        {
            _app.Settings.RestoreDelayMs = Math.Clamp(ms, 0, 5000);
            _app.ApplyDelay();
            _app.Settings.Save();
        }
        DelayBox.Text = _app.Settings.RestoreDelayMs.ToString();
    }
}
