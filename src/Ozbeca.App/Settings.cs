using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace Ozbeca.App;

/// <summary>%AppData%\Ozbeca\settings.json</summary>
[DataContract]
internal sealed class Settings
{
    [DataMember] public string Hotkey { get; set; } = "Ctrl+Alt+O"; // Ctrl+Alt+Space ishlab chiquvchi kompyuterida band chiqdi (2026-11-10 user qarori)
    [DataMember] public bool Enabled { get; set; } = true;
    [DataMember] public bool ShowToast { get; set; } = true;
    [DataMember] public int RestoreDelayMs { get; set; } = 400;

    /// <summary>Yozish paytida avtomatik o'girish (probel bosilganda). Standart — yoqilgan (user qarori, 2026-11-11).</summary>
    [DataMember] public bool AutoType { get; set; } = true;

    /// <summary>Tutuqsiz so'zlarni lug'at bo'yicha tuzatish (ozbek → özbek). Standart — yoqilgan (user qarori, 2026-11-11).</summary>
    [DataMember] public bool RestoreApostrophes { get; set; } = true;

    /// <summary>Avto-o'girish faqat shu dasturlarda (jarayon nomi). Bo'sh — hamma dasturlarda.</summary>
    [DataMember] public List<AllowedApp> AutoTypeApps { get; set; } = new();

    /// <summary>Yozish to'xtaganda (1 s) tugamagan so'zni o'girish. Standart — yoqilgan; tray'dan o'chiriladi.</summary>
    [DataMember] public bool PauseConvert { get; set; } = true;

    /// <summary>Ctrl+V bilan qo'yilgan matnni o'girish. Standart — yoqilgan; tray'dan o'chiriladi.</summary>
    [DataMember] public bool PasteConvert { get; set; } = true;

    /// <summary>Teskari o'girish (ö → o', ş → sh): yangi harflarni qabul qilmaydigan saytlar uchun.</summary>
    [DataMember] public string ReverseHotkey { get; set; } = "Ctrl+Alt+Shift+O";

    /// <summary>Hech qachon o'girilmaydigan so'zlar: ismlar, brendlar, loginlar.</summary>
    [DataMember] public List<string> Exceptions { get; set; } = new();

    /// <summary>Inglizcha so'z va gapga tegmaslik (check, "how are you shop").</summary>
    [DataMember] public bool EnglishGuard { get; set; } = true;

    /// <summary>Dastur bo'yicha rejim (tray → "Shu dastur uchun"). Yo'q — avtomatik.</summary>
    [DataMember] public List<AppMode> AppModes { get; set; } = new();

    /// <summary>GitHub'dan yangi versiyani tekshirish. Standart o'chiq: dastur o'zi internetga chiqmaydi.</summary>
    [DataMember] public bool CheckUpdates { get; set; }

    // DateTime emas: DataContractJsonSerializer MinValue'ni UTC+5 da yozolmaydi (ArgumentOutOfRange).
    [DataMember] public long LastUpdateCheckUtcTicks { get; set; }

    // DataContract konstruktorni chaqirmaydi — faylda yo'q maydonlar standart qiymatda qolsin.
    [OnDeserializing]
    private void SetDefaults(StreamingContext _)
    {
        Hotkey = "Ctrl+Alt+O";
        Enabled = true;
        ShowToast = true;
        RestoreDelayMs = 400;
        AutoType = true;
        RestoreApostrophes = true;
        AutoTypeApps = new();
        PauseConvert = true;
        PasteConvert = true;
        ReverseHotkey = "Ctrl+Alt+Shift+O";
        Exceptions = new();
        EnglishGuard = true;
        AppModes = new();
    }

    /// <summary>settings.json hali yo'q — birinchi ishga tushirish (o'rnatish oynasi ko'rsatiladi).</summary>
    public bool FirstRun { get; private set; }

    private static readonly string FilePath = Path.Combine(Log.Dir, "settings.json");
    private static readonly DataContractJsonSerializer Json = new(typeof(Settings));

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                // File.ReadAllText BOM'ni olib tashlaydi (Notepad'da saqlangan fayl BOM bilan bo'lishi mumkin —
                // DataContractJsonSerializer uni ko'rsa xato beradi).
                using var f = new MemoryStream(new UTF8Encoding(false).GetBytes(File.ReadAllText(FilePath)));
                var s = (Settings?)Json.ReadObject(f) ?? new Settings();
                s.Hotkey ??= "Ctrl+Alt+O";
                s.AutoTypeApps ??= new();
                s.AutoTypeApps.RemoveAll(a => a == null || string.IsNullOrWhiteSpace(a.Process));
                s.ReverseHotkey ??= "Ctrl+Alt+Shift+O";
                s.Exceptions ??= new();
                s.Exceptions.RemoveAll(string.IsNullOrWhiteSpace);
                s.AppModes ??= new();
                s.AppModes.RemoveAll(m => m == null || string.IsNullOrWhiteSpace(m.Process) || m.Mode is not ("prose" or "code" or "off"));
                s.RestoreDelayMs = Math.Max(0, Math.Min(5000, s.RestoreDelayMs));
                return s;
            }
        }
        catch (Exception ex) when (ex is IOException or SerializationException or UnauthorizedAccessException)
        {
            Log.Write("settings.json o'qilmadi, standart sozlamalar: " + ex.Message);
        }
        return new Settings { FirstRun = !File.Exists(FilePath) };
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.Dir);
            using var f = File.Create(FilePath);
            using var w = JsonReaderWriterFactory.CreateJsonWriter(f, Encoding.UTF8, ownsStream: false, indent: true);
            Json.WriteObject(w, this);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("settings.json saqlanmadi: " + ex.Message);
        }
    }
}

/// <summary>Tanlangan dastur: nomi (sozlamalarda ko'rsatish uchun) va jarayon nomi (tekshirish uchun).</summary>
[DataContract]
internal sealed class AllowedApp
{
    [DataMember] public string Name { get; set; } = "";
    [DataMember] public string Process { get; set; } = "";
}

/// <summary>Dastur uchun tanlangan rejim: "prose" (matn), "code" (faqat qo'shtirnoq ichi), "off".</summary>
[DataContract]
internal sealed class AppMode
{
    [DataMember] public string Process { get; set; } = "";
    [DataMember] public string Mode { get; set; } = "";
}

/// <summary>HKCU\...\Run orqali Windows bilan birga ishga tushirish.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Ozbeca";
    private static string Command => Quote(Compat.ProcessPath);
    private static string Quote(string path) => $"\"{path}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string v && string.Equals(v, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Qaysi exe'ga bo'lsa ham yozuv bor (yangilashda yangi nusxaga ko'chiriladi).</summary>
    public static bool Exists
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) != null;
        }
    }

    /// <param name="exePath">o'rnatishda — yangi joydagi exe; aks holda joriy jarayon.</param>
    public static void Set(bool enabled, string? exePath = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(Name, exePath == null ? Command : Quote(exePath));
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
