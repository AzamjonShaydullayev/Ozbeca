using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Widget;

namespace Ozbeca.Bot.Handlers;

/// <summary>
/// Widget so'rovlari (widget_query.data — ishonchsiz kirish, faqat aniq qiymatlar):
/// sozlama — r:s, d:0|1, t:c|w|f; o'yin — r:g, g:i, g:ij, g:t; statistika — r:t.
/// Touch → Store → editMessageWidgetData (holat o'zgargan bo'lsa) → answerWidgetQuery ("ok" | "no:ŞĞ" | "err").
/// </summary>
internal sealed class WidgetHandler(Sender sender, Store store, CardSender card)
{
    public async Task Handle(JsonElement q, CancellationToken ct)
    {
        if (q.Str("id") is not { } id) return;
        if (q.Obj("from")?.Long("id") is not { } userId || q.Obj("chat")?.Long("id") is not { } chatId
            || q.Long("message_id") is not { } messageId)
        {
            await Answer(id, "err", ct);   // inline widget (inline_message_id) — production'da yo'q
            return;
        }

        var u = store.Touch(userId);
        var data = q.Str("data") ?? "";
        var answer = "ok";
        string state;
        switch (data)
        {
            case "r:s" or "d:0" or "d:1" or "t:c" or "t:w" or "t:f":
                var s = u.Settings;
                s = data switch
                {
                    "d:0" => s with { Reverse = false },
                    "d:1" => s with { Reverse = true },
                    "t:c" => s with { Cyrillic = !s.Cyrillic },
                    "t:w" => s with { Dictionary = !s.Dictionary },
                    "t:f" => s with { LongAsFile = !s.LongAsFile },
                    _ => s,
                };
                if (s != u.Settings) store.SaveSettings(u.Id, s);
                state = WidgetState.Settings(s);
                break;
            case "r:t":
                state = card.StatsState(u);
                break;
            case "r:g":
                // taymerli topishmoq vaqti o'tgan (karta keyinroq ochildi) — yangisi, seriyaga tegilmaydi
                var p = Game.Unpack(u.GameAnswer);
                if (p == null || Game.Expired(p, Clock.Ms())) p = card.NewPuzzle(u);
                state = WidgetState.Game(u, p);
                break;
            case ['g', ':', ..]:
                if (Game.Unpack(u.GameAnswer) is not { } cur || Game.Check(cur, data, Clock.Ms()) is not { } right)
                {
                    await Answer(id, "err", ct);
                    return;
                }
                var streak = right ? u.Streak + 1 : 0;
                u = u with { Streak = streak, Best = Math.Max(u.Best, streak) };
                answer = right ? "ok" : "no:" + cur.Answer;
                state = WidgetState.Game(u, card.NewPuzzle(u));   // har javobdan keyin yangi topishmoq — takror hisoblanmaydi
                break;
            default:
                await Answer(id, "err", ct);
                return;
        }

        if (!card.Unchanged(chatId, messageId, state))
        {
            try
            {
                await sender.Send("editMessageWidgetData", new()
                {
                    ["chat_id"] = chatId, ["message_id"] = messageId, ["chat_data"] = state,
                }, ct);
            }
            catch (BotApiException e) when (e.Description.Contains("not modified", StringComparison.OrdinalIgnoreCase)) { }   // holat o'zgarmagan
            catch (BotApiException) { await Answer(id, "err", ct); throw; }   // xabar o'chirilgan va h.k. — widget TIMEOUT kutmasin, xato log'ga
            card.Remember(chatId, messageId, state);
        }
        await Answer(id, answer, ct);
    }

    private Task Answer(string id, string data, CancellationToken ct) =>
        sender.Send("answerWidgetQuery", new() { ["widget_query_id"] = id, ["data"] = data }, ct);
}
