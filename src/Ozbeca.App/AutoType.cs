using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Ozbeca.Core;
using static Ozbeca.App.Native;

namespace Ozbeca.App;

/// <summary>
/// Yozish paytida avtomatik o'girish: so'z tugab probel bosilganda, o'sha so'z yangi alifboga almashtiriladi.
/// Low-level hook FAQAT shu funksiya yoqilganda o'rnatiladi. Tugmalar faqat joriy so'zni bilish uchun
/// xotirada turadi (eng ko'pi 64 belgi) — hech qayerga yozilmaydi va yuborilmaydi.
///
/// Xavfsizlik:
/// - Enter'da oxirgi so'z faqat parol maydoni bo'lmasa o'giriladi (Win32 ES_PASSWORD + UIA IsPassword) —
///   choy1990 + Enter parolda çoy1990 bo'lib qolmasin.
/// - Win32 parol maydoni, terminal, parol menejeri, masofaviy ish stoli, admin oynada o'chiq.
/// - Kod muharrirlarida — kod rejimi: faqat qo'shtirnoq "…" ichidagi matn o'giriladi (TokenBuffer.CodeMode).
/// - Sichqoncha bosilsa, strelka/Home/End/Tab/Esc yoki boshqa oyna — so'z unutiladi (noto'g'ri joyni almashtirmaslik uchun).
/// </summary>
internal sealed class AutoType : IDisposable
{
    /// <summary>Kod muharrirlari — kod rejimi (faqat "…" ichi), Ctrl+V o'girilmaydi.</summary>
    private static readonly HashSet<string> CodeEditors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "Code - Insiders", "VSCodium", "Cursor", "Windsurf", "Trae", "Kiro", "devenv", "rider64", "idea64",
        "pycharm64", "webstorm64", "phpstorm64", "clion64", "goland64", "rubymine64", "datagrip64", "dataspell64",
        "fleet", "studio64", "eclipse", "netbeans64", "sublime_text", "zed", "notepad++", "atom", "brackets",
        "gvim", "neovide", "nvim-qt", "emacs", "runemacs", "lapce", "geany", "kate", "ssms", "azuredatastudio",
        "HeidiSQL", "DBeaver", "Postman", "Insomnia",
    };

    /// <summary>Umuman o'chiq: buyruqlar, parollar, boshqa kompyuter.</summary>
    internal static readonly HashSet<string> BlockedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // terminallar
        "WindowsTerminal", "cmd", "powershell", "pwsh", "conhost", "putty", "mintty", "wsl",
        // parol menejerlari, masofaviy ish stoli, virtual mashinalar
        "KeePass", "KeePassXC", "1Password", "Bitwarden", "mstsc", "vmconnect", "VirtualBoxVM", "vmware-vmx",
    };

    /// <summary>O'zimiz yuborgan tugmalar belgisi (dwExtraInfo) — ularni qayta qayta ishlamaymiz.</summary>
    private static readonly IntPtr OwnInput = new(0x0E8A0E8A);

    private readonly TokenBuffer _word = new();
    private readonly LowLevelProc _keyProc, _mouseProc; // GC yig'ib olmasligi uchun maydonda
    private IntPtr _keyHook, _mouseHook;
    private IntPtr _lastWindow;
    private enum Mode { Off, Prose, Code }
    private Mode _mode;

    // Fokusdagi maydon parolmi (UI Automation). null — hali bilmaymiz; sichqoncha, Tab, boshqa oyna — qayta tekshiriladi.
    private bool? _focusPassword;
    private readonly byte[] _keyState = new byte[256];
    private readonly StringBuilder _chars = new(8);

    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    private readonly ClipboardSwap _clip;

    /// <param name="clipboardOwner">Ctrl+V da clipboard'ni vaqtincha almashtirish uchun oyna (hotkey oynasi).</param>
    public AutoType(IntPtr clipboardOwner)
    {
        _clip = new ClipboardSwap(clipboardOwner);
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _keyProc = OnKey;
        _mouseProc = OnMouse;
    }

    public bool IsRunning => _keyHook != IntPtr.Zero;

    // Faqat shu dasturlarda o'giriladi (jarayon nomi). null — hamma dasturlarda.
    private HashSet<string>? _allowed;

    /// <summary>User tanlagan dasturlar. Bo'sh ro'yxat — hamma dasturlarda ishlaydi.</summary>
    public void SetAllowedApps(IEnumerable<string> processes)
    {
        var set = new HashSet<string>(processes, StringComparer.OrdinalIgnoreCase);
        _allowed = set.Count == 0 ? null : set;
        _lastWindow = IntPtr.Zero; // joriy oyna keyingi tugmada qayta tekshirilsin
    }

    public void Start()
    {
        if (IsRunning) return;
        IntPtr module = GetModuleHandle(null);
        _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        int hookError = Marshal.GetLastWin32Error();
        if (!IsRunning) Stop(); // klaviatura hook'i bo'lmasa sichqonchanikini ham qoldirmaymiz (keyingi Start'da oqib ketmasin)
        _word.Reset();
        _lastWindow = IntPtr.Zero;
        if (_sender == null)
        {
            _sender = new Thread(SenderLoop) { IsBackground = true, Name = "OzbecaAutoTypeSender" };
            _sender.Start();
        }
        Log.Write(IsRunning ? "Yozish paytida o'girish yoqildi" : "Hook o'rnatilmadi: " + hookError);
    }

    public void Stop()
    {
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyHook = _mouseHook = IntPtr.Zero;
        _word.Reset();
    }

    public void Dispose() => Stop();

    /// <summary>Hook'larni qayta o'rnatadi (uyqu/qulfdan keyin). Almashtirish ketayotgan bo'lsa — tegmaydi.</summary>
    public void Restart()
    {
        if (!IsRunning || _pending > 0 || _settling > 0 || _held.Count > 0) return;
        Stop();
        Start();
    }

    private IntPtr OnMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = wParam.ToInt32();
        if (nCode >= 0 && (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN))
        {
            ResetWord();
            ForgetFocus(); // boshqa maydonga bosilgan bo'lishi mumkin
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ---- Tartibni saqlash ----
    // Probel ushlanib, o'rniga "Backspace×N + yangi so'z" yuborilganda, foydalanuvchining keyingi tugmalari
    // navbatda bizning tugmalarimizdan OLDIN turgan bo'lishi mumkin (dastur bir lahzaga band bo'lsa yoki juda tez
    // yozilsa) — matn aralashib ketadi. Shuning uchun bizning tugmalarimiz hook'dan o'tib bo'lguncha kelgan
    // tugmalar ushlab turiladi va keyin aynan shu tartibda qayta yuboriladi.
    private static readonly IntPtr ReplayInput = new(0x0E8A0E8B);
    private const int WM_KEYUP = 0x101, WM_SYSKEYUP = 0x105;
    private const uint LLKHF_EXTENDED = 0x1;
    // Saqlagich: o'zimiz yuborgan tugmalar shuncha vaqtda hook'dan o'tmasa — ushlanganlar qaytariladi.
    // Ataylab keng: muddatidan oldin qaytarilgan tugma almashtirish orasiga tushib, backspace bilan o'chib ketardi.
    private const int PendingTimeoutMs = 1500;

    private readonly List<(KBDLLHOOKSTRUCT Key, bool Up)> _held = new();
    private int _pending;          // hali hook'dan o'tmagan o'zimiz yuborgan tugmalar soni
    private int _pendingSince;
    private System.Windows.Threading.DispatcherTimer? _guard;

    private IntPtr OnKey(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = wParam.ToInt32();
        bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN, up = msg == WM_KEYUP || msg == WM_SYSKEYUP;
        if (nCode < 0 || !(down || up)) return CallNextHookEx(_keyHook, nCode, wParam, lParam);

        var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        bool injected = (k.flags & LLKHF_INJECTED) != 0;
        try
        {
            if (injected && k.dwExtraInfo == InputSender.EngineInput)
                return CallNextHookEx(_keyHook, nCode, wParam, lParam); // hotkey oqimining Ctrl+C/Ctrl+V'si

            if (injected && k.dwExtraInfo == OwnInput)
            {
                _holdReplays = false;
                if (_pending > 0) _pending--;
                TryFlush();
                return CallNextHookEx(_keyHook, nCode, wParam, lParam);
            }

            bool replay = injected && k.dwExtraInfo == ReplayInput;
            if (replay)
            {
                if (_pending > 0) _pending--;
                // Qayta yuborilganlar ichida yangi almashtirish boshlangan: qolganlari undan keyin turishi kerak —
                // yana ushlaymiz (bu paytda kelgan yangi tugmalardan oldinga) va almashtirishdan keyin qaytaramiz.
                if (_holdReplays)
                {
                    _held.Insert(_reheld++, (k, up));
                    return new IntPtr(1);
                }
                TryFlush(); // qayta yuborish paytida yana ushlanganlar
            }
            else if (_pending > 0 || _settling > 0 || _held.Count > 0) // navbatda kutayotganlar bo'lsa — ulardan oldin o'tmasin
            {
                _held.Add((k, up));
                return new IntPtr(1); // keyinroq tartib bilan qaytariladi
            }

            // Qayta yuborilgan tugmalar ham o'giriladi (tez yozish, Ctrl+V va Enter almashtirishdan keyin darhol).
            if (down && HandleKeyDown(k))
            {
                if (replay) _holdReplays = true;
                return new IntPtr(1);
            }
        }
        catch (Exception ex)
        {
            _word.Reset();
            Log.Write("AutoType xato: " + ex.Message);
        }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    // Qayta yuborilgan tugma yangi almashtirishni boshladi — partiyaning qolgani almashtirishdan keyin ketadi.
    // Navbat FIFO: almashtirishning birinchi tugmasi kelganda partiyadan boshqa tugma qolmagan bo'ladi.
    private bool _holdReplays;
    private int _reheld; // _held boshiga qaytarilgan (qayta ushlangan) tugmalar soni

    private void BeginPending(int count, int extraMs = 0)
    {
        _pending += count;
        _pendingSince = Environment.TickCount + extraMs; // yuborish uchun ketadigan vaqt qo'shiladi
        if (_guard == null)
        {
            _guard = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _guard.Tick += (_, _) =>
            {
                // Saqlagich: tugmalarimiz biror sabab bilan yetib bormasa, ushlangan tugmalar yo'qolib qolmasin.
                if (_pending > 0 && Environment.TickCount - _pendingSince > PendingTimeoutMs)
                {
                    Log.Write("AutoType: kutish vaqti tugadi, ushlangan tugmalar qaytarildi");
                    _pending = 0;
                    _settling = 0;
                    _holdReplays = false;
                    FlushHeld();
                }
                if (_pending == 0 && _settling == 0 && _held.Count == 0) _guard!.Stop();
            };
        }
        _guard.Start();
    }

    private void FlushHeld()
    {
        if (_held.Count == 0) return;
        var inputs = _held.Select(h => new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)h.Key.vkCode,
                    wScan = (ushort)h.Key.scanCode,
                    dwFlags = (h.Up ? KEYEVENTF_KEYUP : 0) | ((h.Key.flags & LLKHF_EXTENDED) != 0 ? KEYEVENTF_EXTENDEDKEY : 0),
                    dwExtraInfo = ReplayInput,
                },
            },
        }).ToArray();
        _held.Clear();
        _reheld = 0;
        _holdReplays = false; // yangi partiya: oldingi almashtirish o'tib bo'lgan (yoki yuborilmay qolgan) — qotib qolmasin
        SendDeferred(inputs, "ushlangan tugmalar");
    }

    /// <summary>
    /// Hook ichida yuborilgan tugmalar hook'lardan o'tmaydi — shuning uchun hook'dan chiqqandan keyin yuboramiz.
    /// Ular hook'dan o'tib bo'lguncha (pending) foydalanuvchining yangi tugmalari ushlab turiladi.
    /// </summary>
    private void SendDeferred(INPUT[] inputs, string what)
    {
        BeginPending(inputs.Length, inputs.Length / 2 * (PaceMs + 20)); // sekin kompyuter uchun zaxira bilan
        _settling++;
        _sendQueue.Add((inputs, what));
    }

    /// <summary>
    /// Ushlangan tugmalarni qaytarish vaqti: o'zimiz yuborganlar hook'dan o'tgan (pending = 0) VA ilova ularni
    /// qayta ishlab ulgurgan bo'lishi kerak (settling = 0). Windows 11 Notepad Unicode harflarni (ş, ç, ö)
    /// oddiy tugmalardan kechroq qayta ishlaydi — tinchlanishsiz keyingi harf ulardan oldin kirib, yo'qolardi.
    /// </summary>
    private void TryFlush()
    {
        if (_pending == 0 && _settling == 0) FlushHeld();
    }

    private int _settling; // yuborilgan, lekin hali "tinchlanmagan" navbat elementlari
    private const int SettleMs = 40;

    // Windows 11 Notepad kabi ilovalar bir zumda kelgan Unicode harflar tartibini almashtirib yuboradi
    // ("shahar" → "şaha r"). Shuning uchun har bir tugma (bosish+qo'yish) orasida kichik pauza — alohida
    // thread'da, UI va hook'ni bloklamaslik uchun. Navbat yagona: almashtirish va qayta yuborish tartibi saqlanadi.
    private const int PaceMs = 6;
    private const int AfterDeleteMs = 30;
    private readonly System.Collections.Concurrent.BlockingCollection<(INPUT[] Inputs, string What)> _sendQueue = new();
    private Thread? _sender;

    /// <summary>Aniq qisqa pauza: Thread.Sleep(4) Windows'da ~15 ms uxlaydi (taymer aniqligi).</summary>
    private static void Pause(int ms)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) Thread.Yield();
    }

    private void SenderLoop()
    {
        int size = Marshal.SizeOf<INPUT>();
        foreach (var (inputs, what) in _sendQueue.GetConsumingEnumerable())
        {
            int sent = 0;
            bool prevWasDelete = false;
            for (int i = 0; i < inputs.Length; i += 2)
            {
                int n = Math.Min(2, inputs.Length - i);
                var pair = new INPUT[n];
                Array.Copy(inputs, i, pair, 0, n);
                bool isDelete = pair[0].U.ki.wVk == VK_BACK;
                // Win11 Notepad backspace'lardan darhol keyin kelgan birinchi harfni ba'zan tashlab yuboradi
                // ("tosh" → "oş") — o'chirish tugashini biroz kutamiz.
                if (prevWasDelete && !isDelete) Pause(AfterDeleteMs);
                prevWasDelete = isDelete;
                sent += (int)SendInput((uint)n, pair, size);
                Pause(PaceMs);
            }
            bool ok = sent == inputs.Length;
            if (ok && what != "ushlangan tugmalar") Pause(SettleMs); // ilova almashtirishni yozib ulgursin
            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (_settling > 0) _settling--;
                if (!ok)
                {
                    Log.Write($"AutoType: {what} to'liq yuborilmadi ({sent}/{inputs.Length})");
                    _pending = 0;
                }
                TryFlush();
            }));
        }
    }

    /// <summary>true — tugma ushlab qolindi (o'rniga o'zimiz yubordik).</summary>
    private bool HandleKeyDown(KBDLLHOOKSTRUCT k)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg != _lastWindow)
        {
            _lastWindow = fg;
            _mode = WindowMode(fg);
            _word.CodeMode = _mode == Mode.Code;
            ForgetFocus();
            ResetWord();
        }
        if (_mode == Mode.Off) return false;

        int vk = (int)k.vkCode;
        // Modifier'lar, NumLock/ScrollLock/Pause, ovoz va media tugmalari kursorni siljitmaydi — so'zga ta'sir qilmaydi.
        if (vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_CAPITAL or VK_LWIN or VK_RWIN or 0x13 or 0x90 or 0x91
            or >= 0xA0 and <= 0xA5 or >= 0xA6 and <= 0xB7)
            return false;

        // Ctrl+V: qo'yilayotgan matn (probel yoki yangi qatori bo'lsa) ham o'giriladi.
        // Tegilmaydi: parol maydoni (UIA bilan ma'lum bo'lsa ham) va Office'ga qo'yilayotgan Excel kataklari —
        // oddiy matnga aylantirilsa formulalar va jadval yo'qolardi.
        if (vk == VK_V && _mode == Mode.Prose && InputSender.IsDown(VK_CONTROL) && !InputSender.IsDown(VK_MENU) &&
            !InputSender.IsDown(VK_LWIN) && !InputSender.IsDown(VK_RWIN) && !IsPasswordFocus(fg) && _focusPassword != true &&
            !(ConvertEngine.IsOfficeWindow(fg) && ClipboardSwap.HasSpreadsheetCells))
        {
            ResetWord();
            BeginPending(1, 600); // haqiqiy son PasteConverted'da aniqlanadi; shu orada kelgan tugmalar ushlanadi
            _dispatcher.BeginInvoke(new Action(PasteConverted));
            return true; // V ushlandi — clipboard tayyor bo'lgach o'zimiz yuboramiz
        }

        // LL-hook'da asinxron holat voqealar tartibida yangilanadi (ushlangan tugmalar uni o'zgartirmaydi).
        // AltGr (= Ctrl + o'ng Alt) bilan harf yozish mumkin — ba'zi o'zbek layoutlarida tutuq ʻ shunday yoziladi.
        bool ctrl = InputSender.IsDown(VK_CONTROL), alt = InputSender.IsDown(VK_MENU);
        bool altGr = ctrl && alt && InputSender.IsDown(VK_RMENU);
        if (!altGr && (ctrl || alt) || InputSender.IsDown(VK_LWIN) || InputSender.IsDown(VK_RWIN))
        {
            ResetWord(); // Ctrl+V, Ctrl+Z, Alt+Tab... — matn qayerda ekanini endi bilmaymiz
            ForgetFocus(); // Ctrl+Tab, Ctrl+L, Alt+D — o'sha oynada boshqa maydon (parol bo'lishi mumkin)
            return false;
        }

        // Dead key ('/`/^ kabi) dan keyingi probel: probel yozilmaydi, faqat o'sha belgi chiqadi — so'z davom etadi.
        if (vk == VK_SPACE && _dead is char deadChar)
        {
            _dead = null;
            _word.Add(deadChar);
            return false;
        }

        switch (vk)
        {
            case VK_BACK:
                if (_dead != null) { ResetWord(); return false; } // dead key bekor qilindi — nima o'chganini bilmaymiz
                // O'girilgan so'zdan keyin darhol Backspace — o'girish qaytadi (check → çeck → check), Backspace yutiladi.
                if (_word.TryUndo(out int undoDel, out string undoText))
                {
                    Replace(undoDel, undoText, Array.Empty<INPUT>());
                    return true;
                }
                _word.Backspace();
                ArmPause();
                return false;
            case VK_SPACE:
                return CompleteWord(fg);
            case VK_RETURN:
                _dead = null;
                // "Yaxshimisan?" + Enter — oxirgi so'z ham o'girilsin. Parol maydoni bo'lsa — tegmaymiz (pastda UIA).
                if (_mode == Mode.Prose && !IsPasswordFocus(fg) &&
                    _word.TryCompleteAtEnter(out int enterDel, out string enterWord))
                {
                    Replace(enterDel, enterWord, new[] { Key(VK_RETURN, false), Key(VK_RETURN, true) });
                    ForgetFocus(); // pastdagi izohga qarang; Replace joriy maydon javobini olib bo'ldi
                    return true; // Enter ushlandi — so'z almashgach o'zimiz yuboramiz
                }
                _word.ResetNewLine();
                // Enter fokusni ko'chirishi mumkin: login → Enter → o'sha oynada parol maydoni (Google, Telegram Web).
                // Eski maydonning "parol emas" javobi yangi maydonga o'tib ketmasin.
                ForgetFocus();
                return false;
            case 0x24: // Home
                _word.ResetNewLine(); // kursor qator boshida — kod rejimida qo'shtirnoqdan tashqari ekani aniq
                return false;
            case VK_TAB:
                ForgetFocus();
                ResetWord();
                return false;
            case VK_ESCAPE:
                ForgetFocus(); // dialog yopilib, fokus boshqa maydonga o'tishi mumkin
                ResetWord();
                return false;
            case VK_DELETE:
            case VK_INSERT:
            case >= 0x21 and <= 0x28: // PgUp, PgDn, End, strelkalar
                ResetWord();
                return false;
        }

        string? typed = ToChars(fg, k, altGr, out bool isDead);
        if (isDead && typed?.Length == 1)
        {
            // Dead key: belgi keyingi tugma bilan birga chiqadi. So'zni unutmaymiz (aks holda to'g'ri → to'g'irlaç).
            if (_dead is char prev) _word.Add(prev); // ikki dead key ketma-ket — birinchisi o'zi chiqadi
            _dead = typed[0];
            PrefetchPassword();
            return false;
        }
        if (typed != null && _dead is char dead)
        {
            _dead = null;
            typed = Compose(dead, typed);
        }

        if (typed != null && typed.All(ch => !char.IsControl(ch) && !char.IsWhiteSpace(ch)))
        {
            // Kod rejimi: qo'shtirnoq yopilyapti — oldingi so'zni o'girib, keyin " ni o'zimiz yozamiz.
            if (typed == "\"" && _word.TryCompleteBeforeQuote(out int del, out string word))
            {
                Replace(del, word, UnicodeKeys('"'));
                return true;
            }
            foreach (char ch in typed) _word.Add(ch);
            PrefetchPassword();
            ArmPause();
        }
        else
        {
            ResetWord();
            ForgetFocus(); // F6 va shunga o'xshash tugmalar fokusni ko'chiradi
        }
        return false;
    }

    // ---- Dead key (US-International va b.): ' ` ^ ~ " keyingi harf bilan birlashadi yoki o'zi chiqadi ----
    private char? _dead;

    private void ResetWord()
    {
        _dead = null;
        _word.Reset();
    }

    /// <summary>
    /// Dead key + keyingi belgi ekranda nima bo'lib chiqadi (US-International jadvali): unli bilan birlashadi
    /// (' + o → ó), ' + c → ç, ~ + n → ñ; qolganlari — ikkalasi alohida (' + g → 'g, shuning uchun to'g'ri buzilmaydi).
    /// </summary>
    private static string Compose(char dead, string next)
    {
        if (next.Length != 1) return dead + next;
        char c = next[0];
        if (dead == '\'' && (c == 'c' || c == 'C')) return c == 'c' ? "ç" : "Ç";
        char mark = dead switch
        {
            '\'' or '´' => '\u0301', '`' => '\u0300', '^' => '\u0302', '~' => '\u0303', '"' or '¨' => '\u0308', _ => '\0',
        };
        bool composes = "aeiouyAEIOUY".IndexOf(c) >= 0 || (dead == '~' && (c == 'n' || c == 'N'));
        if (mark != '\0' && composes)
        {
            string composed = (next + mark).Normalize(NormalizationForm.FormC);
            if (composed.Length == 1) return composed;
        }
        return dead + next;
    }

    // ---- Pauza: so'z yozilib, biroz kutilsa — probel/Enter'ni kutmasdan o'giriladi ----
    private const int PauseMs = 1000;
    private System.Windows.Threading.DispatcherTimer? _pause;

    /// <summary>Har harf/backspace'da qayta boshlanadi. Hook bilan bitta thread'da — tugma bilan poyga yo'q.</summary>
    private void ArmPause()
    {
        if (_pause == null)
        {
            _pause = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PauseMs) };
            _pause.Tick += (_, _) => { _pause.Stop(); ConvertOnPause(); };
        }
        _pause.Stop();
        _pause.Start();
    }

    private void ConvertOnPause()
    {
        if (!IsRunning || _mode == Mode.Off || _pending > 0 || _settling > 0 || _held.Count > 0) return;
        IntPtr fg = GetForegroundWindow();
        if (fg != _lastWindow) return; // boshqa oynaga o'tilgan — so'z u yerda emas
        // Ushlab turilgan modifier bilan Backspace boshqa ma'no beradi (Ctrl+Backspace — butun so'zni o'chiradi).
        if (InputSender.IsDown(VK_SHIFT) || InputSender.IsDown(VK_CONTROL) || InputSender.IsDown(VK_MENU) ||
            InputSender.IsDown(VK_LWIN) || InputSender.IsDown(VK_RWIN) || IsPasswordFocus(fg)) return;
        if (_word.TryConvertAtPause(out int del, out string word))
            Replace(del, word, Array.Empty<INPUT>());
    }

    private bool CompleteWord(IntPtr fg)
    {
        if (IsPasswordFocus(fg)) { ResetWord(); return false; }
        if (!_word.TryComplete(out int deleteCount, out string replacement)) return false;
        Replace(deleteCount, replacement, UnicodeKeys(' '));
        return true;
    }

    private static INPUT[] UnicodeKeys(char ch) => new[] { Unicode(ch, false), Unicode(ch, true) };

    private static List<INPUT> ReplacementKeys(int deleteCount, string replacement, INPUT[] trailing)
    {
        var keys = new List<INPUT>(deleteCount * 2 + replacement.Length * 2 + trailing.Length);
        for (int i = 0; i < deleteCount; i++) { keys.Add(Key(VK_BACK, false)); keys.Add(Key(VK_BACK, true)); }
        foreach (char ch in replacement) { keys.Add(Unicode(ch, false)); keys.Add(Unicode(ch, true)); }
        keys.AddRange(trailing);
        return keys;
    }

    /// <summary>
    /// So'zni almashtiradi (Backspace × N + yangi so'z), keyin ushlangan tugmani (probel, ", Enter) yuboradi.
    /// Parol maydonida — so'zga tegmaydi, faqat ushlangan tugma ketadi. Parol ekanini UI Automation bilan bilamiz
    /// (Chrome, Telegram, WinUI ham); natija shu maydon uchun eslab qolinadi — keyingi so'zlar kutmaydi.
    /// Tekshiruv paytida kelgan tugmalar ushlanadi (pending) va keyin tartib bilan qaytariladi.
    /// </summary>
    private void Replace(int deleteCount, string replacement, INPUT[] trailing)
    {
        if (_focusPassword == false)
        {
            SendDeferred(ReplacementKeys(deleteCount, replacement, trailing).ToArray(), "almashtirish");
            return;
        }

        BeginPending(1, UiaTimeoutMs + 1100); // haqiqiy son tekshiruvdan keyin aniqlanadi
        int focus = _focusGen;
        bool? cached = _focusPassword; // true — shu maydon parol ekani allaqachon ma'lum
        var check = cached != null ? null : _uiaCheck ?? System.Threading.Tasks.Task.Run(FocusIsPasswordUia); // odatda harf yozilganda boshlangan
        _uiaCheck = null;
        System.Threading.Tasks.Task.Run(() =>
        {
            bool? uia = cached ?? check!.Result;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                // Javob kelmasa (sekin kompyuter) — bu so'zga tegmaymiz, lekin "parol" deb eslab qolmaymiz:
                // aks holda sichqoncha bosilguncha shu maydonda hech narsa o'girilmasdi.
                // Javob kutilayotganda fokus o'zgargan bo'lsa (sichqoncha, Enter) — u eski maydonniki, eslab qolmaymiz.
                if (focus == _focusGen) _focusPassword ??= uia;
                bool password = uia ?? true;
                var keys = password ? new List<INPUT>(trailing) : ReplacementKeys(deleteCount, replacement, trailing);
                if (uia == null) Log.Write("AutoType: UI Automation javob bermadi — so'z o'girilmadi, keyingisida qayta tekshiriladi");
                else if (password) Log.Write("AutoType: parol maydoni — so'z o'girilmadi");
                _pending += keys.Count - 1;
                _pendingSince = Environment.TickCount + keys.Count / 2 * (PaceMs + 20);
                _settling++;
                _sendQueue.Add((keys.ToArray(), "almashtirish"));
            }));
        });
    }

    private const int UiaTimeoutMs = 800;
    private System.Threading.Tasks.Task<bool?>? _uiaCheck;

    /// <summary>Fokus o'zgardi (sichqoncha, Tab, boshqa oyna) — parol tekshiruvi qaytadan.</summary>
    private void ForgetFocus()
    {
        _focusPassword = null;
        _uiaCheck = null;
        _focusGen++;
    }

    private int _focusGen; // fokus har o'zgarganda oshadi — kechikkan UIA javobi yangi maydonga yozilmasin

    /// <summary>
    /// UIA tekshiruvini so'zning birinchi harfida boshlaymiz — probelgacha javob tayyor bo'ladi va tugmalar ushlanmaydi
    /// (Qt/Telegram'da birinchi UIA so'rovi sekin: ilova accessibility daraxtini quradi).
    /// </summary>
    private void PrefetchPassword()
    {
        if (_focusPassword == null && _uiaCheck == null)
            _uiaCheck = System.Threading.Tasks.Task.Run(FocusIsPasswordUia);
    }

    /// <summary>UIA: fokusdagi element parol maydonimi. null — javob kelmadi yoki xato (chaqiruvchi bu so'zga tegmaydi).</summary>
    private static bool? FocusIsPasswordUia()
    {
        var t = System.Threading.Tasks.Task.Run(() =>
        {
            var el = System.Windows.Automation.AutomationElement.FocusedElement;
            return el == null || el.Current.IsPassword;
        });
        try { return t.Wait(UiaTimeoutMs) ? t.Result : null; }
        catch (AggregateException) { return null; }
    }

    private const int PasteRestoreDelayMs = 500;

    /// <summary>
    /// Clipboard'dagi matnni o'girib qo'yadi, keyin asl clipboard'ni tiklaydi.
    /// Faqat probel/yangi qatori bor matn: bitta so'z (masalan, nusxalangan parol "shahar123") o'zgarmasin.
    /// Nima bo'lsa ham V yuboriladi — foydalanuvchining paste'i yo'qolmasin.
    /// </summary>
    private void PasteConverted()
    {
        ClipboardSwap.Snapshot? snapshot = null;
        uint ours = 0;
        string? converted = null;
        try
        {
            string? text = _clip.ReadText();
            if (text != null && text.Any(char.IsWhiteSpace))
            {
                List<(string, byte[])>? extras = null;
                string? conv = null;
                bool hasCode = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(CodeGuard.LooksLikeCode);
                if (hasCode && _clip.ReadNamed(TelegramTags.TextFormat) != null)
                {
                    // Telegram'da kod qo'yilyapti — stillarni buzmaslik uchun umuman tegmaymiz (conv = null)
                }
                else if (ConvertEngine.TryTelegram(_clip, out var tgOld, out var tgNew, out var tgTags))
                {
                    text = tgOld;
                    conv = tgNew;
                    extras = new() { (TelegramTags.TextFormat, Encoding.UTF8.GetBytes(tgNew)), (TelegramTags.TagsFormat, tgTags) };
                }
                else conv = CodeGuard.ConvertProse(text); // kod parchasi bo'lsa — identifikatorlarga tegmaydi

                if (conv != null && conv != text)
                {
                    snapshot = _clip.Save();
                    ours = _clip.SetText(conv, extras);
                    if (ours != 0) converted = conv;
                    else snapshot = null;
                }
            }
        }
        catch (Exception ex) { Log.Write("AutoType paste: " + ex.Message); }

        bool ctrl = InputSender.IsDown(VK_CONTROL); // user Ctrl'ni qo'yib yuborgan bo'lsa — o'zimiz bosamiz
        var keys = new List<INPUT>();
        if (!ctrl) keys.Add(Key(VK_CONTROL, false));
        keys.Add(Key(VK_V, false));
        keys.Add(Key(VK_V, true));
        if (!ctrl) keys.Add(Key(VK_CONTROL, true));
        _pending += keys.Count - 1; // BeginPending(1) o'rniga haqiqiy son
        _sendQueue.Add((keys.ToArray(), "paste"));

        if (snapshot == null) return;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PasteRestoreDelayMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            // Boshqa dastur clipboard'ga yangi narsa yozgan bo'lsa — ustidan yozmaymiz.
            if (ClipboardSwap.Sequence == ours || _clip.ReadText() == converted) _clip.Restore(snapshot);
        };
        timer.Start();
    }

    /// <summary>Tugma qaysi belgi(lar)ni yozadi. isDead — dead key (belgi keyingi tugma bilan chiqadi).</summary>
    private string? ToChars(IntPtr fg, KBDLLHOOKSTRUCT k, bool altGr, out bool isDead)
    {
        Array.Clear(_keyState, 0, _keyState.Length);
        if (InputSender.IsDown(VK_SHIFT)) _keyState[VK_SHIFT] = 0x80;
        if ((GetKeyState(VK_CAPITAL) & 1) != 0) _keyState[VK_CAPITAL] = 0x01;
        if (altGr) _keyState[VK_CONTROL] = _keyState[VK_MENU] = _keyState[VK_LCONTROL] = _keyState[VK_RMENU] = 0x80;
        IntPtr hkl = GetKeyboardLayout(GetWindowThreadProcessId(fg, out _));
        _chars.Clear();
        // flag 4: klaviatura holatini o'zgartirmaslik (aks holda foydalanuvchining dead key'lari buziladi)
        int n = ToUnicodeEx(k.vkCode, k.scanCode, _keyState, _chars, _chars.Capacity, 4, hkl);
        isDead = n < 0;
        if (n < 0) return _chars.Length > 0 ? _chars[0].ToString() : null;
        return n > 0 ? _chars.ToString(0, Math.Min(n, _chars.Length)) : null;
    }

    private Mode WindowMode(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return Mode.Off;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Process.GetCurrentProcess().Id) return Mode.Off; // o'z oynalarimiz (hotkey maydoni)
        string name;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            name = p.ProcessName;
        }
        catch (ArgumentException) { return Mode.Off; }
        catch (InvalidOperationException) { return Mode.Off; }
        if (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)) name = HostedAppName(hwnd, pid) ?? name;
        string? off = BlockedProcesses.Contains(name) ? "istisno ro'yxatida" : ConvertEngine.IsTerminalWindow(hwnd) ? "terminal"
            : ConvertEngine.IsElevatedWindow(hwnd) ? "administrator oynasi"
            : _allowed != null && !_allowed.Contains(name) ? "tanlangan dasturlar ichida emas" : null;
        if (off != null)
        {
            Log.Write($"AutoType: {name} — o'chiq ({off})");
            return Mode.Off;
        }
        return CodeEditors.Contains(name) ? Mode.Code : Mode.Prose;
    }

    /// <summary>
    /// UWP ilovalar (Kalkulyator, Pochta) oynasi ApplicationFrameHost'niki — ichidagi haqiqiy ilova jarayonini topamiz,
    /// aks holda user tanlagan ilova tanilmasdi.
    /// </summary>
    private static string? HostedAppName(IntPtr frame, uint hostPid)
    {
        string? found = null;
        EnumChildWindows(frame, (child, _) =>
        {
            GetWindowThreadProcessId(child, out uint pid);
            if (pid == hostPid) return true;
            try { using var p = Process.GetProcessById((int)pid); found = p.ProcessName; }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
            return found == null;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc proc, IntPtr lParam);

    /// <summary>Klassik Win32 parol maydoni (ES_PASSWORD). Brauzerlardagi parol maydonida probel kam bo'ladi.</summary>
    private static bool IsPasswordFocus(IntPtr fg)
    {
        var gti = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(GetWindowThreadProcessId(fg, out _), ref gti) || gti.hwndFocus == IntPtr.Zero) return false;
        const long ES_PASSWORD = 0x20;
        return (GetWindowLongPtr(gti.hwndFocus, -16).ToInt64() & ES_PASSWORD) != 0 &&
               ConvertEngine.ClassNameOf(gti.hwndFocus).IndexOf("edit", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static INPUT Key(int vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)MapVirtualKey((uint)vk, 0), dwFlags = up ? KEYEVENTF_KEYUP : 0, dwExtraInfo = OwnInput } },
    };

    private static INPUT Unicode(char ch, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0), dwExtraInfo = OwnInput } },
    };
}
