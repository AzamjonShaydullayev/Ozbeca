using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Ozbeca.Core;

/// <summary>
/// GitHub'dagi oxirgi release (api.github.com/repos/.../releases/latest) javobidan yangilanish ma'lumoti.
/// Faqat shu repo release'idagi "Ozbeca.exe" qabul qilinadi.
/// </summary>
public sealed class UpdateInfo
{
    public const string Repo = "AzamjonShaydullayev/Ozbeca";
    public const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";
    public const string DownloadPrefix = "https://github.com/" + Repo + "/releases/download/";
    public const string AssetName = "Ozbeca.exe";
    public const long MaxSize = 20_000_000;

    public Version Version { get; private set; } = new(0, 0);
    public string Url { get; private set; } = "";
    public long Size { get; private set; }

    /// <summary>GitHub bergan SHA-256 (kichik harfli hex). Xeshsiz release qabul qilinmaydi.</summary>
    public string Sha256 { get; private set; } = "";

    /// <summary>Javobda joriydan yangi, to'g'ri release bo'lsa — ma'lumot; aks holda null.</summary>
    public static UpdateInfo? Parse(string json, Version current)
    {
        Release? r;
        try
        {
            using var s = new MemoryStream(Encoding.UTF8.GetBytes(json));
            r = new DataContractJsonSerializer(typeof(Release)).ReadObject(s) as Release;
        }
        catch (SerializationException) { return null; }
        if (r?.Tag == null || r.Assets == null) return null;

        var tag = Regex.Match(r.Tag, @"^v?(\d+\.\d+(?:\.\d+)?)$");
        if (!tag.Success || !Version.TryParse(tag.Groups[1].Value, out var latest)) return null;
        if (Normalize(latest) <= Normalize(current)) return null;

        foreach (var a in r.Assets)
        {
            if (a?.Name != AssetName || a.Url == null) continue;
            if (a.Url != DownloadPrefix + r.Tag + "/" + AssetName || a.Size <= 0 || a.Size > MaxSize) return null;
            var digest = Regex.Match(a.Digest ?? "", "^sha256:([0-9a-fA-F]{64})$");
            if (!digest.Success) return null;
            return new UpdateInfo
            {
                Version = Normalize(latest),
                Url = a.Url,
                Size = a.Size,
                Sha256 = digest.Groups[1].Value.ToLowerInvariant(),
            };
        }
        return null;
    }

    /// <summary>1.7 va 1.7.0 teng; revision hisobga olinmaydi (exe versiyasi 1.7.0.0).</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    [DataContract]
    private sealed class Release
    {
        [DataMember(Name = "tag_name")] public string? Tag { get; set; }
        [DataMember(Name = "assets")] public List<Asset?>? Assets { get; set; }
    }

    [DataContract]
    private sealed class Asset
    {
        [DataMember(Name = "name")] public string? Name { get; set; }
        [DataMember(Name = "browser_download_url")] public string? Url { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "digest")] public string? Digest { get; set; }
    }
}
