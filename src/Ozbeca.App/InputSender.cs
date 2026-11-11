using System.Diagnostics;
using System.Runtime.InteropServices;
using static Ozbeca.App.Native;

namespace Ozbeca.App;

/// <summary>SendInput orqali tugmalar yuborish va modifier'larni tozalash.</summary>
internal static class InputSender
{
    private static readonly int[] Modifiers = { VK_CONTROL, VK_MENU, VK_SHIFT, VK_LWIN, VK_RWIN };

    /// <summary>Hotkey oqimi yuboradigan tugmalar belgisi — avto-yozish ularga tegmaydi (Ctrl+V ni "user paste" deb o'ylamasin).</summary>
    public static readonly IntPtr EngineInput = new(0x0E8A0E8C);

    public static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>Alt menyusi faollashmasligi uchun zararsiz tayinlanmagan tugma (AutoHotkey usuli).</summary>
    public static void SendMask() => Send(Key(VK_MASK, false), Key(VK_MASK, true));

    /// <summary>
    /// Modifier'lar va hotkey tugmasi qo'yib yuborilishini kutadi. Vaqt tugasa — sun'iy key-up.
    /// Aks holda oyna Ctrl+C o'rniga Ctrl+Alt+C oladi.
    /// </summary>
    public static void WaitKeysReleased(int hotkeyVk, int timeoutMs = 1000)
    {
        var keys = Modifiers.Append(hotkeyVk).Distinct().ToArray();
        var sw = Stopwatch.StartNew();
        while (keys.Any(IsDown) && sw.ElapsedMilliseconds < timeoutMs)
            Thread.Sleep(10);

        var stuck = keys.Where(IsDown).ToArray();
        if (stuck.Length == 0) return;
        Log.Write($"Tugmalar qo'yib yuborilmadi, sun'iy key-up: {string.Join(",", stuck.Select(k => $"0x{k:X2}"))}");
        SendMask(); // Win/Alt key-up Start yoki menyuni ochmasligi uchun
        Send(stuck.Select(k => Key(k, true)).ToArray());
    }

    /// <summary>modifier + key (masalan Ctrl+C, Shift+Insert).</summary>
    public static bool Chord(int modifierVk, int keyVk) =>
        Send(Key(modifierVk, false), Key(keyVk, false), Key(keyVk, true), Key(modifierVk, true));

    private static INPUT Key(int vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (vk is VK_INSERT or VK_RWIN or VK_LWIN) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)MapVirtualKey((uint)vk, 0), dwFlags = flags, dwExtraInfo = EngineInput } },
        };
    }

    private static bool Send(params INPUT[] inputs) =>
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
}
