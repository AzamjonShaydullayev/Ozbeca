using System.Text;
using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Text;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Handlers;

/// <summary>Lichkadagi matn oqimi: buyruqlar, matn/caption o'girish, "Teskari" tugmasi. Foydalanuvchi matni saqlanmaydi.</summary>
internal sealed class PrivateHandler(BotApi api, Sender sender, Store store, PremiumEmoji emoji, MenuHandler menu)
{
    private const int MaxPart = 4000;      // Telegram chegarasi 4096; sarlavhaga joy qoladi
    private const int ExpandableOver = 500;
    private const int CopyMax = 256;   // Telegram copy_text chegarasi — uzunroq natijada Nusxa tugmasi yo'q

    internal MenuHandler Menu => menu;

    public async Task Handle(JsonElement msg, UserRow user, CancellationToken ct)
    {
        var chatId = user.Id;   // lichkada chat id == user id
        var text = msg.Str("text");
        var entities = Entity.Parse(Prop(msg, text != null ? "entities" : "caption_entities"));
        // buyruq: faqat 0-o'rinda bot_command entity bo'lsa ("/ salom" yoki yo'l matni o'giriladi)
        if (text != null && entities.Any(e => e.Type == "bot_command" && e.Offset == 0)) { await Command(chatId, user, text, msg.Obj("from")?.Str("first_name"), ct); return; }
        text ??= msg.Str("caption");
        if (string.IsNullOrEmpty(text)) { await Say(chatId, Texts.SendText, ct: ct); return; }

        if (await Respond(chatId, user, user.Settings.Reverse, text, entities, ct))
        {
            store.AddChars(user.Id, text.Length);
            store.AddUsage("text", text.Length);
        }
    }

    private async Task Command(long chatId, UserRow user, string text, string? name, CancellationToken ct)
    {
        var parts = text.Split([' ', '\n'], 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].Split('@')[0].ToLowerInvariant();
        switch (cmd)
        {
            case "/start" or "/menu": await menu.Send('0', chatId, user, name, ct); break;   // parametr (masalan "inline") ahamiyatsiz
            case "/sozlama": await menu.Send('s', chatId, user, name, ct); break;
            case "/oyin": await menu.Send('g', chatId, user, name, ct); break;
            case "/harflar": await menu.Send('h', chatId, user, name, ct); break;
            case "/statistika": await menu.Send('t', chatId, user, name, ct); break;
            default:   // /help va noma'lum buyruqlar
                await Say(chatId, Texts.Help, ct: ct);
                break;
        }
    }

    /// <summary>"Teskari" tugmasi: xabardagi blockquote'ni qarama-qarshi yo'nalishda o'girib yangi xabar yuboradi. Matn saqlanmaydi.</summary>
    public async Task OnReverse(JsonElement cq, UserRow user, CancellationToken ct)
    {
        if (cq.Obj("message") is not { } msg || msg.Long("date") == 0 || msg.Str("text") is not { } text) return;
        if (msg.Obj("chat") is not { } chat || chat.Str("type") != "private") return;

        var all = Entity.Parse(Prop(msg, "entities"));
        var quote = all.FirstOrDefault(e => e.Type is "blockquote" or "expandable_blockquote");
        if (quote is null || quote.Offset < 0 || quote.Length <= 0 || quote.Offset + quote.Length > text.Length) return;

        var inner = all.Where(e => e != quote && e.Type is not ("blockquote" or "expandable_blockquote")
                                   && e.Offset >= quote.Offset && e.Length > 0 && e.Offset + e.Length <= quote.Offset + quote.Length)
                       .Select(e => e with { Offset = e.Offset - quote.Offset }).ToList();
        // rev:n — ko'rsatilgan natija yangi alifboda, demak eskiga qaytaramiz (Reverse = true)
        var reverse = cq.Str("data") == "rev:n";
        await Respond(user.Id, user, reverse, text.Substring(quote.Offset, quote.Length), inner, ct);
    }

    /// <summary>O'giradi va yuboradi. false — o'zgarish yo'q (NoChange yuborildi).</summary>
    private async Task<bool> Respond(long chatId, UserRow user, bool reverse, string text, List<Entity> entities, CancellationToken ct)
    {
        var conv = TextPipeline.Convert(text, entities, user.Settings.ToOptions() with { Reverse = reverse });
        var result = conv.Text;
        if (!conv.Changed || string.IsNullOrWhiteSpace(result)) { await Say(chatId, Texts.NoChange, ct: ct); return false; }

        var header = reverse ? Texts.OldHeader : Texts.NewHeader;
        if (result.Length > MaxPart && user.Settings.LongAsFile)
        {
            await SendFile(chatId, user.Id, header, result, ct);
            return true;
        }

        var parts = result.Length > MaxPart ? TextPipeline.Split(result, conv.Entities, MaxPart) : [(result, conv.Entities)];
        for (var i = 0; i < parts.Count; i++)
        {
            var b = new MessageBuilder(emoji, user.Id);
            if (i == 0) b.Emoji("success").Text(" " + header).NewLine();
            b.Quote(parts[i].Text, parts[i].Entities, parts[i].Text.Length > ExpandableOver);
            var (t, e) = b.Build();
            await Say(chatId, t, e, Keyboard(parts.Count == 1 ? result : null, reverse), ct);
        }
        return true;
    }

    /// <summary>Teskari — har bo'lakda (o'z quote'ini o'giradi); Nusxa — faqat butun natija ≤ 256 bo'lsa.</summary>
    private static object Keyboard(string? result, bool reverse)
    {
        var row = new List<object>();
        if (result is { Length: <= CopyMax }) row.Add(Buttons.Copy(Texts.CopyButton, result));
        row.Add(Buttons.Callback(Texts.ReverseButton, reverse ? "rev:o" : "rev:n", "primary"));
        return Buttons.Markup(row.ToArray());
    }

    private async Task SendFile(long chatId, long userId, string header, string result, CancellationToken ct)
    {
        var (caption, ents) = new MessageBuilder(emoji, userId).Emoji("success").Text(" " + header).Build();
        var fields = new Dictionary<string, string> { ["chat_id"] = chatId.ToString(), ["caption"] = caption };
        if (ents.Count > 0) fields["caption_entities"] = JsonSerializer.Serialize(ents.Select(e => e.ToJson()), Json.Options);
        var bytes = new UTF8Encoding(false).GetBytes(result);
        try
        {
            await api.Upload("sendDocument", fields, "document", "natija.txt", new MemoryStream(bytes), ct);
        }
        catch (BotApiException e) when (e.Code == 400 && fields.Remove("caption_entities"))
        {
            // premium emoji rad etildi — bezaksiz qayta (Sender shu ishni matn xabarlari uchun qiladi)
            await api.Upload("sendDocument", fields, "document", "natija.txt", new MemoryStream(bytes), ct);
        }
    }

    private Task Say(long chatId, string text, IReadOnlyList<Entity>? entities = null, object? markup = null, CancellationToken ct = default)
    {
        var p = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text,
            ["link_preview_options"] = new { is_disabled = true },
        };
        if (entities is { Count: > 0 }) p["entities"] = entities.Select(e => e.ToJson()).ToList();
        if (markup != null) p["reply_markup"] = markup;
        return sender.Send("sendMessage", p, ct);
    }

    private static JsonElement? Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : null;
}
