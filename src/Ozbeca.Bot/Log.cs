namespace Ozbeca.Bot;

/// <summary>Minimal log (journald stdout/stderr'ni yig'adi). Foydalanuvchi matni va token hech qachon yozilmaydi.</summary>
internal static class Log
{
    public static void Info(string msg) => Console.Out.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} INFO {msg}");
    public static void Error(string msg) => Console.Error.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} ERROR {msg}");
}
