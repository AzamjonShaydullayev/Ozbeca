using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Widget;

namespace Ozbeca.Bot.Handlers;

/// <summary>
/// Karta so'rovlari (widget_query.data — ishonchsiz kirish, faqat aniq qiymatlar): r — yangilash, d:0|1 — yo'nalish,
/// t:c|w|f — toggle, g:0..2 — o'yin javobi. Store → editMessageWidgetData → answerWidgetQuery ("ok" | "no:Ş" | "err").
/// </summary>
internal sealed class WidgetHandler(Sender sender, Store store)
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
        var s = u.Settings;
        var puzzle = Game.Unpack(u.GameAnswer);
        var answer = "ok";
        switch (q.Str("data"))
        {
            case "r": break;
            case "d:0": s = s with { Reverse = false }; break;
            case "d:1": s = s with { Reverse = true }; break;
            case "t:c": s = s with { Cyrillic = !s.Cyrillic }; break;
            case "t:w": s = s with { Dictionary = !s.Dictionary }; break;
            case "t:f": s = s with { LongAsFile = !s.LongAsFile }; break;
            case ['g', ':', >= '0' and <= '2' and var c] when puzzle != null:
                var right = puzzle.Options[c - '0'] == puzzle.Answer;
                var streak = right ? u.Streak + 1 : 0;
                u = u with { Streak = streak, Best = Math.Max(u.Best, streak) };
                answer = right ? "ok" : "no:" + puzzle.Answer;
                puzzle = null;   // har javobdan keyin yangi topishmoq
                break;
            default:
                await Answer(id, "err", ct);
                return;
        }

        if (s != u.Settings) { store.SaveSettings(u.Id, s); u = u with { Settings = s }; }
        if (puzzle == null)
        {
            puzzle = Game.Next(Random.Shared);
            store.SetGame(u.Id, Game.Pack(puzzle), u.Streak, u.Best);
        }

        try
        {
            await sender.Send("editMessageWidgetData", new()
            {
                ["chat_id"] = chatId, ["message_id"] = messageId, ["chat_data"] = WidgetState.Build(u, puzzle),
            }, ct);
        }
        catch (BotApiException e) when (e.Description.Contains("not modified", StringComparison.OrdinalIgnoreCase)) { }   // holat o'zgarmagan (r)
        catch (BotApiException) { await Answer(id, "err", ct); throw; }   // xabar o'chirilgan va h.k. — widget TIMEOUT kutmasin, xato log'ga
        await Answer(id, answer, ct);
    }

    private Task Answer(string id, string data, CancellationToken ct) =>
        sender.Send("answerWidgetQuery", new() { ["widget_query_id"] = id, ["data"] = data }, ct);
}
