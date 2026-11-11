using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Ozbeca.App;

/// <summary>
/// Ixtiyoriy (standart o'chiq) yangi versiya tekshiruvi: GitHub'dagi oxirgi release raqamini o'qiydi, xolos.
/// Hech narsa yuklab olinmaydi va yuborilmaydi (faqat oddiy GET, User-Agent — dastur nomi). Yangisi bo'lsa —
/// bildirishnoma; bosilsa release sahifasi brauzerda ochiladi.
/// </summary>
internal static class UpdateChecker
{
    private const string Api = "https://api.github.com/repos/AzamjonShaydullayev/Ozbeca/releases/latest";
    private const string ReleasePrefix = "https://github.com/AzamjonShaydullayev/Ozbeca/releases/";
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>Yangi versiya bo'lsa (versiya, sahifa), aks holda null. Tarmoq xatosi — null (logga yoziladi).</summary>
    public static (Version Version, string Url)? Check(Version current)
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(Api);
            req.UserAgent = "Ozbeca-update-check";
            req.Accept = "application/vnd.github+json";
            req.Timeout = req.ReadWriteTimeout = 10_000;
            using var resp = req.GetResponse();
            using var reader = new StreamReader(resp.GetResponseStream()!);
            string json = reader.ReadToEnd();

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?(\\d+(?:\\.\\d+){1,3})\"");
            var url = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(" + Regex.Escape(ReleasePrefix) + "[^\"]+)\"");
            if (!tag.Success || !url.Success || !Version.TryParse(tag.Groups[1].Value, out var latest)) return null;
            return Normalize(latest) > Normalize(current) ? (latest, url.Groups[1].Value) : null;
        }
        catch (Exception ex) when (ex is WebException or IOException or InvalidOperationException or NotSupportedException)
        {
            Log.Write("Yangilanish tekshirilmadi: " + ex.Message);
            return null;
        }
    }

    // 1.5 va 1.5.0 teng bo'lsin
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
