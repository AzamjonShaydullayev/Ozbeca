using System.Text.Encodings.Web;
using System.Text.Json;
using Ozbeca.Bot.Data;

namespace Ozbeca.Bot.Widget;

/// <summary>Widget'lar chat_data JSON'i (≤ 1024 bayt). "k" — tur: s (sozlama), h (harflar), g (o'yin), t (statistika).</summary>
public static class WidgetState
{
    private static readonly JsonSerializerOptions Opts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json(object o) => JsonSerializer.Serialize(o, Opts);

    public const string Letters = "{\"k\":\"h\"}";

    public static string Settings(Settings s) =>
        Json(new { k = "s", d = s.Reverse ? 1 : 0, c = s.Cyrillic ? 1 : 0, w = s.Dictionary ? 1 : 0, f = s.LongAsFile ? 1 : 0 });

    /// <summary>Javob (Answer) chat_data'ga kirmaydi.</summary>
    public static string Game(UserRow u, Puzzle p) =>
        Json(new { k = "g", s = u.Streak, b = u.Best, l = p.Level, q = p.Masked, o = p.Options, t = p.Timer });

    /// <summary>week — 7 kun eskidan bugunga; t = week[6] (bugun).</summary>
    public static string Stats(int[] week, (long Chars, int Files) totals, int best) =>
        Json(new { k = "t", t = week[6], w = week, a = totals.Chars, f = totals.Files, b = best });
}
