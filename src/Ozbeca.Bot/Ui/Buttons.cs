namespace Ozbeca.Bot.Ui;

/// <summary>InlineKeyboardButton JSON'lari (Dictionary — Sender tugmadan style/ikonkani olib tashlay olsin).</summary>
public static class Buttons
{
    public static object Callback(string text, string data, string? style = null, string? iconId = null) =>
        System.Text.Encoding.UTF8.GetByteCount(data) > 64
            ? throw new ArgumentException("callback_data 64 baytdan oshdi", nameof(data))
            : Make(text, "callback_data", data, style, iconId);

    public static object Copy(string text, string copy, string? style = null) =>
        copy.Length > 256
            ? throw new ArgumentException("copy_text 256 belgidan oshdi", nameof(copy))
            : Make(text, "copy_text", new Dictionary<string, object?> { ["text"] = copy }, style, null);

    public static object Markup(params object[][] rows) =>
        new Dictionary<string, object?> { ["inline_keyboard"] = rows };

    private static Dictionary<string, object?> Make(string text, string key, object value, string? style, string? iconId)
    {
        var d = new Dictionary<string, object?> { ["text"] = text, [key] = value };
        if (style != null) d["style"] = style;
        if (iconId != null) d["icon_custom_emoji_id"] = iconId;
        return d;
    }
}
