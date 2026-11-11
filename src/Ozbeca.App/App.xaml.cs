using System.Windows;
using WinForms = System.Windows.Forms;

namespace Ozbeca.App;

public partial class App : Application
{
    internal const string AltGrWarning =
        "Saqlandi. Diqqat: Ctrl+Alt+harf ba'zi klaviatura tartiblarida AltGr bo'lib, harf yozib yuborishi mumkin.";

    private const string ShowSettingsEvent = @"Local\Ozbeca.ShowSettings";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent, _exitEvent;
    private HotkeyService _hotkey = null!;
    private ConvertEngine _engine = null!;
    private WinForms.NotifyIcon _tray = null!;
    private WinForms.ToolStripMenuItem _enabledItem = null!, _autostartItem = null!, _autoTypeItem = null!;
    private AutoType _autoType = null!;
    private ToastWindow? _toast;
    private NoticeWindow? _notice;
    private SettingsWindow? _settingsWindow;
    private bool _ready; // ishga tushish tugadi (tray bor)

    internal Settings Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Tray dasturi kutilmagan xatoda jim yopilib qolmasin: logga yozamiz va ishlashda davom etamiz.
        // Ishga tushish tugamagan bo'lsa — yopiladi (tray belgisisiz "arvoh" jarayon qolmasin).
        DispatcherUnhandledException += (_, ev) =>
        {
            Log.Write("Kutilmagan xato: " + ev.Exception);
            ev.Handled = _ready;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) => Log.Write("Jiddiy xato: " + ev.ExceptionObject);

        // Yangi versiya ishga tushirildi — eski nusxani yopib, o'rnini egallaymiz (mutex'dan oldin: u eskisida).
        try
        {
            if (Install.UpdateOlder()) { Shutdown(); return; }
        }
        catch (Exception ex) { Log.Write("Yangilashda xato: " + ex); }

