using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Ozbeca.App.Native;

namespace Ozbeca.App;

/// <summary>
/// Win32 clipboard: saqlash / tiklash / matn o'qish-yozish. Userning clipboard'i muqaddas (HANDOFF 9).
/// Barcha usullar o'zi ochib-yopadi; band bo'lsa qisqa pauzalar bilan qayta urinadi.
/// </summary>
internal sealed class ClipboardSwap
{
    // GDI handle formatlari va private/GDI-obj oraliqlari — HGLOBAL emas, nusxa olinmaydi (CF_DIB ularning o'rnini bosadi).
    private static readonly HashSet<uint> GdiFormats = new() { 2, 3, 9, 14, 0x80, 0x82, 0x83, 0x8E };

    // Win+V tarixi va bulutga tushmasligi uchun.
    private static readonly uint[] NoHistoryFormats =
    {
        RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"),
        RegisterClipboardFormat("CanIncludeInClipboardHistory"),
        RegisterClipboardFormat("CanUploadToCloudClipboard"),
    };

    private const string EmptySelectionMarker = "\"isFromEmptySelection\":true";

    private readonly IntPtr _owner;

    public ClipboardSwap(IntPtr owner) => _owner = owner;

    public static uint Sequence => GetClipboardSequenceNumber();

    public sealed record Snapshot(IReadOnlyList<(uint Format, byte[] Data)> Items);

    public Snapshot Save()
    {
        var items = new List<(uint, byte[])>();
        Use(() =>
        {
            for (uint f = EnumClipboardFormats(0); f != 0; f = EnumClipboardFormats(f))
            {
                if (GdiFormats.Contains(f) || f is >= 0x200 and <= 0x3FF) continue;
                var data = ReadBytes(f);
                if (data != null) items.Add((f, data));
            }
        });
        return new Snapshot(items);
    }

    public bool Restore(Snapshot snap) => Use(() =>
    {
        EmptyClipboard();
        foreach (var (format, data) in snap.Items) SetBytes(format, data);
        if (snap.Items.Count > 0) MarkNoHistory(); // asl nusxa tarixda allaqachon bor — dublikat qilmaymiz
    });

    /// <summary>Clipboard'ni bo'shatadi va yangi sequence number'ni qaytaradi (0 — ochib bo'lmadi).</summary>
    public uint Clear() => Use(() => EmptyClipboard()) ? Sequence : 0;

    /// <summary>Sequence number o'zgarguncha kutadi (qat'iy Sleep emas, holatni kuzatish).</summary>
    public static bool WaitForChange(uint seq, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Sequence != seq) return true;
            Thread.Sleep(5);
        }
        return Sequence != seq;
    }

    /// <summary>CF_UNICODETEXT; VS Code'ning bo'sh tanlovdagi butun qator nusxasi null qaytaradi.</summary>
    public string? ReadText()
    {
        string? text = null;
        Use(() =>
        {
            if (IsVsCodeEmptySelection()) { Log.Write("VS Code: bo'sh tanlov (butun qator nusxasi)"); return; }
            var bytes = ReadBytes(CF_UNICODETEXT);
            if (bytes == null) return;
            text = Encoding.Unicode.GetString(bytes);
            int nul = text.IndexOf('\0');
            if (nul >= 0) text = text[..nul];
        });
        return text;
    }

    /// <summary>Nomli formatni (masalan Telegram'ning "application/x-td-field-tags") o'qiydi.</summary>
    public byte[]? ReadNamed(string formatName)
    {
        byte[]? data = null;
        Use(() => data = ReadBytes(RegisterClipboardFormat(formatName)));
        return data;
    }

    /// <summary>
    /// Matnni (va ixtiyoriy nomli formatlarni) tarixga yozmaydigan qilib qo'yadi;
    /// qo'yilgandan keyingi sequence'ni qaytaradi (0 — xato).
    /// </summary>
    public uint SetText(string text, IReadOnlyList<(string Format, byte[] Data)>? extras = null)
    {
        bool ok = Use(() =>
        {
            EmptyClipboard();
            SetBytes(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + "\0"));
            foreach (var (format, data) in extras ?? Array.Empty<(string, byte[])>())
                SetBytes(RegisterClipboardFormat(format), data);
            MarkNoHistory();
        });
        return ok ? Sequence : 0;
    }

    // ---- ichki ----

    private bool Use(Action body)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(_owner))
            {
                try { body(); return true; }
                finally { CloseClipboard(); }
            }
            Thread.Sleep(20);
        }
        Log.Write("OpenClipboard: band, urinishlar tugadi");
        return false;
    }

    private bool IsVsCodeEmptySelection()
    {
        var name = new StringBuilder(256);
        for (uint f = EnumClipboardFormats(0); f != 0; f = EnumClipboardFormats(f))
        {
            if (f < 0xC000) continue;
            name.Clear();
            if (GetClipboardFormatName(f, name, name.Capacity) == 0) continue;
            var n = name.ToString();
            // Electron maxsus formatlarni ba'zan alohida, ba'zan Chromium "pickle" ichida saqlaydi.
            if (!n.Contains("vscode-editor-data") && n != "Chromium Web Custom MIME Data Format") continue;
            var bytes = ReadBytes(f);
            if (bytes == null) continue;
            if (Encoding.Unicode.GetString(bytes).Contains(EmptySelectionMarker) ||
                Encoding.UTF8.GetString(bytes).Contains(EmptySelectionMarker))
                return true;
        }
        return false;
    }

    private static byte[]? ReadBytes(uint format)
    {
        IntPtr h = GetClipboardData(format);
        if (h == IntPtr.Zero) return null;
        int size = (int)GlobalSize(h).ToUInt64();
        if (size == 0) return null;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) return null;
        try
        {
            var data = new byte[size];
            Marshal.Copy(p, data, 0, size);
            return data;
        }
        finally { GlobalUnlock(h); }
    }

    private static void SetBytes(uint format, byte[] data)
    {
        IntPtr h = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)Math.Max(data.Length, 1));
        if (h == IntPtr.Zero) return;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) { GlobalFree(h); return; }
        Marshal.Copy(data, 0, p, data.Length);
        GlobalUnlock(h);
        if (SetClipboardData(format, h) == IntPtr.Zero) GlobalFree(h); // muvaffaqiyatda egalik tizimga o'tadi
    }

    private static void MarkNoHistory()
    {
        foreach (var f in NoHistoryFormats) SetBytes(f, BitConverter.GetBytes(0));
    }
}
