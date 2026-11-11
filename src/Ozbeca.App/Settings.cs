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

    // DataContract konstruktorni chaqirmaydi — faylda yo'q maydonlar standart qiymatda qolsin.
    [OnDeserializing]
    private void SetDefaults(StreamingContext _)
    {
        Hotkey = "Ctrl+Alt+O";
        Enabled = true;
        ShowToast = true;
        RestoreDelayMs = 400;
        AutoType = true;
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
                using var f = File.OpenRead(FilePath);
                var s = (Settings?)Json.ReadObject(f) ?? new Settings();
                s.Hotkey ??= "Ctrl+Alt+O";
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

/// <summary>HKCU\...\Run orqali Windows bilan birga ishga tushirish.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Ozbeca";
    private static string Command => Quote(Compat.ProcessPath!);
    private static string Quote(string path) => $"\"{path}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string v && string.Equals(v, Command, StringComparison.OrdinalIgnoreCase);
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
