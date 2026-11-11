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
    private SettingsWindow? _settingsWindow;

    internal Settings Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        _engine.Notify += msg => Dispatcher.BeginInvoke(() => Toast(msg));
        ApplyDelay();
        Ozbeca.Core.Transliterator.RestoreApostrophes = Settings.RestoreApostrophes;
        ThreadPool.QueueUserWorkItem(_ => Ozbeca.Core.Transliterator.ToNewAlphabet("ozbek")); // lug'atni oldindan yuklash

        CreateTray();
        ApplyAutoType();

        string? error = ApplyHotkeyState();
        Log.Write($"Ishga tushdi {Theme.Version}, hotkey {Settings.Hotkey}, yoqilgan={Settings.Enabled}, avto-yozish={Settings.AutoType}, admin={Compat.IsElevatedSelf}" + (error != null ? $" — {error}" : ""));
        if (updated && error == null)
            _tray.ShowBalloonTip(5000, "Özbeça", $"{Theme.Version} versiyaga yangilandi. Sozlamalar saqlandi.", WinForms.ToolTipIcon.Info);
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
            if (error != null) _tray.ShowBalloonTip(5000, "Özbeça", error, WinForms.ToolTipIcon.Warning);
        };

        _autoTypeItem = new WinForms.ToolStripMenuItem("Yozish paytida o'girish") { CheckOnClick = true };
        _autoTypeItem.Click += (_, _) => SetAutoType(_autoTypeItem.Checked);

        _autostartItem = new WinForms.ToolStripMenuItem("Windows bilan birga ishga tushirish") { CheckOnClick = true };
        _autostartItem.Click += (_, _) => Autostart.Set(_autostartItem.Checked);

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(TrayMenu.Header());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_autoTypeItem);
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
