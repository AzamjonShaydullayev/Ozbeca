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
        try { File.Delete(Exe + ":Zone.Identifier"); } catch (IOException) { }
    }

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
