using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Ozbeca.App;

/// <summary>
/// Birinchi ishga tushirishdagi "o'rnatish": exe'ni doimiy papkaga ko'chirish va yorliqlar.
/// Installer yo'q — dasturning o'zi bajaradi. Hammasi foydalanuvchi profilida, administrator kerak emas.
/// </summary>
internal static class Install
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ozbeca");
    public static readonly string Exe = Path.Combine(Dir, "Ozbeca.exe");

    public static string DesktopLink => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Özbeça.lnk");
    public static string StartMenuLink => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Özbeça.lnk");

    public static bool IsInstalledCopy =>
        string.Equals(Path.GetFullPath(Compat.ProcessPath!), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase);

    public static void CopySelf()
    {
        Directory.CreateDirectory(Dir);
        File.Copy(Compat.ProcessPath!, Exe, overwrite: true);
        // Internetdan yuklangan belgisi (Zone.Identifier) nusxaga ham o'tadi — user faylni allaqachon ochgan,
        // qayta SmartScreen so'ramasligi uchun olib tashlaymiz (installerlar ham shunday qiladi).
        Native.DeleteFile(Exe + ":Zone.Identifier"); // oqim bo'lmasa — shunchaki false qaytaradi
    }

    /// <summary>Ishlab turgan nusxaga "yopil" — yangilashda (eski versiyalarda yo'q, ular to'xtatiladi).</summary>
    public const string ExitEvent = @"Local\Ozbeca.Exit";

    /// <summary>Eski nusxa yopilib, yangisi o'rnini egalladi — ishga tushgach "yangilandi" deb aytiladi.</summary>
    public static bool Updated { get; private set; }

    /// <summary>
    /// Yangi versiya exe ishga tushirildi (masalan, Telegram/Downloads'dan), eskisi o'rnatilgan va/yoki ishlab turibdi:
    /// eskisini yopib, o'rnini egallaymiz — foydalanuvchi hech narsani qo'lda o'chirmasin. Sozlamalar
    /// (%AppData%), yorliqlar va autostart joyida qoladi.
    /// true — o'rnatilgan nusxa yangilanib ishga tushirildi, bu nusxa yopilsin.
    /// </summary>
    public static bool UpdateOlder()
    {
        if (IsInstalledCopy) return false;
        Version ours = typeof(Install).Assembly.GetName().Version!;
        var others = OtherInstances();
        // Bir xil yoki yangiroq versiya ishlab turibdi — eski xatti-harakat (u nusxada sozlamalar ochiladi).
        if (others.Any(o => !(VersionOf(o.Path) < ours))) return false;
        bool installedOlder = File.Exists(Exe) && VersionOf(Exe) < ours;
        if (!installedOlder && others.Count == 0) return false;

        Log.Write($"Yangilash {ours}: eski nusxalar {others.Count}, o'rnatilgani eski={installedOlder}");
        foreach (var o in others) Stop(o.Process);
        Updated = true;
        if (!installedOlder)
        {
            // O'rnatilmagan (boshqa papkadan ishlagan) eski nusxa yopildi — endi shu nusxa ishlaydi.
            if (Autostart.Exists) Autostart.Set(true);
            return false;
        }

        for (int i = 0; ; i++)
        {
            try { CopySelf(); break; }
            catch (IOException) when (i < 10) { Thread.Sleep(300); } // fayl hali band (antivirus, yopilayotgan jarayon)
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Exe, UpdatedArg) { UseShellExecute = true });
        return true;
    }

    public const string UpdatedArg = "--updated";

    private static List<(System.Diagnostics.Process Process, string Path)> OtherInstances()
    {
        int self = System.Diagnostics.Process.GetCurrentProcess().Id;
        var list = new List<(System.Diagnostics.Process, string)>();
        foreach (var p in System.Diagnostics.Process.GetProcesses())
        {
            // "Ozbeca", "Ozbeca (1)" (brauzer qayta yuklaganda) — exe ichidagi asl nom bilan tasdiqlanadi.
            if (p.Id == self || !p.ProcessName.StartsWith("Ozbeca", StringComparison.OrdinalIgnoreCase) ||
                PathOf(p) is not string path ||
                FileVersionInfo(path)?.OriginalFilename?.StartsWith("Ozbeca", StringComparison.OrdinalIgnoreCase) != true)
            {
                p.Dispose();
                continue;
            }
            list.Add((p, path));
        }
        return list;
    }

    private static void Stop(System.Diagnostics.Process p)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ExitEvent, out var ev)) { ev.Set(); ev.Dispose(); }
            if (!p.WaitForExit(3000)) { p.Kill(); p.WaitForExit(3000); } // v1.2.0 va eskilarida "yopil" signali yo'q
        }
        catch (Exception ex) { Log.Write("Eski nusxa to'xtatilmadi: " + ex.Message); }
        finally { p.Dispose(); }
    }

    private static string? PathOf(System.Diagnostics.Process p)
    {
        IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)p.Id);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return Native.QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { Native.CloseHandle(h); }
    }

    private static System.Diagnostics.FileVersionInfo? FileVersionInfo(string path)
    {
        try { return System.Diagnostics.FileVersionInfo.GetVersionInfo(path); }
        catch (Exception) { return null; }
    }

    /// <summary>O'qilmasa — 0.0 (eski deb hisoblanadi).</summary>
    private static Version VersionOf(string path) =>
        Version.TryParse(FileVersionInfo(path)?.FileVersion, out var v) ? v : new Version(0, 0);

    /// <summary>
    /// IShellLinkW (Unicode). WScript.Shell ishlatilmaydi: u nomni ANSI ga o'giradi ("Özbeça" → "Ozbeca")
    /// va kirillcha foydalanuvchi papkasida (C:\Users\Азамжон) yorliq noto'g'ri joyga ko'rsatadi.
    /// </summary>
    public static void CreateShortcut(string linkPath, string targetExe)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(targetExe);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetExe)!);
            link.SetIconLocation(targetExe, 0);
            link.SetDescription("Özbeça — belgilangan matnni yangi o'zbek alifbosiga o'giradi");
            ((IPersistFile)link).Save(linkPath, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    // Metodlar tartibi shobjidl.h dagi IShellLinkW bilan aynan bir xil bo'lishi shart.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int cch, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
