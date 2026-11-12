using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Handlers;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot;

/// <summary>
/// Update'ni turi bo'yicha tegishli handler'ga yo'naltiradi. Keyingi task'lar ulanish joylari "TASKn:" izohlari bilan belgilangan.
/// </summary>
public sealed class Router(BotApi api, Sender sender, Store store, PremiumEmoji emoji, BotConfig cfg)
{
    internal BotConfig Config => cfg;   // TASK10: admin ID'lari uchun

    private readonly PrivateHandler _private = new(api, sender, store, emoji, new MenuHandler(sender, store, emoji));
    private readonly InlineHandler _inline = new(sender, store);   // TASK9
    internal AdminHandler Admin { get; } = new(api, sender, store, cfg);   // TASK10
    private readonly FileHandler _file = new(api, sender, store, emoji, cfg.WorkDir);   // TASK7
    private readonly FloodGuard _flood = new();

    // Mazmunli xabar turlari; ro'yxatda yo'q (xizmat xabarlari: pinned_message, ...) e'tiborsiz.
    private static readonly string[] ContentKeys =
        ["text", "document", "photo", "sticker", "voice", "audio", "video", "video_note", "animation", "contact", "location", "venue", "poll", "dice", "story"];

    public async Task Handle(JsonElement update, CancellationToken ct)
    {
        if (update.Obj("message") is { } message) await OnMessage(message, ct);
        else if (update.Obj("callback_query") is { } callback) await OnCallback(callback, ct);
        else if (update.Obj("inline_query") is { } inline) await OnInlineQuery(inline, ct);
        else if (update.Obj("chosen_inline_result") is { } chosen) await OnChosenInline(chosen, ct);
        // edited_message va boshqalar e'tiborsiz
    }

    /// <summary>Log uchun update turi (matnsiz).</summary>
    internal static string Kind(JsonElement update) =>
        update.ValueKind == JsonValueKind.Object
            ? update.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => n != "update_id") ?? "unknown"
            : "unknown";

    private async Task OnMessage(JsonElement m, CancellationToken ct)
    {
        if (m.Obj("chat") is not { } chat || chat.Str("type") != "private") return;
        if (m.Obj("from") is not { } from || from.Long("id") is not { } userId) return;
        if (from.TryGetProperty("is_bot", out var bot) && bot.ValueKind == JsonValueKind.True) return;
        if (m.Obj("via_bot") is not null) return;   // inline natija (o'zimizniki ham) — javob bermaymiz
        if (!ContentKeys.Any(k => m.TryGetProperty(k, out _))) return;

        if (!_flood.Allow(userId, out var notify))
        {
            if (notify) await sender.Send("sendMessage", new() { ["chat_id"] = userId, ["text"] = Texts.Flood }, ct);
            return;
        }
        var user = store.Touch(userId);

        if (await Admin.TryHandle(m, user, ct)) return;   // TASK10: admin buyruqlari
        if (m.TryGetProperty("document", out _)) await OnDocument(m, user, ct);   // TASK7: FileHandler (animation ham document maydoniga ega — FileHandler nom bo'yicha rad etadi)
        else await _private.Handle(m, user, ct);
    }

    private Task OnDocument(JsonElement m, UserRow user, CancellationToken ct) => _file.Handle(m, user, ct);

    private async Task OnCallback(JsonElement cq, CancellationToken ct)
    {
        if (cq.Obj("from") is not { } from || from.Long("id") is not { } userId) return;
        if (!_flood.Allow(userId)) { await AnswerCallback(cq, ct); return; }
        var user = store.Touch(userId);
        var data = cq.Str("data") ?? "";

        if (data.StartsWith("rev:", StringComparison.Ordinal)) { await AnswerCallback(cq, ct); await _private.OnReverse(cq, user, ct); }
        else if (data.StartsWith("m:", StringComparison.Ordinal) || data.StartsWith("set:", StringComparison.Ordinal)
                 || data.StartsWith("g:", StringComparison.Ordinal)) await _private.Menu.OnCallback(cq, user, ct);   // menyu, sozlama, o'yin — o'zi javob beradi
        else if (data.StartsWith("bc:", StringComparison.Ordinal)) await Admin.OnCallback(cq, user, ct);   // TASK10
        else await AnswerCallback(cq, ct);   // noma'lum — shunchaki javob
    }

    private async Task OnInlineQuery(JsonElement q, CancellationToken ct)   // TASK9
    {
        if (q.Obj("from") is not { } from || from.Long("id") is not { } userId || !_flood.Allow(userId)) return;   // limitdan oshsa — javobsiz
        await _inline.HandleQuery(q, ct);
    }

    private Task OnChosenInline(JsonElement c, CancellationToken ct) => _inline.HandleChosen(c);      // TASK9

    private async Task AnswerCallback(JsonElement cq, CancellationToken ct)
    {
        if (cq.Str("id") is not { } id) return;
        try { await sender.Send("answerCallbackQuery", new() { ["callback_query_id"] = id }, ct); }
        catch (BotApiException) { }   // eskirgan callback — kerakli emas
    }
}
