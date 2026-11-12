using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozbeca.Bot.Api;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static bool TryProp(JsonElement e, string name, out JsonElement v)
    {
        v = default;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out v) && v.ValueKind != JsonValueKind.Null;
    }

    public static string? Str(this JsonElement e, string name) =>
        TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static long? Long(this JsonElement e, string name) =>
        TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : null;

    public static int? Int(this JsonElement e, string name) =>
        TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    public static JsonElement? Obj(this JsonElement e, string name) =>
        TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;
}
