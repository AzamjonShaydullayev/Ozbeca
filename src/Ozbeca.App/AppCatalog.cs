using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

namespace Ozbeca.App;

/// <summary>Kompyuterdagi dastur: ko'rinadigan nomi, jarayon nomi (Telegram, WINWORD, Notepad) va ikonkasi.</summary>
internal sealed class InstalledApp
{
    public string Name { get; init; } = "";
    public string Process { get; init; } = "";
    public ImageSource? Icon { get; init; }
}

/// <summary>
/// Windows'ning "Barcha ilovalar" ro'yxati (shell:AppsFolder) — Start menyudagi hamma narsa, Store ilovalari ham.
/// Har biri jarayon nomiga aylantiriladi: oddiy dastur — yorliq ko'rsatgan exe, Store ilovasi — AppxManifest'dagi Executable.
/// </summary>
internal static class AppCatalog
{
    private static Task<List<InstalledApp>>? _task;

    /// <summary>Joriy yig'ish (yoki tayyor natija). Hali boshlanmagan bo'lsa — boshlaydi.</summary>
    public static Task<List<InstalledApp>> LoadAsync() => _task ?? Refresh();

    /// <summary>Ro'yxatni qaytadan yig'ishni boshlaydi (sozlamalar ochilganda — yangi o'rnatilgan dasturlar ham chiqsin).</summary>
    public static Task<List<InstalledApp>> Refresh()
    {
        if (_task is { IsCompleted: false }) return _task; // yig'ilyapti — kutamiz
        var tcs = new TaskCompletionSource<List<InstalledApp>>();
        var t = new Thread(() =>
        {
            try { tcs.SetResult(Load()); }
            catch (Exception ex)
            {
                Log.Write("Dasturlar ro'yxati olinmadi: " + ex.Message);
                tcs.SetResult(new List<InstalledApp>());
            }
        }) { IsBackground = true, Name = "OzbecaAppCatalog" };
        t.SetApartmentState(ApartmentState.STA); // Shell COM
        t.Start();
        return _task = tcs.Task;
    }

    private static List<InstalledApp> Load()
    {
        var byProcess = new Dictionary<string, (string Name, string Aumid)>(StringComparer.OrdinalIgnoreCase);
        var shellType = Type.GetTypeFromProgID("Shell.Application")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            foreach (dynamic item in folder.Items())
            {
                try
                {
                    string name = item.Name ?? "";
                    string aumid = item.Path ?? "";
                    string? process = ProcessOf(item, aumid);
                    if (process == null || name.Length == 0 || IsJunk(name, process)) continue;
                    // Bir exe'ga bir nechta yorliq (Chrome va uning ilovalari) — eng qisqa nomlisi qoladi.
                    if (byProcess.TryGetValue(process, out var had) && had.Name.Length <= name.Length) continue;
                    byProcess[process] = (name, aumid);
                }
                catch (Exception ex) { Log.Write("Dastur o'qilmadi: " + ex.Message); } // bittasi buzuq — qolganlari chiqsin
            }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
        return byProcess
            .Select(p => new InstalledApp { Name = p.Value.Name, Process = p.Key, Icon = IconOf(p.Value.Aumid) })
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string? ProcessOf(dynamic item, string aumid)
    {
        if (item.ExtendedProperty("System.Link.TargetParsingPath") is string target &&
            target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileNameWithoutExtension(target);

        // Store ilovasi: Oila!AppId → paket papkasidagi AppxManifest.xml → <Application Id=AppId Executable=...>
        int bang = aumid.IndexOf('!');
        if (bang < 0 || item.ExtendedProperty("System.AppUserModel.PackageInstallPath") is not string root) return null;
        try
        {
            var doc = new XmlDocument();
            doc.Load(Path.Combine(root, "AppxManifest.xml"));
            string appId = aumid.Substring(bang + 1);
            foreach (XmlElement app in doc.GetElementsByTagName("Application"))
            {
                string exe = app.GetAttribute("Executable");
                if (app.GetAttribute("Id") == appId && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && exe.IndexOf('$') < 0)
                    return Path.GetFileNameWithoutExtension(exe);
            }
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException) { }
        return null;
    }

    /// <summary>O'chiruvchi/o'rnatuvchilar, o'zimiz va avto-o'girish umuman ishlamaydigan joylar (terminal va b.) ko'rsatilmaydi.</summary>
    private static bool IsJunk(string name, string process) =>
        process.StartsWith("unins", StringComparison.OrdinalIgnoreCase) ||
        process.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0 ||
        process.Equals("Ozbeca", StringComparison.OrdinalIgnoreCase) ||
        AutoType.BlockedProcesses.Contains(process);

    private static ImageSource? IconOf(string aumid)
    {
        IntPtr pidl = IntPtr.Zero;
        try
        {
            if (SHParseDisplayName(@"shell:AppsFolder\" + aumid, IntPtr.Zero, out pidl, 0, out _) != 0) return null;
            var info = new SHFILEINFO();
            const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0, SHGFI_PIDL = 0x8;
            if (SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_PIDL) == IntPtr.Zero ||
                info.hIcon == IntPtr.Zero) return null;
            try
            {
                var img = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                img.Freeze(); // UI thread'ga o'tishi uchun
                return img;
            }
            finally { DestroyIcon(info.hIcon); }
        }
        catch (Exception) { return null; }
        finally { if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindCtx, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attr, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
