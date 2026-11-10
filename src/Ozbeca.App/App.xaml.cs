using System.Windows;
using WinForms = System.Windows.Forms;

namespace Ozbeca.App;

public partial class App : Application
{
    internal const string AltGrWarning =
        "Saqlandi. Diqqat: Ctrl+Alt+harf ba'zi klaviatura tartiblarida AltGr bo'lib, harf yozib yuborishi mumkin.";

    private Mutex? _mutex;
    private HotkeyService _hotkey = null!;
    private ConvertEngine _engine = null!;
    private WinForms.NotifyIcon _tray = null!;
    private WinForms.ToolStripMenuItem _enabledItem = null!, _autostartItem = null!;
    private ToastWindow? _toast;
    private SettingsWindow? _settingsWindow;

    internal Settings Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Local\Ozbeca.SingleInstance", out bool created);
        if (!created)
        {
            Log.Write("Dastur allaqachon ishlayapti — ikkinchi nusxa yopildi");
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        Settings = Settings.Load();
        if (e.Args.Length > 0) Settings.Hotkey = e.Args[0]; // sinov uchun: Ozbeca.exe Ctrl+Alt+F10

        _hotkey = new HotkeyService();
        _engine = new ConvertEngine(_hotkey.Handle);
        _hotkey.Pressed += _engine.Trigger;
        _engine.Notify += msg => Dispatcher.BeginInvoke(() => Toast(msg));
        ApplyDelay();

        CreateTray();

        string? error = ApplyHotkeyState();
        Log.Write($"Ishga tushdi, hotkey {Settings.Hotkey}, yoqilgan={Settings.Enabled}" + (error != null ? $" — {error}" : ""));
        if (error != null)
        {
            _tray.ShowBalloonTip(6000, "Özbeça", error + " Sozlamalarda boshqasini tanlang.", WinForms.ToolTipIcon.Warning);
            ShowSettings()?.ShowStatus(error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _hotkey?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ---- hotkey ----

    /// <summary>Joriy sozlamaga ko'ra hotkey'ni yoqadi/o'chiradi. Xato matni yoki null.</summary>
    private string? ApplyHotkeyState()
    {
        if (!Settings.Enabled) { _hotkey.Unregister(); UpdateTrayText(); return null; }
        string? error = Register(Settings.Hotkey);
        UpdateTrayText();
        return error;
    }

    private string? Register(string combo)
    {
        if (!HotkeyService.TryParse(combo, out uint mods, out int vk)) return $"Noto'g'ri kombinatsiya: {combo}.";
        if (!_hotkey.Register(mods, (uint)vk)) return $"{combo} boshqa dasturda band.";
        _engine.HotkeyVk = vk;
        return null;
    }

    /// <summary>Sozlamalar oynasidan: yangi kombinatsiyani darhol RegisterHotKey bilan tekshiradi.</summary>
    internal string? ChangeHotkey(string combo)
    {
        string? error = Register(combo);
        if (error != null)
        {
            ApplyHotkeyState(); // eskisini qaytaramiz
            return error;
        }
        Settings.Hotkey = combo;
        Settings.Save();
        ApplyHotkeyState();
        Log.Write("Hotkey o'zgardi: " + combo);
        HotkeyService.TryParse(combo, out uint mods, out int vk);
        bool letterOrDigit = vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A;
        return mods == (Native.MOD_CONTROL | Native.MOD_ALT) && letterOrDigit ? AltGrWarning : null;
    }

    internal void SuspendHotkey() => _hotkey.Unregister();
    internal string? ResumeHotkey() => ApplyHotkeyState();
    internal void ApplyDelay() => _engine.RestoreDelayMs = Settings.RestoreDelayMs;

    // ---- tray ----

    private void CreateTray()
    {
        _enabledItem = new WinForms.ToolStripMenuItem("Yoqilgan") { CheckOnClick = true, Checked = Settings.Enabled };
        _enabledItem.Click += (_, _) =>
        {
            Settings.Enabled = _enabledItem.Checked;
            Settings.Save();
            string? error = ApplyHotkeyState();
            if (error != null) _tray.ShowBalloonTip(5000, "Özbeça", error, WinForms.ToolTipIcon.Warning);
        };

        _autostartItem = new WinForms.ToolStripMenuItem("Windows bilan birga ishga tushirish") { CheckOnClick = true };
        _autostartItem.Click += (_, _) => Autostart.Set(_autostartItem.Checked);

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add("Sozlamalar…", null, (_, _) => ShowSettings());
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Chiqish", null, (_, _) => Shutdown());
        menu.Opening += (_, _) =>
        {
            _enabledItem.Checked = Settings.Enabled;
            _autostartItem.Checked = Autostart.IsEnabled;
        };

        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/ozbeca.ico")).Stream;
        _tray = new WinForms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream, WinForms.SystemInformation.SmallIconSize),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == WinForms.MouseButtons.Left) ShowSettings(); };
    }

    private void UpdateTrayText()
    {
        if (_tray == null) return;
        _tray.Text = Settings.Enabled ? $"Özbeça — {Settings.Hotkey}" : "Özbeça — o'chirilgan";
    }

    internal SettingsWindow? ShowSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
        return _settingsWindow;
    }

    private void Toast(string message)
    {
        if (!Settings.ShowToast) return;
        _toast ??= new ToastWindow();
        _toast.ShowMessage(message);
    }
}
