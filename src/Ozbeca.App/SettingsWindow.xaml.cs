using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Ozbeca.App;

public partial class SettingsWindow : Window
{
    private readonly App _app;

    internal SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        ApplyTheme();
        HotkeyBox.Text = app.Settings.Hotkey;
        ToastBox.IsChecked = app.Settings.ShowToast;
        AutostartBox.IsChecked = Autostart.IsEnabled;
        DelayBox.Text = app.Settings.RestoreDelayMs.ToString();
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
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
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

    private void ApplyTheme()
    {
        bool dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is int light && light == 0;
        void Set(string k, uint argb) => Resources[k] = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        Set("Bg", dark ? 0xFF202020 : 0xFFF9F9F9);
        Set("Fg", dark ? 0xFFF2F2F2 : 0xFF1B1B1B);
        Set("Muted", dark ? 0xFFA0A0A0 : 0xFF5F5F5F);
        Set("Field", dark ? 0xFF2D2D2D : 0xFFFFFFFF);
        Set("Line", dark ? 0xFF3D3D3D : 0xFFD0D0D0);

        if (dark)
            SourceInitialized += (_, _) =>
            {
                int on = 1; // DWMWA_USE_IMMERSIVE_DARK_MODE — qorong'i sarlavha
                Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref on, sizeof(int));
            };
    }
}
