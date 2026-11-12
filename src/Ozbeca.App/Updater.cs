using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Threading;
using Ozbeca.Core;

namespace Ozbeca.App;

/// <summary>
/// Avtomatik yangilanish (sozlamalardan o'chiriladi). Har 3 soatda GitHub'dagi oxirgi release tekshiriladi;
/// yangisi bo'lsa Ozbeca.exe yuklanadi va hajmi, SHA-256 xeshi (GitHub bergan), nomi va versiyasi tekshiriladi.
/// O'rnatish — user kompyuterdan bir daqiqa foydalanmay turganda: yangi exe ishga tushiriladi va u mavjud
/// Install.UpdateOlder yo'li bilan eski nusxani yopib, o'rniga o'tadi. Hech narsa yuborilmaydi.
/// Faqat o'rnatilgan nusxada ishlaydi (Downloads'dan ishlatilganda — yo'q).
/// </summary>
internal sealed class Updater
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ozbeca", "update");

    // Kuniga 1 marta sekin edi: release'dan keyin user bir kun kutardi. 3 soat — GitHub'ning 60 so'rov/soat chegarasidan juda uzoq.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromHours(1); // internet yo'q yoki GitHub javob bermadi
    private const uint IdleMs = 60_000;

    private readonly Func<Settings> _settings;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(2) };
    private DateTime _nextTry = DateTime.MinValue;
    private string? _ready; // yuklangan va tekshirilgan exe — o'rnatishni kutyapti
    private bool _busy;

    public Updater(Func<Settings> settings)
    {
        _settings = settings;
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        if (Install.IsInstalledCopy) Cleanup();
    }

    /// <summary>Sozlama yoqildi — keyingi daqiqada tekshirilsin.</summary>
    public void CheckSoon()
    {
        _nextTry = DateTime.MinValue;
        _settings().LastUpdateCheckUtcTicks = 0;
    }

    private void Tick()
    {
        _timer.Interval = TimeSpan.FromMinutes(1);
        var s = _settings();
        if (!s.AutoUpdate || !Install.IsInstalledCopy) return;
        if (_ready != null)
        {
            if (IdleFor() >= IdleMs) Apply();
            return;
        }
        DateTime now = DateTime.UtcNow;
        long last = s.LastUpdateCheckUtcTicks;
        if (_busy || now < _nextTry || last > 0 && last <= now.Ticks && now.Ticks - last < CheckInterval.Ticks) return;

        _busy = true;
        var current = typeof(Updater).Assembly.GetName().Version;
        var dispatcher = Dispatcher.CurrentDispatcher;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            string? path = null;
            bool ok = false;
            try { path = CheckAndDownload(current, Dir); ok = true; }
            catch (Exception ex) { Log.Write("Yangilanish tekshirilmadi: " + ex.Message); }
            dispatcher.BeginInvoke(new Action(() =>
            {
                _busy = false;
                _ready = path;
                if (!ok) { _nextTry = DateTime.UtcNow + RetryInterval; return; }
                s.LastUpdateCheckUtcTicks = now.Ticks;
                s.Save();
            }));
        });
    }

    /// <summary>
    /// Yangi versiya bo'lsa — dir\Ozbeca.exe ga yuklab, tekshirib, yo'lini qaytaradi; yangisi yo'q — null.
    /// Tarmoq yoki tekshiruv xatosi — exception (chaqiruvchi logga yozadi).
    /// </summary>
    internal static string? CheckAndDownload(Version current, string dir)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string json;
        using (var resp = Get(UpdateInfo.ApiUrl, "application/vnd.github+json"))
        using (var reader = new StreamReader(resp.GetResponseStream()!))
            json = reader.ReadToEnd();
        var info = UpdateInfo.Parse(json, current);
        if (info == null) return null;
        Log.Write($"Yangi versiya bor: {info.Version} — yuklanmoqda");

        Directory.CreateDirectory(dir);
        string part = Path.Combine(dir, "Ozbeca.exe.part"), exe = Path.Combine(dir, "Ozbeca.exe");
        using (var resp = Get(info.Url, "application/octet-stream"))
        {
            if (resp.ResponseUri.Scheme != Uri.UriSchemeHttps) throw new IOException("yuklash HTTPS emas");
            using var src = resp.GetResponseStream()!;
            using var dst = File.Create(part);
            var buf = new byte[81920];
            long total = 0;
            for (int n; (n = src.Read(buf, 0, buf.Length)) > 0;)
            {
                total += n;
                if (total > info.Size) throw new IOException("fayl kutilganidan katta");
                dst.Write(buf, 0, n);
            }
        }
        try
        {
            if (new FileInfo(part).Length != info.Size) throw new IOException("fayl hajmi mos emas");
            using (var f = File.OpenRead(part))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant() != info.Sha256)
                    throw new IOException("SHA-256 mos emas");
            var name = AssemblyName.GetAssemblyName(part);
            if (name.Name != "Ozbeca" || name.Version == null || UpdateInfo.Normalize(name.Version) != info.Version)
                throw new IOException($"exe mos emas: {name.Name} {name.Version}");
            if (File.Exists(exe)) File.Delete(exe);
            File.Move(part, exe);
        }
        finally { if (File.Exists(part)) File.Delete(part); }
        Log.Write($"Yangilanish {info.Version} yuklandi va tekshirildi");
        return exe;
    }

    private static WebResponse Get(string url, string accept)
    {
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.UserAgent = "Ozbeca-updater";
        req.Accept = accept;
        req.Timeout = req.ReadWriteTimeout = 30_000;
        return req.GetResponse();
    }

    /// <summary>Yangi exe ishga tushadi — Install.UpdateOlder bu nusxani yopib, o'rnatilganini almashtiradi.</summary>
    private void Apply()
    {
        string path = _ready!;
        _ready = null;
        if (!File.Exists(path)) return;
        Log.Write("Yangilanish o'rnatilmoqda");
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Dir }); }
        catch (Exception ex) { Log.Write("Yangilanish ishga tushmadi: " + ex.Message); }
    }

    /// <summary>O'rnatilgan nusxa ishlayapti — yuklangan fayl kerak emas (band bo'lsa keyingi safar).</summary>
    private static void Cleanup()
    {
        try { if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Oxirgi klaviatura/sichqoncha harakatidan beri o'tgan ms.</summary>
    internal static uint IdleFor()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.dwTime) : 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
}
