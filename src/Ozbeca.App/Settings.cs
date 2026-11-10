using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace Ozbeca.App;

/// <summary>%AppData%\Ozbeca\settings.json</summary>
internal sealed class Settings
{
    public string Hotkey { get; set; } = "Ctrl+Alt+O"; // Ctrl+Alt+Space ishlab chiquvchi kompyuterida band chiqdi (2026-11-10 user qarori)
    public bool Enabled { get; set; } = true;
    public bool ShowToast { get; set; } = true;
    public int RestoreDelayMs { get; set; } = 400;

    private static readonly string FilePath = Path.Combine(Log.Dir, "settings.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                s.RestoreDelayMs = Math.Clamp(s.RestoreDelayMs, 0, 5000);
                return s;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Write("settings.json o'qilmadi, standart sozlamalar: " + ex.Message);
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
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
    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string v && string.Equals(v, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(Name, Command);
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
