using System.Text;
using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Text;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Handlers;

/// <summary>/stat va /send (faqat cfg.Admins, from.id bo'yicha) va fon rejimidagi ommaviy xabar.</summary>
internal sealed class AdminHandler(BotApi api, Sender sender, Store store, BotConfig cfg)
{
    private readonly Broadcaster _broadcaster = new(api, store, TimeSpan.FromMilliseconds(40));   // ~25/s
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    /// <summary>Oxirgi broadcast vazifasi. Testlar kutishi uchun.</summary>
    internal Task? Current { get; private set; }
    internal TimeSpan ProgressEvery { get; set; } = TimeSpan.FromSeconds(5);

    public async Task<bool> TryHandle(JsonElement m, UserRow user, CancellationToken ct)
    {
        if (!cfg.Admins.Contains(user.Id) || m.Str("text") is not { } text) return false;
        JsonElement? ents = m.TryGetProperty("entities", out var e) ? e : null;
        if (!Entity.Parse(ents).Any(x => x.Type == "bot_command" && x.Offset == 0)) return false;
        switch (text.Split([' ', '\n'], 2, StringSplitOptions.RemoveEmptyEntries)[0].Split('@')[0].ToLowerInvariant())
        {
            case "/stat": await Say(user.Id, StatText(store.GetStats()), null, ct); return true;
            case "/send": await Send(m, user.Id, ct); return true;
            default: return false;
        }
    }

    private static string StatText(Stats s)
    {
        var sb = new StringBuilder(Texts.StatTitle).Append('\n')
            .Append($"{Texts.StatUsers}: {s.Users}\n{Texts.StatToday}: {s.ActiveToday}\n{Texts.StatWeek}: {s.Active7d}\n{Texts.StatBlocked}: {s.Blocked}");
        foreach (var (kind, label) in new[] { ("text", Texts.StatText), ("file", Texts.StatFile), ("inline", Texts.StatInline) })
        {
            var (count, chars) = s.Today.TryGetValue(kind, out var v) ? v : (0, 0L);
            sb.Append($"\n{label}: {count} ({chars} {Texts.StatChars})");
        }
        return sb.ToString();
    }

    private async Task Send(JsonElement m, long adminId, CancellationToken ct)
    {
        if (m.Obj("reply_to_message")?.Int("message_id") is not { } target) { await Say(adminId, Texts.BroadcastAsk, null, ct); return; }
        var markup = Buttons.Markup([Buttons.Callback(Texts.BroadcastGo, $"bc:go:{target}", "success"), Buttons.Callback(Texts.BroadcastNo, "bc:no", "danger")]);
        await Say(adminId, $"{store.ActiveUserIds().Count} {Texts.BroadcastConfirm}", markup, ct);
    }

    /// <summary>bc:* — faqat adminlar; boshqalarga shunchaki answerCallbackQuery.</summary>
    public async Task OnCallback(JsonElement cq, UserRow user, CancellationToken ct)
    {
        var data = cq.Str("data") ?? "";
        string? answer = null;
        if (cfg.Admins.Contains(user.Id))
        {
            if (data.StartsWith("bc:go:", StringComparison.Ordinal) && int.TryParse(data.AsSpan(6), out var target))
            {
                if (!Start(user.Id, target)) answer = Texts.BroadcastBusy;
            }
            else if (data == "bc:stop") { lock (_lock) _cts?.Cancel(); }
            else if (data == "bc:no" && cq.Obj("message")?.Int("message_id") is { } mid)
                await Edit(user.Id, mid, Texts.BroadcastCancelled, null, ct);
        }
        if (cq.Str("id") is not { } id) return;
        try { await sender.Send("answerCallbackQuery", new() { ["callback_query_id"] = id, ["text"] = answer }, ct); }
        catch (BotApiException) { }
    }

    private bool Start(long adminId, int target)
    {
        lock (_lock)
        {
            if (Current is { IsCompleted: false }) return false;
            var cts = _cts = new CancellationTokenSource();
            // poller token'i ataylab uzatilmaydi: broadcast per-user zanjirdan mustaqil, to'xtatish — bc:stop
            Current = Task.Run(() => RunBroadcast(adminId, target, cts.Token));
        }
        return true;
    }

    private async Task RunBroadcast(long adminId, int target, CancellationToken ct)
    {
        try
        {
            var ids = store.ActiveUserIds();
            var stopMarkup = Buttons.Markup([Buttons.Callback(Texts.BroadcastStop, "bc:stop", "danger")]);
            var first = await sender.Send("sendMessage", new() { ["chat_id"] = adminId, ["text"] = Texts.BroadcastRunning, ["reply_markup"] = stopMarkup });
            var progressMsg = first.Int("message_id");

            var last = (Sent: 0, Failed: 0, Left: ids.Count);
            var run = _broadcaster.Run(adminId, target, ids, new Progress(v => last = v), ct);
            while (await Task.WhenAny(run, Task.Delay(ProgressEvery)) != run)
                if (progressMsg is { } pm) await Edit(adminId, pm, Summary(Texts.BroadcastRunning, last), stopMarkup, default);
            var (sent, failed) = await run;
            var final = Summary(ct.IsCancellationRequested ? Texts.BroadcastStopped : Texts.BroadcastDone, (sent, failed, ids.Count - sent - failed));
            if (progressMsg is { } m) await Edit(adminId, m, final, null, default);
            else await Say(adminId, final, null, default);
        }
        catch (Exception e) { Log.Error($"broadcast: {e.GetType().Name}"); }
    }

    private static string Summary(string title, (int Sent, int Failed, int Left) v) =>
        $"{title}\n{Texts.BroadcastSent}: {v.Sent}\n{Texts.BroadcastFailed}: {v.Failed}\n{Texts.BroadcastLeft}: {v.Left}";

    private Task Say(long chatId, string text, object? markup, CancellationToken ct) =>
        sender.Send("sendMessage", new() { ["chat_id"] = chatId, ["text"] = text, ["reply_markup"] = markup }, ct);

    private async Task Edit(long chatId, int messageId, string text, object? markup, CancellationToken ct)
    {
        try { await sender.Send("editMessageText", new() { ["chat_id"] = chatId, ["message_id"] = messageId, ["text"] = text, ["reply_markup"] = markup }, ct); }
        catch (BotApiException) { }   // "message is not modified" va h.k. — muhim emas
    }

    private sealed class Progress(Action<(int, int, int)> set) : IProgress<(int Sent, int Failed, int Left)>
    {
        public void Report((int Sent, int Failed, int Left) v) => set(v);
    }
}
