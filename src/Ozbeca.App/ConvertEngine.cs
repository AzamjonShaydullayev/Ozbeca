using System.Text;
using Ozbeca.Core;
using static Ozbeca.App.Native;

namespace Ozbeca.App;

/// <summary>Hotkey bosilgandagi to'liq oqim. Ish alohida STA thread'da.</summary>
internal sealed class ConvertEngine
{
    // Terminalda Ctrl+C jarayonni to'xtatadi — Ctrl+Insert / Shift+Insert ishlatiladi.
    private static readonly HashSet<string> TerminalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "mintty", "PuTTY",
    };

    // Office: Word/Excel/PowerPoint/Outlook oynalari.
    private static readonly HashSet<string> OfficeClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "OpusApp", "XLMAIN", "PPTFrameClass", "rctrl_renwnd32",
    };

    // Sarlavhadagi "himoyalangan ko'rinish" / "faqat o'qish" belgilari (Office UI tiliga qarab).
    // Himoyalangan ko'rinishda Ctrl+C umuman ishlamaydi; faqat o'qishda paste ishlamaydi.
    private static readonly string[] ReadOnlyTitleMarkers =
    {
        "Protected View", "Read-Only", "Защищенный просмотр", "Защищённый просмотр", "Только для чтения",
        "Himoyalangan", "Faqat o'qish", "Faqat oʻqish",
    };

    private const int CopyTimeoutMs = 600;
    private const int OfficeRestoreDelayMs = 1000;

    private readonly ClipboardSwap _clip;
    private int _busy;

    public ConvertEngine(IntPtr clipboardOwner) => _clip = new ClipboardSwap(clipboardOwner);

    /// <summary>Hotkey'ning asosiy tugmasi (modifier'lardan tashqari) — qo'yib yuborilishini kutish uchun.</summary>
    public int HotkeyVk { get; set; } = VK_SPACE;
    public int RestoreDelayMs { get; set; } = 400;

    /// <summary>Foydalanuvchiga ko'rsatiladigan xabar (toast).</summary>
    public event Action<string>? Notify;

    public void Trigger()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            Log.Write("Oldingi o'girish tugamagan — e'tiborsiz");
            return;
        }
        var t = new Thread(() =>
        {
            try { Run(); }
            catch (Exception ex) { Log.Write("Xato: " + ex); }
            finally { Volatile.Write(ref _busy, 0); }
        }) { IsBackground = true, Name = "OzbecaConvert" };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    private void Run()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        string cls = ClassNameOf(hwnd);
        bool terminal = TerminalClasses.Contains(cls);
        Log.Write($"Hotkey: oyna 0x{hwnd:X} class={cls} terminal={terminal}");

        if (IsElevatedWindow(hwnd))
        {
            Say("Administrator huquqidagi oynada ishlamaydi");
            return;
        }

        bool office = OfficeClasses.Contains(cls);
        if (office && IsReadOnlyTitle(hwnd))
        {
            Say("Tahrirlashga ruxsat yo'q — avval «Tahrirlashni yoqish»ni bosing");
            return;
        }

        if (InputSender.IsDown(VK_MENU)) InputSender.SendMask();
        InputSender.WaitKeysReleased(HotkeyVk);
        if (!Same(hwnd)) return;

        var snapshot = _clip.Save();
        uint seq = _clip.Clear();
        if (seq == 0) { Say("Clipboard band"); return; }

        bool copied = Copy(VK_CONTROL, terminal ? VK_INSERT : VK_C, seq);
        if (!copied && !terminal) copied = Copy(VK_CONTROL, VK_INSERT, seq); // zaxira yo'l

        string? text = copied ? _clip.ReadText() : null;
        if (text == null || text.Length == 0)
        {
            _clip.Restore(snapshot);
            Say(office ? "Matn belgilanmagan yoki tahrirlashga ruxsat yo'q" : "Matn belgilanmagan");
            return;
        }
        if (!Same(hwnd)) { _clip.Restore(snapshot); return; }

        // Telegram: stillar (qalin, chiziqli, premium emoji) alohida formatda — ularni ham surib qaytaramiz.
        List<(string, byte[])>? extras = null;
        string converted;
        if (TryTelegram(_clip, out var tgOld, out var tgNew, out var tgTags))
        {
            text = tgOld;
            converted = tgNew;
            extras = new() { (TelegramTags.TextFormat, Encoding.UTF8.GetBytes(tgNew)), (TelegramTags.TagsFormat, tgTags) };
            Log.Write("Telegram stillari saqlanadi");
        }
        else converted = Transliterator.ToNewAlphabet(text);

        if (converted == text)
        {
            _clip.Restore(snapshot);
            Say("O'zgarish yo'q");
            return;
        }

        uint ours = _clip.SetText(converted, extras);
        if (ours == 0 || !Same(hwnd)) { _clip.Restore(snapshot); return; }

        bool sent = terminal ? InputSender.Chord(VK_SHIFT, VK_INSERT) : InputSender.Chord(VK_CONTROL, VK_V);
        if (!sent) Log.Write("SendInput: paste yuborilmadi");

        // Office kabi ilovalar clipboard'ni kechroq o'qiydi — shuning uchun darhol tiklanmaydi.
        // Office'da avtomatik ko'proq kutiladi (UI'da sozlama yo'q; settings.json'dagi qiymat — eng kami).
        Thread.Sleep(office ? Math.Max(RestoreDelayMs, OfficeRestoreDelayMs) : RestoreDelayMs);
        // Explorer kabi ba'zilari paste'dan keyin o'sha matnni clipboard'ga qayta yozadi (sequence o'zgaradi).
        // Matn hamon biznikiga teng bo'lsa — bu userning yangi ma'lumoti emas, tiklash xavfsiz.
        if (ClipboardSwap.Sequence == ours || _clip.ReadText() == converted) _clip.Restore(snapshot);
        else Log.Write("Clipboard'ni boshqa dastur o'zgartirdi — tiklanmadi");

        Say("Özbeça ✓"); // muvaffaqiyat xabari (user qarori, 2026-11-10)
    }

    /// <summary>Clipboard'da Telegram formati bo'lsa — matn va stillarni o'giradi (hotkey va Ctrl+V uchun umumiy).</summary>
    internal static bool TryTelegram(ClipboardSwap clip, out string oldText, out string newText, out byte[] newTags)
    {
        oldText = newText = "";
        newTags = Array.Empty<byte>();
        var tdText = clip.ReadNamed(TelegramTags.TextFormat);
        var tdTags = clip.ReadNamed(TelegramTags.TagsFormat);
        if (tdText == null || tdTags == null) return false;
        if (!TelegramTags.TryConvert(tdText, tdTags, out newText, out newTags))
        {
            Log.Write("Telegram formati o'qilmadi — oddiy matn sifatida");
            return false;
        }
        oldText = Encoding.UTF8.GetString(tdText);
        return true;
    }

    private static bool Copy(int modifierVk, int keyVk, uint seq)
    {
        if (!InputSender.Chord(modifierVk, keyVk)) Log.Write("SendInput: copy yuborilmadi");
        return ClipboardSwap.WaitForChange(seq, CopyTimeoutMs);
    }

    /// <summary>Jarayon davomida foreground oyna o'zgarsa — bekor (matn boshqa oynaga tushmasin).</summary>
    private static bool Same(IntPtr hwnd)
    {
        if (GetForegroundWindow() == hwnd) return true;
        Log.Write("Foreground oyna o'zgardi — bekor qilindi");
        return false;
    }

    private void Say(string message)
    {
        Log.Write(message);
        Notify?.Invoke(message);
    }

    private static bool IsReadOnlyTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        string title = sb.ToString();
        return ReadOnlyTitleMarkers.Any(m => title.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static bool IsTerminalWindow(IntPtr hwnd) => TerminalClasses.Contains(ClassNameOf(hwnd));

    internal static string ClassNameOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    /// <summary>
    /// UIPI: elevated oynaga oddiy dastur SendInput yubora olmaydi. Token ochilmasa — elevated deb hisoblaymiz.
    /// O'zimiz ham elevated bo'lsak — to'siq yo'q. UAC o'chiq kompyuterda HAMMA oyna elevated ko'rinadi:
    /// bu tekshiruvsiz avto-yozish hamma joyda jim o'chib qolardi (Ctrl+Alt+O esa ishlardi).
    /// </summary>
    internal static bool IsElevatedWindow(IntPtr hwnd)
    {
        if (Compat.IsElevatedSelf) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        IntPtr proc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (proc == IntPtr.Zero) return true;
        try
        {
            if (!OpenProcessToken(proc, TOKEN_QUERY, out IntPtr token)) return true;
            try { return GetTokenInformation(token, TokenElevation, out int elevated, sizeof(int), out _) && elevated != 0; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(proc); }
    }
}
