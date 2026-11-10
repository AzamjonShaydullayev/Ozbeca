using System.IO;

namespace Ozbeca.App;

/// <summary>%AppData%\Ozbeca\ozbeca.log — real ilovalardagi muammolarni tekshirish uchun (M4).</summary>
internal static class Log
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ozbeca");
    private static readonly string FilePath = Path.Combine(Dir, "ozbeca.log");
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                // ponytail: cheksiz o'sadi; 1 MB dan oshsa qayta boshlaymiz.
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
