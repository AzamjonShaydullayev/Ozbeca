using System.Collections.Concurrent;
using System.Text.Json;
using Ozbeca.Bot.Api;

namespace Ozbeca.Bot;

/// <summary>getUpdates long polling. Har update alohida Task'da (bir vaqtda 16 tagacha); xato siklni to'xtatmaydi.</summary>
public sealed class Poller(BotApi api, Router router)
{
    private static readonly string[] Allowed = ["message", "callback_query", "inline_query", "chosen_inline_result", "widget_query"];

    /// <summary>getUpdates tarmoq xatosidan keyingi kutish (testlarda nol).</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>401 (token yaroqsiz) dan keyingi kutish.</summary>
    public TimeSpan AuthRetryDelay { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Shundan ko'p handler kutib tursa yangi getUpdates so'ralmaydi (backpressure). Testlarda kichik.</summary>
    internal int MaxPending { get; init; } = DefaultMaxPending;
    internal const int DefaultMaxPending = 500;

    private static readonly string[] UserKinds = ["message", "callback_query", "inline_query", "chosen_inline_result", "widget_query"];
    private readonly object _lock = new();
    private readonly Dictionary<long, Task> _tails = new();

    private static long? UserKey(JsonElement u)
    {
        foreach (var k in UserKinds)
            if (u.Obj(k) is { } o && o.Obj("from") is { } f && f.Long("id") is { } id) return id;
        return null;
    }

    // Bir user'ning update'lari ketma-ket (oldingisi tugagach), turli user'lar parallel. Global chegara: gate.
    private Task Dispatch(JsonElement u, SemaphoreSlim gate)
    {
        async Task Run(Task? prev)
        {
            if (prev != null) await prev;   // prev hech qachon xato bermaydi (quyida ushlanadi)
            await gate.WaitAsync();
            try { await router.Handle(u, CancellationToken.None); }   // yopilishda ham boshlangan javob tugasin
            catch (Exception e) { Log.Error($"update={Router.Kind(u)} {Describe(e)}"); }
            finally { gate.Release(); }
        }

        if (UserKey(u) is not { } key) return Task.Run(() => Run(null));
        lock (_lock)
        {
            _tails.TryGetValue(key, out var prev);
            var task = Task.Run(() => Run(prev));
            _tails[key] = task;
            _ = task.ContinueWith(t =>
            {
                lock (_lock) if (_tails.TryGetValue(key, out var cur) && cur == t) _tails.Remove(key);
            }, TaskScheduler.Default);
            return task;
        }
    }

    internal int PendingUsers { get { lock (_lock) return _tails.Count; } }

    public async Task RunAsync(CancellationToken ct)
    {
        var gate = new SemaphoreSlim(16);
        var running = new ConcurrentDictionary<Task, byte>();
        long offset = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Backpressure: handler'lar ulgurmasa yangi update olmaymiz (Telegram ularni saqlab turadi).
                while (running.Count > MaxPending) await Task.Delay(50, ct);
                JsonElement updates;
                try
                {
                    updates = await api.Call("getUpdates", new { offset, timeout = 50, allowed_updates = Allowed }, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    Log.Error("getUpdates: " + Describe(e));
                    var delay = e is BotApiException { Code: 401 } ? AuthRetryDelay : RetryDelay;
                    try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
                    continue;
                }
                if (updates.ValueKind != JsonValueKind.Array) continue;

                foreach (var u in updates.EnumerateArray())
                {
                    if (u.Long("update_id") is { } id) offset = Math.Max(offset, id + 1);
                    var task = Dispatch(u, gate);
                    running[task] = 0;
                    _ = task.ContinueWith(t => running.TryRemove(t, out _), TaskScheduler.Default);
                }
            }
        }
        catch (OperationCanceledException) { }   // gate.WaitAsync bekor qilindi
        await Task.WhenAny(Task.WhenAll(running.Keys), Task.Delay(TimeSpan.FromSeconds(10)));   // ishlayotgan handler'larni kutamiz (ko'pi bilan 10 s)
    }

    // Tur, kod/metod va Telegram'ning description'i (Telegram matni, tokensiz; 200 belgi, bitta qator) — 400 ni journald'dan
    // tushunish uchun. Boshqa xatolarda faqat tur: ularning matnida URL (token) bo'lishi mumkin.
    internal static string Describe(Exception e)
    {
        if (e is not BotApiException b) return e.GetType().Name;
        var d = b.Description.ReplaceLineEndings(" ");
        if (d.Length > 200) d = d[..(char.IsHighSurrogate(d[199]) ? 199 : 200)];
        return $"{nameof(BotApiException)} code={b.Code} method={b.Data["method"] ?? "?"} desc={d}";
    }
}
