using System.Collections.Concurrent;
using System.IO;

namespace Ozbeca.App;

/// <summary>
/// %AppData%\Ozbeca\ozbeca.log — real ilovalardagi muammolarni tekshirish uchun (M4).
/// Yozish alohida thread'da: Write klaviatura hook'i ichidan ham chaqiriladi, disk sekin bo'lsa hook kechikib,
/// Windows uni olib tashlardi.
/// </summary>
internal static class Log
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ozbeca");
    private static readonly string FilePath = Path.Combine(Dir, "ozbeca.log");
    private static readonly BlockingCollection<string> Queue = new(boundedCapacity: 1000);
    private static readonly ManualResetEventSlim Idle = new(true);

    static Log()
    {
        new Thread(Writer) { IsBackground = true, Name = "OzbecaLog", Priority = ThreadPriority.BelowNormal }.Start();
    }

    public static void Write(string message)
    {
        Idle.Reset();
        // To'lib qolsa (disk osilgan) — xabarni tashlab yuboramiz, chaqiruvchi kutmasin.
        if (!Queue.TryAdd($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}")) Idle.Set();
    }

    /// <summary>Dastur yopilayotganda: navbatdagilar faylga tushsin (ko'pi bilan timeoutMs).</summary>
    public static void Flush(int timeoutMs = 500) => Idle.Wait(timeoutMs);

    private static void Writer()
    {
        var batch = new List<string>();
        foreach (var first in Queue.GetConsumingEnumerable())
        {
            batch.Clear();
            batch.Add(first);
            while (batch.Count < 200 && Queue.TryTake(out var more)) batch.Add(more);
            try
            {
                Directory.CreateDirectory(Dir);
                // Cheksiz o'smasin: 1 MB dan oshsa qayta boshlaymiz.
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Delete(FilePath);
                File.AppendAllText(FilePath, string.Join(Environment.NewLine, batch) + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (Queue.Count == 0) Idle.Set();
        }
    }
}
