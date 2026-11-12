using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ozbeca.Bot.Api;

/// <summary>Xabar yuborish: 429 da kutib qayta, premium emoji/style xatosida bezaksiz qayta.</summary>
public sealed class Sender(BotApi api)
{
    /// <summary>Testlarda almashtiriladi.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    public async Task<JsonElement> Send(string method, Dictionary<string, object?> payload, CancellationToken ct = default)
    {
        // JsonNode nusxasi: chaqiruvchi payload'iga tegmaymiz, strip shu nusxada.
        var node = JsonSerializer.SerializeToNode(payload, Json.Options)!;
        DropNulls(node);   // lug'atdagi null qiymatlar WhenWritingNull'ga bo'ysunmaydi: "reply_markup":null yuborilmasin
        var retried429 = false;
        var stripped = false;
        while (true)
        {
            try { return await api.Call(method, node, ct); }
            catch (BotApiException e) when (e.Code == 429 && !retried429)
            {
                retried429 = true;
                await Delay(TimeSpan.FromSeconds(Math.Clamp(e.RetryAfter ?? 1, 0, 30)), ct);
            }
            catch (BotApiException e) when (e.Code == 400 && !stripped && IsDecorationError(e.Description))
            {
                stripped = true;
                Strip(node);
            }
        }
    }

    internal static bool IsDecorationError(string d) =>
        d.Contains("emoji", StringComparison.OrdinalIgnoreCase) || d.Contains("style", StringComparison.OrdinalIgnoreCase);

    private static void DropNulls(JsonNode? n)
    {
        if (n is JsonObject o)
            foreach (var (k, v) in o.ToList()) { if (v is null) o.Remove(k); else DropNulls(v); }
        else if (n is JsonArray a)
            foreach (var v in a) DropNulls(v);
    }

    private static void Strip(JsonNode root)
    {
        StripOne(root);
        // answerInlineQuery: results[].input_message_content va results[].reply_markup
        if (root["results"] is JsonArray results)
            foreach (var r in results.OfType<JsonObject>())
            {
                StripOne(r);
                if (r["input_message_content"] is JsonObject imc) StripOne(imc);
            }
    }

    private static void StripOne(JsonNode root)
    {
        foreach (var key in new[] { "entities", "caption_entities" })
            if (root[key] is JsonArray arr)
                for (var i = arr.Count - 1; i >= 0; i--)
                    if (arr[i]?["type"]?.GetValue<string>() == "custom_emoji") arr.RemoveAt(i);
        if (root["reply_markup"]?["inline_keyboard"] is JsonArray rows)
            foreach (var row in rows.OfType<JsonArray>())
                foreach (var btn in row.OfType<JsonObject>())
                {
                    btn.Remove("icon_custom_emoji_id");
                    btn.Remove("style");
                }
    }
}
