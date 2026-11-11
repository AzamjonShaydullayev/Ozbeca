using System.Windows.Interop;

namespace Ozbeca.App;

/// <summary>RegisterHotKey + yashirin message-only oyna. Hotkey uchun hook kerak emas (hook faqat AutoType'da).</summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x0E8A;
    private readonly HwndSource _source;
    private bool _registered;

    public event Action? Pressed;

    /// <summary>Clipboard egasi sifatida ham shu oyna ishlatiladi.</summary>
    public IntPtr Handle => _source.Handle;

    public HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("OzbecaHotkey") { ParentWindow = new IntPtr(-3) }); // HWND_MESSAGE
        _source.AddHook(WndProc);
    }

    /// <summary>modifiers — Native.MOD_*; false qaytsa kombinatsiya boshqa dasturda band.</summary>
    public bool Register(uint modifiers, uint vk)
    {
        Unregister();
        _registered = Native.RegisterHotKey(Handle, HotkeyId, modifiers | Native.MOD_NOREPEAT, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered) Native.UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.Dispose();
    }

    /// <summary>Ko'rsatish uchun: "Ctrl+Alt+D1" → Ctrl, Alt, 1 (WPF raqam tugmasini "D1" deb ataydi); Space → Probel.</summary>
    public static string[] Caps(string combo) => combo.Split('+').Select(k => k.Trim()).Select(k =>
        k == "Space" ? "Probel" : k.Length == 2 && k[0] == 'D' && char.IsDigit(k[1]) ? k.Substring(1) : k).ToArray();

    /// <summary>"Ctrl+Alt+Space" → (MOD_*, VK). Noto'g'ri bo'lsa false.</summary>
    public static bool TryParse(string text, out uint modifiers, out int vk)
    {
        modifiers = 0;
        vk = 0;
        foreach (var raw in text.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= Native.MOD_CONTROL; break;
                case "alt": modifiers |= Native.MOD_ALT; break;
                case "shift": modifiers |= Native.MOD_SHIFT; break;
                case "win": modifiers |= Native.MOD_WIN; break;
                default:
                    if (vk != 0 || !Enum.TryParse(raw, true, out System.Windows.Input.Key key)) return false;
                    vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return vk != 0 && modifiers != 0;
    }
}
