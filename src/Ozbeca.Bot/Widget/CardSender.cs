using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Widget;

/// <summary>Boshqaruv kartasi: sendWidget; widget rad etilsa — oddiy xabar + set: tugmalari (zaxira).</summary>
public sealed class CardSender(Sender sender, Store store)
{
    internal static readonly string Html = LoadHtml();

    private static string LoadHtml()
    {
        using var s = typeof(CardSender).Assembly.GetManifestResourceStream("card.html")
                      ?? throw new InvalidOperationException("card.html resursi yo'q");
        return new StreamReader(s).ReadToEnd();
    }

    /// <summary>u — Touch qilingan user. Yangi topishmoq Store'ga yoziladi.</summary>
    public async Task Send(long chatId, UserRow u, CancellationToken ct)
    {
        var p = Game.Next(Random.Shared);
        store.SetGame(u.Id, Game.Pack(p), u.Streak, u.Best);
        try
        {
            await sender.Send("sendWidget", new()
            {
                ["chat_id"] = chatId,
                ["html"] = Html,
                ["chat_data"] = WidgetState.Build(u, p),
                ["caption"] = Texts.CardCaption,
                ["allow_queries"] = true,
            }, ct);
        }
        catch (BotApiException e)
        {
            Log.Info($"sendWidget rad etildi ({e.Code}) — tugmali zaxira");
            await sender.Send("sendMessage", new()
            {
                ["chat_id"] = chatId, ["text"] = Texts.SettingsTitle, ["reply_markup"] = Keyboard(u.Settings),
            }, ct);
        }
    }

    /// <summary>Zaxira tugmalari (set:r|c|w|f): sozlamani almashtiradi, xabarni yangilaydi, callback'ga o'zi javob beradi.</summary>
    public async Task OnCallback(JsonElement cq, UserRow u, CancellationToken ct)
    {
        var s = u.Settings;
        Settings? next = cq.Str("data") switch
        {
            "set:r" => s with { Reverse = !s.Reverse },
            "set:c" => s with { Cyrillic = !s.Cyrillic },
            "set:w" => s with { Dictionary = !s.Dictionary },
            "set:f" => s with { LongAsFile = !s.LongAsFile },
            _ => null,
        };
        var msg = cq.Obj("message");
        if (next != null && msg?.Long("date") is > 0) store.SaveSettings(u.Id, next);
        else next = null;   // noma'lum yoki eskirgan (inaccessible) xabar — faqat javob

        if (cq.Str("id") is { } id)
            try { await sender.Send("answerCallbackQuery", new() { ["callback_query_id"] = id }, ct); }
            catch (BotApiException) { }   // eskirgan callback

        if (next != null && msg!.Value.Obj("chat")?.Long("id") is { } chatId && msg.Value.Long("message_id") is { } mid)
            await sender.Send("editMessageText", new()
            {
                ["chat_id"] = chatId, ["message_id"] = mid, ["text"] = Texts.SettingsTitle, ["reply_markup"] = Keyboard(next),
            }, ct);
    }

    private static object Keyboard(Settings s)
    {
        static string Mark(bool on, string text) => (on ? "✅ " : "⬜ ") + text;
        return Buttons.Markup(
            [Buttons.Callback("🔁 " + (s.Reverse ? Texts.SetNewToOld : Texts.SetOldToNew), "set:r")],
            [Buttons.Callback(Mark(s.Cyrillic, Texts.SetCyrillic), "set:c")],
            [Buttons.Callback(Mark(s.Dictionary, Texts.SetWords), "set:w")],
            [Buttons.Callback(Mark(s.LongAsFile, Texts.SetFile), "set:f")]);
    }
}
