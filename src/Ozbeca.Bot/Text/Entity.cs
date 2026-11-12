using System.Text.Json;
using Ozbeca.Bot.Api;

namespace Ozbeca.Bot.Text;

/// <summary>Telegram MessageEntity (offset/length UTF-16 birliklarida).</summary>
public sealed record Entity(string Type, int Offset, int Length, string? Url = null, string? CustomEmojiId = null, string? Language = null, JsonElement? User = null)
{
    public static List<Entity> Parse(JsonElement? array)
    {
        var list = new List<Entity>();
        if (array is not { ValueKind: JsonValueKind.Array } arr) return list;
        foreach (var e in arr.EnumerateArray())
        {
            var type = e.Str("type");
            if (type is null || e.Int("offset") is not { } off || e.Int("length") is not { } len) continue;
            list.Add(new Entity(type, off, len, e.Str("url"), e.Str("custom_emoji_id"), e.Str("language"), e.Obj("user")?.Clone()));  // hujjat dispose bo'lsa ham yashasin
        }
        return list;
    }

    /// <summary>Telegramga yuboriladigan shakl (Json.Options bilan: snake_case, null'lar tushiriladi).</summary>
    public object ToJson() => new
    {
        type = Type,
        offset = Offset,
        length = Length,
        url = Url,
        custom_emoji_id = CustomEmojiId,
        language = Language,
        user = User,
    };
}
