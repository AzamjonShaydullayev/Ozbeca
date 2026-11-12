namespace Ozbeca.Bot;

/// <summary>Ixtiyoriy `.env` fayl: KEY=VALUE qatorlari, `#` izoh. Muhitda allaqachon bor o'zgaruvchini almashtirmaydi. Qiymatlar logga yozilmaydi.</summary>
internal static class DotEnv
{
    public static void Load(string path)
    {
        if (File.Exists(path)) Apply(File.ReadAllLines(path), Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable);
    }

    internal static void Apply(IEnumerable<string> lines, Func<string, string?> get, Action<string, string> set)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            if (key.Length == 0 || get(key) != null) continue;
            set(key, line[(eq + 1)..].Trim());
        }
    }
}
