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
/// - Enter'da o'girilmaydi: parol odatda Enter bilan tugaydi (choy1990 + Enter → çoy1990 bo'lib qolmasin).
/// - Win32 parol maydoni, terminal, kod muharriri, parol menejeri, masofaviy ish stoli, admin oynada o'chiq.
/// - Sichqoncha bosilsa, strelka/Home/End/Tab/Esc yoki boshqa oyna — so'z unutiladi (noto'g'ri joyni almashtirmaslik uchun).
/// </summary>
internal sealed class AutoType : IDisposable
{
    private static readonly HashSet<string> BlockedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // kod muharrirlari (Enter/Tab avtoto'ldirish, kod buziladi)
        "Code", "Code - Insiders", "Cursor", "devenv", "rider64", "idea64", "pycharm64", "webstorm64", "phpstorm64",
        "clion64", "goland64", "studio64", "sublime_text", "zed",
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
    private bool _allowed;
    private readonly byte[] _keyState = new byte[256];
    private readonly StringBuilder _chars = new(8);

    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    public AutoType()
    {
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _keyProc = OnKey;
        _mouseProc = OnMouse;
    }

    public bool IsRunning => _keyHook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning) return;
        IntPtr module = GetModuleHandle(null);
        _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        _word.Reset();
        _lastWindow = IntPtr.Zero;
        if (_sender == null)
        {
            _sender = new Thread(SenderLoop) { IsBackground = true, Name = "OzbecaAutoTypeSender" };
            _sender.Start();
        }
        Log.Write(IsRunning ? "Yozish paytida o'girish yoqildi" : "Hook o'rnatilmadi: " + Marshal.GetLastWin32Error());
    }

    public void Stop()
    {
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyHook = _mouseHook = IntPtr.Zero;
        _word.Reset();
    }

    public void Dispose() => Stop();

    private IntPtr OnMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = wParam.ToInt32();
        if (nCode >= 0 && (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)) _word.Reset();
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
    private const int PendingTimeoutMs = 300;

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
#if DEBUG
        Log.Write($"[debug] {(up ? "up  " : "down")} vk=0x{k.vkCode:X2} inj={injected} extra=0x{k.dwExtraInfo.ToInt64():X} pending={_pending} held={_held.Count}");
#endif
        try
        {
            if (injected && k.dwExtraInfo == OwnInput)
            {
                if (_pending > 0 && --_pending == 0) FlushHeld();
                return CallNextHookEx(_keyHook, nCode, wParam, lParam);
            }

            bool replay = injected && k.dwExtraInfo == ReplayInput;
            if (replay)
            {
                if (_pending > 0 && --_pending == 0) FlushHeld(); // qayta yuborish paytida yana ushlanganlar
            }
            else if (_pending > 0 || _held.Count > 0) // navbatda kutayotganlar bo'lsa — ulardan oldin o'tmasin
            {
                _held.Add((k, up));
                return new IntPtr(1); // keyinroq tartib bilan qaytariladi
            }

            // Qayta yuborilayotgan tugmalar ichida yangi almashtirish qilinmaydi (tartib yana buzilmasin).
            if (down && HandleKeyDown(k, allowReplace: !replay)) return new IntPtr(1);
        }
        catch (Exception ex)
        {
            _word.Reset();
            Log.Write("AutoType xato: " + ex.Message);
        }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

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
                    FlushHeld();
                }
                if (_pending == 0 && _held.Count == 0) _guard!.Stop();
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
        SendDeferred(inputs, "ushlangan tugmalar");
    }

    /// <summary>
    /// Hook ichida yuborilgan tugmalar hook'lardan o'tmaydi — shuning uchun hook'dan chiqqandan keyin yuboramiz.
    /// Ular hook'dan o'tib bo'lguncha (pending) foydalanuvchining yangi tugmalari ushlab turiladi.
    /// </summary>
    private void SendDeferred(INPUT[] inputs, string what)
    {
        BeginPending(inputs.Length, inputs.Length / 2 * PaceMs);
        _sendQueue.Add((inputs, what));
    }

    // Windows 11 Notepad kabi ilovalar bir zumda kelgan Unicode harflar tartibini almashtirib yuboradi
    // ("shahar" → "şaha r"). Shuning uchun har bir tugma (bosish+qo'yish) orasida kichik pauza — alohida
    // thread'da, UI va hook'ni bloklamaslik uchun. Navbat yagona: almashtirish va qayta yuborish tartibi saqlanadi.
    private const int PaceMs = 4;
    private readonly System.Collections.Concurrent.BlockingCollection<(INPUT[] Inputs, string What)> _sendQueue = new();
    private Thread? _sender;

    private void SenderLoop()
    {
        int size = Marshal.SizeOf<INPUT>();
        foreach (var (inputs, what) in _sendQueue.GetConsumingEnumerable())
        {
            int sent = 0;
            for (int i = 0; i < inputs.Length; i += 2)
            {
                int n = Math.Min(2, inputs.Length - i);
                var pair = new INPUT[n];
                Array.Copy(inputs, i, pair, 0, n);
                sent += (int)SendInput((uint)n, pair, size);
                Thread.Sleep(PaceMs);
            }
            if (sent == inputs.Length) continue;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                Log.Write($"AutoType: {what} to'liq yuborilmadi ({sent}/{inputs.Length})");
                _pending = 0;
                FlushHeld();
            }));
        }
    }

    /// <summary>true — tugma ushlab qolindi (o'rniga o'zimiz yubordik).</summary>
    private bool HandleKeyDown(KBDLLHOOKSTRUCT k, bool allowReplace)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg != _lastWindow)
        {
            _lastWindow = fg;
            _allowed = IsAllowedWindow(fg);
            _word.Reset();
        }
        if (!_allowed) return false;

        int vk = (int)k.vkCode;
        // Modifier'lar, NumLock/ScrollLock/Pause, ovoz va media tugmalari kursorni siljitmaydi — so'zga ta'sir qilmaydi.
        if (vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_CAPITAL or VK_LWIN or VK_RWIN or 0x13 or 0x90 or 0x91
            or >= 0xA0 and <= 0xA5 or >= 0xA6 and <= 0xB7)
            return false;

        // LL-hook'da asinxron holat voqealar tartibida yangilanadi (ushlangan tugmalar uni o'zgartirmaydi).
        if (InputSender.IsDown(VK_CONTROL) || InputSender.IsDown(VK_MENU) || InputSender.IsDown(VK_LWIN) || InputSender.IsDown(VK_RWIN))
        {
            _word.Reset(); // Ctrl+V, Ctrl+Z, Alt+Tab... — matn qayerda ekanini endi bilmaymiz
            return false;
        }

        switch (vk)
        {
            case VK_BACK:
                _word.Backspace();
                return false;
            case VK_SPACE:
                if (allowReplace) return CompleteWord(fg);
                _word.Reset();
                return false;
            case VK_RETURN: // ataylab o'girilmaydi — parollar Enter bilan tugaydi
            case VK_TAB:
            case VK_ESCAPE:
            case VK_DELETE:
            case VK_INSERT:
            case >= 0x21 and <= 0x28: // PgUp, PgDn, End, Home, strelkalar
                _word.Reset();
                return false;
        }

        char? c = ToChar(fg, k);