        _mutex = new Mutex(true, @"Local\Ozbeca.SingleInstance", out bool created);
        if (!created)
        {
            // Ikkinchi nusxa (masalan, ish stolidagi yorliq bosildi): ishlab turgan nusxaga "sozlamalarni och" deymiz.
            Log.Write("Dastur allaqachon ishlayapti — sozlamalar ishlayotgan nusxada ochiladi");
            if (EventWaitHandle.TryOpenExisting(ShowSettingsEvent, out var ev))
            {
                Native.AllowSetForegroundWindow(-1); // ASFW_ANY — u oynani oldinga chiqara olsin
                ev.Set();
                ev.Dispose();
            }
            else
                MessageBox.Show("Özbeça allaqachon ishlab turibdi.\n\nU ekranning pastki o'ng burchagidagi (soat yonidagi) Ö belgisida.",
                    "Özbeça", MessageBoxButton.OK, MessageBoxImage.Information);
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        Settings = Settings.Load();
        bool updated = Install.Updated || e.Args.Contains(Install.UpdatedArg);
        if (e.Args.Length > 0 && !updated) Settings.Hotkey = e.Args[0]; // sinov uchun: Ozbeca.exe Ctrl+Alt+F10

        if (Settings.FirstRun && e.Args.Length == 0 && RunFirstTimeSetup()) return; // o'rnatilgan nusxa ishga tushirildi

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEvent);
        ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(() => ShowSettings()), null, -1, false);
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Install.ExitEvent);
        ThreadPool.RegisterWaitForSingleObject(_exitEvent, (_, _) => Dispatcher.BeginInvoke(() => Shutdown()), null, -1, true);

        _hotkey = new HotkeyService();
        _engine = new ConvertEngine(_hotkey.Handle);
        _hotkey.Pressed += _engine.Trigger;
        _autoType = new AutoType(_hotkey.Handle);
        _autoType.SetAllowedApps(Settings.AutoTypeApps.Select(a => a.Process));
        _engine.Notify += msg => Dispatcher.BeginInvoke(() => Toast(msg));
        ApplyDelay();
        Ozbeca.Core.Transliterator.RestoreApostrophes = Settings.RestoreApostrophes;
        ThreadPool.QueueUserWorkItem(_ => Ozbeca.Core.Transliterator.ToNewAlphabet("ozbek")); // lug'atni oldindan yuklash

        CreateTray();
        ApplyAutoType();
        // Kompyuter uyqudan yoki qulfdan chiqqanda hook jim o'chib qolgan bo'lishi mumkin: tizim band paytda hook
        // vaqtida javob bermasa, Windows uni ogohlantirmasdan olib tashlaydi. Qayta o'rnatamiz.
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, ev) =>
        {
            if (ev.Mode == Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(() => _autoType.Restart());
        };
        Microsoft.Win32.SystemEvents.SessionSwitch += (_, ev) =>
        {
            if (ev.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock) Dispatcher.BeginInvoke(() => _autoType.Restart());
        };
        _ready = true;

        string? error = ApplyHotkeyState();
        Log.Write($"Ishga tushdi {Theme.Version}, hotkey {Settings.Hotkey}, yoqilgan={Settings.Enabled}, avto-yozish={Settings.AutoType}, admin={Compat.IsElevatedSelf}" + (error != null ? $" — {error}" : ""));
        if (updated && error == null)
            Notice("Özbeça yangilandi", $"{Theme.Version} versiya ishlayapti. Sozlamalar saqlandi.");
        if (error != null)
        {
            Notice("Tezkor tugma ishlamayapti", error + " Sozlamalarda boshqasini tanlang.", warning: true);
            ShowSettings()?.ShowStatus(error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _hotkey?.Dispose();
        _autoType?.Dispose();
        _showEvent?.Dispose();
        _exitEvent?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// O'rnatish oynasi. true — dastur doimiy papkaga ko'chirildi va o'sha nusxa ishga tushirildi (bu nusxa yopiladi).
    /// </summary>
    private bool RunFirstTimeSetup()
    {
        var w = new WelcomeWindow(Settings.Hotkey);
        bool ok = w.ShowDialog() == true;
        if (ok) Settings.AutoType = w.DoAutoType;
        Settings.Save(); // X bosilsa ham — keyingi safar qayta so'ramaymiz
        if (!ok) { Log.Write("O'rnatish oynasi yopildi — hech narsa o'zgartirilmadi"); return false; }

        string target = Compat.ProcessPath;
        try
        {
            if (w.DoInstall) { Install.CopySelf(); target = Install.Exe; }
            if (w.DoDesktop) Install.CreateShortcut(Install.DesktopLink, target);
            if (w.DoStartMenu) Install.CreateShortcut(Install.StartMenuLink, target);
            Autostart.Set(w.DoAutostart, target);
            Log.Write($"O'rnatildi: {target}; yorliq={w.DoDesktop}, start={w.DoStartMenu}, autostart={w.DoAutostart}");
        }
        catch (Exception ex)
        {
            Log.Write("O'rnatishda xato: " + ex);
            MessageBox.Show("O'rnatishda xato bo'ldi: " + ex.Message + "\n\nDastur shu joyidan ishlashda davom etadi.",
                "Özbeça", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.Equals(target, Compat.ProcessPath, StringComparison.OrdinalIgnoreCase)) return false;

        // Yangi joydagi nusxani ishga tushiramiz; u mutex'ni olishi uchun avval o'zimiznikini bo'shatamiz.
        _mutex!.ReleaseMutex();
        _mutex.Dispose();
        _mutex = null;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
        Shutdown();
        return true;
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

    /// <summary>Hook faqat ikkala sozlama yoqilganda o'rnatiladi.</summary>
    private void ApplyAutoType()
    {
        _autoType.PauseConvert = Settings.PauseConvert;
        _autoType.PasteConvert = Settings.PasteConvert;
        if (Settings.Enabled && Settings.AutoType) _autoType.Start();
        else _autoType.Stop();
    }

    /// <summary>Umumiy yoqish/o'chirish (hotkey va yozish paytida o'girish). Xato matni yoki null.</summary>
    internal string? SetEnabled(bool on)
    {
        Settings.Enabled = on;
        Settings.Save();
        string? error = ApplyHotkeyState();
        ApplyAutoType();
        return error;
    }

    internal void SetAutoType(bool on)
    {
        Settings.AutoType = on;
        Settings.Save();
        ApplyAutoType();
    }

    /// <summary>Avto-o'girish qaysi dasturlarda ishlaydi. Bo'sh — hammasida.</summary>
    internal void SetAutoTypeApps(List<AllowedApp> apps)
    {
        Settings.AutoTypeApps = apps;
        Settings.Save();
        _autoType.SetAllowedApps(apps.Select(a => a.Process));
        Log.Write("Avto-o'girish dasturlari: " + (apps.Count == 0 ? "hammasi" : string.Join(", ", apps.Select(a => a.Process))));
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
            string? error = SetEnabled(_enabledItem.Checked);
            if (error != null) Notice("Diqqat", error, warning: true);
        };

        _autoTypeItem = new WinForms.ToolStripMenuItem("Yozish paytida o'girish") { CheckOnClick = true };
        _autoTypeItem.Click += (_, _) => SetAutoType(_autoTypeItem.Checked);

        var pauseItem = new WinForms.ToolStripMenuItem("Pauzada o'girish (1 s)") { CheckOnClick = true };
        pauseItem.Click += (_, _) => { Settings.PauseConvert = pauseItem.Checked; Settings.Save(); ApplyAutoType(); };
        var pasteItem = new WinForms.ToolStripMenuItem("Ctrl+V da o'girish") { CheckOnClick = true };
        pasteItem.Click += (_, _) => { Settings.PasteConvert = pasteItem.Checked; Settings.Save(); ApplyAutoType(); };

        _autostartItem = new WinForms.ToolStripMenuItem("Windows bilan birga ishga tushirish") { CheckOnClick = true };
        _autostartItem.Click += (_, _) => Autostart.Set(_autostartItem.Checked);

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(TrayMenu.Header());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_autoTypeItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(pasteItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sozlamalar…", null, (_, _) => ShowSettings());
        menu.Items.Add("Chiqish", null, (_, _) => Shutdown());
        TrayMenu.Style(menu);
        menu.Opening += (_, _) =>
        {
            _enabledItem.Checked = Settings.Enabled;
            _autoTypeItem.Checked = Settings.AutoType;
            _autoTypeItem.Enabled = Settings.Enabled;
            pauseItem.Checked = Settings.PauseConvert;
            pasteItem.Checked = Settings.PasteConvert;
            pauseItem.Enabled = pasteItem.Enabled = Settings.Enabled && Settings.AutoType;
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
        _tray.Text = Settings.Enabled ? $"Özbeça — {string.Join("+", HotkeyService.Caps(Settings.Hotkey))}" : "Özbeça — o'chirilgan";
    }

    internal SettingsWindow? ShowSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
        return _settingsWindow;
    }

    private void Toast(string message)
    {
        if (!Settings.ShowToast) return;
        _toast ??= new ToastWindow();
        _toast.ShowMessage(message);
    }

    /// <summary>Soat ustida chiqadigan bildirishnoma (tray balloon o'rniga — dastur uslubida).</summary>
    private void Notice(string title, string message, bool warning = false)
    {
        if (_notice == null)
        {
            _notice = new NoticeWindow();
            _notice.Clicked += () => ShowSettings();
        }
        _notice.ShowNotice(title, message, warning);
    }
}
