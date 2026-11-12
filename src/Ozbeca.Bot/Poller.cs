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

    public async Task RunAsync(CancellationToken ct)
    {
        var gate = new SemaphoreSlim(16);
        var running = new ConcurrentDictionary<Task, byte>();
        long offset = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                JsonElement updates;
                try
                {
                    updates = await api.Call("getUpdates", new { offset, timeout = 50, allowed_updates = Allowed }, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    Log.Error("getUpdates: " + Describe(e));
                    try { await Task.Delay(RetryDelay, ct); } catch (OperationCanceledException) { break; }
                    continue;
                }
                if (updates.ValueKind != JsonValueKind.Array) continue;

                foreach (var u in updates.EnumerateArray())
                {
                    if (u.Long("update_id") is { } id) offset = Math.Max(offset, id + 1);
                    await gate.WaitAsync(ct);
                    var task = Task.Run(async () =>
                    {
                        try { await router.Handle(u, CancellationToken.None); }   // yopilishda ham boshlangan javob tugasin
                        catch (Exception e) { Log.Error($"update={Router.Kind(u)} {Describe(e)}"); }
                        finally { gate.Release(); }
                    }, CancellationToken.None);
                    running[task] = 0;
                    _ = task.ContinueWith(t => running.TryRemove(t, out _), TaskScheduler.Default);
                }
            }
        }
        catch (OperationCanceledException) { }   // gate.WaitAsync bekor qilindi
        await Task.WhenAny(Task.WhenAll(running.Keys), Task.Delay(TimeSpan.FromSeconds(10)));   // ishlayotgan handler'larni kutamiz (ko'pi bilan 10 s)
    }

    // Faqat tur va kod/metod: xabar matni va token yozilmaydi.
    private static string Describe(Exception e) =>
        e is BotApiException b ? $"{nameof(BotApiException)} code={b.Code} method={b.Data["method"] ?? "?"}" : e.GetType().Name;
}