#if DEBUG
        Log.Write($"[debug] vk=0x{vk:X2} char={(c is char d ? d.ToString() : "null")} buf='{_word.Current}'");
#endif
        if (c is char ch && !char.IsControl(ch) && !char.IsWhiteSpace(ch)) _word.Add(ch);
        else _word.Reset();
        return false;
    }

    private bool CompleteWord(IntPtr fg)
    {
        if (IsPasswordFocus(fg)) { _word.Reset(); return false; }
        if (!_word.TryComplete(out int deleteCount, out string replacement)) return false;

        // Backspace × so'z uzunligi, keyin yangi so'z va probel (Unicode sifatida — klaviatura tartibiga bog'liq emas).
        var inputs = new List<INPUT>(deleteCount * 2 + replacement.Length * 2 + 2);
        for (int i = 0; i < deleteCount; i++)
        {
            inputs.Add(Key(VK_BACK, false));
            inputs.Add(Key(VK_BACK, true));
        }
        foreach (char ch in replacement + " ")
        {
            inputs.Add(Unicode(ch, false));
            inputs.Add(Unicode(ch, true));
        }
#if DEBUG
        Log.Write($"[debug] almashtirish: del={deleteCount} -> '{replacement}' ({inputs.Count} tugma)");
#endif
        SendDeferred(inputs.ToArray(), "almashtirish");
        return true;
    }

    private char? ToChar(IntPtr fg, KBDLLHOOKSTRUCT k)
    {
        Array.Clear(_keyState, 0, _keyState.Length);
        if (InputSender.IsDown(VK_SHIFT)) _keyState[VK_SHIFT] = 0x80;
        if ((GetKeyState(VK_CAPITAL) & 1) != 0) _keyState[VK_CAPITAL] = 0x01;
        IntPtr hkl = GetKeyboardLayout(GetWindowThreadProcessId(fg, out _));
        _chars.Clear();
        // flag 4: klaviatura holatini o'zgartirmaslik (aks holda dead key'lar buziladi)
        int n = ToUnicodeEx(k.vkCode, k.scanCode, _keyState, _chars, _chars.Capacity, 4, hkl);
        return n == 1 ? _chars[0] : null;
    }

    private static bool IsAllowedWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Process.GetCurrentProcess().Id) return false; // o'z oynalarimiz (hotkey maydoni)
        try
        {
            using var p = Process.GetProcessById((int)pid);
            if (BlockedProcesses.Contains(p.ProcessName)) return false;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        return !ConvertEngine.IsTerminalWindow(hwnd) && !ConvertEngine.IsElevatedWindow(hwnd);
    }

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
