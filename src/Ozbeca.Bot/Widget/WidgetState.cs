using System.Text.Encodings.Web;
using System.Text.Json;
using Ozbeca.Bot.Data;

namespace Ozbeca.Bot.Widget;

/// <summary>Kartaning chat_data JSON'i (≤ 1024 bayt): d/c/w/f — sozlamalar, t — bugungi belgilar, s/b — seriya/eng yaxshi, q/o — o'yin.</summary>
public static class WidgetState
{
    private static readonly JsonSerializerOptions Opts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Build(UserRow u, Puzzle? p)
    {
        var s = u.Settings;
        var d = new Dictionary<string, object>
        {
            ["d"] = s.Reverse ? 1 : 0, ["c"] = s.Cyrillic ? 1 : 0, ["w"] = s.Dictionary ? 1 : 0, ["f"] = s.LongAsFile ? 1 : 0,
            ["t"] = u.CharsToday, ["s"] = u.Streak, ["b"] = u.Best,
        };
        if (p != null) { d["q"] = p.Masked; d["o"] = p.Options; }
        return JsonSerializer.Serialize(d, Opts);
    }
}
