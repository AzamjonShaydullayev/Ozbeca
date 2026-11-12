using System.Text.Json;

namespace Ozbeca.Bot.Ui;

/// <summary>Premium (custom) emoji katalogi. O'chiq bo'lsa yoki topilmasa — oddiy emoji.</summary>
public sealed class PremiumEmoji
{
    private readonly Dictionary<string, (string Fallback, string[] Ids)> _catalog = new();
    private readonly Dictionary<string, string> _anim = new();
    private readonly bool _enabled;

    public PremiumEmoji(string catalogJson, string animatedJson, bool enabled)
    {
        _enabled = enabled;
        using (var doc = JsonDocument.Parse(catalogJson))
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var fb = p.Value.TryGetProperty("fallback", out var f) ? f.GetString() ?? "" : "";
                var ids = p.Value.TryGetProperty("ids", out var a) && a.ValueKind == JsonValueKind.Array
                    ? a.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
                _catalog[p.Name] = (fb, ids);
            }
        using (var doc = JsonDocument.Parse(animatedJson))
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var id = p.Value.GetString()!;
                _anim[p.Name] = id;
                _anim.TryAdd(Bare(p.Name), id);   // U+FE0F bilan ham, usiz ham topilsin
            }
    }

    public static PremiumEmoji LoadEmbedded(bool enabled) =>
        new(Read("emoji.json"), Read("emoji_animated.json"), enabled);

    private static string Read(string name)
    {
        using var s = typeof(PremiumEmoji).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Resurs yo'q: " + name);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private static string Bare(string s) => s.Replace("️", "");

    private string? AnimId(string ch) =>
        ch.Length == 0 ? null
        : _anim.GetValueOrDefault(ch) ?? _anim.GetValueOrDefault(Bare(ch)) ?? _anim.GetValueOrDefault(ch + "️");

    /// <summary>Oddiy emoji: katalogdagi fallback; rol o'zi emoji belgisi bo'lsa — o'sha; aks holda "".</summary>
    public string Plain(string role)
    {
        if (_catalog.TryGetValue(role, out var c)) return c.Fallback;
        return role.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-') ? "" : role;
    }

    public string? Id(string role, long userId, DateOnly day)
    {
        if (!_enabled) return null;
        if (_catalog.TryGetValue(role, out var c) && c.Ids.Length > 0)
        {
            var seed = Crc32(userId + "|" + day.ToString("yyyy-MM-dd")) ^ Crc32(role);
            return c.Ids[(int)(seed % (uint)c.Ids.Length)];
        }
        return AnimId(role) ?? (c.Fallback is { } fb ? AnimId(fb) : null);
    }

    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(i =>
    {
        var c = (uint)i;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    internal static uint Crc32(string s)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(s)) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return ~c;
    }
}
