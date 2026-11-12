using System.Text.Json;

namespace Ozbeca.Bot.Tests;

public sealed class PollerTests
{
    private static string Upd(int id, int uid, string text) =>
        $"{{\"update_id\":{id},\"message\":{{\"message_id\":1,\"date\":1,\"chat\":{{\"id\":{uid},\"type\":\"private\"}},\"from\":{{\"id\":{uid},\"is_bot\":false}},\"text\":\"{text}\"}}}}";

    [Fact]
    public async Task Poller_handles_batch_advances_offset_then_stops_on_cancel()
    {
        using var h = new BotHarness();
        using var cts = new CancellationTokenSource();
        var polls = 0;
        h.Fake.Respond = (method, _) =>
        {
            if (method != "getUpdates") return FakeTelegram.DefaultResponse;
            if (++polls == 1)
                return "{\"ok\":true,\"result\":[" + Upd(7, 1, "Shahar") + "," + Upd(8, 2, "choy") + "]}";
            cts.Cancel();
            return """{"ok":true,"result":[]}""";
        };
        await new Poller(h.Api, h.Router).RunAsync(cts.Token);

        Assert.Equal(2, h.Sent().Count);
        var polls2 = h.Sent("getUpdates");
        Assert.Equal(50, polls2[0].GetProperty("timeout").GetInt32());
        Assert.Equal(5, polls2[0].GetProperty("allowed_updates").GetArrayLength());
        Assert.Equal(9, polls2[1].GetProperty("offset").GetInt64());
    }

    [Fact]
    public async Task Poller_survives_handler_and_network_errors()
    {
        using var h = new BotHarness();
        using var cts = new CancellationTokenSource();
        var polls = 0;
        h.Fake.RespondRaw = (method, _) =>
        {
            if (method == "sendMessage") return (500, """{"ok":false,"error_code":500,"description":"boom"}""");
            if (++polls == 1) return (502, "bad gateway");   // getUpdates xatosi
            if (polls == 2)
                return (200, "{\"ok\":true,\"result\":[" + Upd(1, 1, "Shahar") + "]}");
            cts.Cancel();
            return (200, """{"ok":true,"result":[]}""");
        };
        await new Poller(h.Api, h.Router) { RetryDelay = TimeSpan.Zero }.RunAsync(cts.Token);
        Assert.Equal(3, polls);   // xatolardan keyin ham sikl davom etdi
    }

    // sendMessage javoblari tugash tartibini yozadi; "Shahar" (1-update) sekinlashtiriladi.
    private static async Task<List<string>> RunSlowFirst(BotHarness h, string updates)
    {
        using var cts = new CancellationTokenSource();
        var polls = 0;
        var done = new List<string>();
        h.Fake.Respond = (method, body) =>
        {
            if (method == "sendMessage")
            {
                var first = body.GetProperty("text").GetString()!.EndsWith("Şahar");
                if (first) Thread.Sleep(400);
                lock (done) done.Add(first ? "first" : "second");
                return FakeTelegram.DefaultResponse;
            }
            if (method != "getUpdates") return FakeTelegram.DefaultResponse;
            if (++polls == 1) return "{\"ok\":true,\"result\":[" + updates + "]}";
            cts.Cancel();
            return "{\"ok\":true,\"result\":[]}";
        };
        var poller = new Poller(h.Api, h.Router);
        await poller.RunAsync(cts.Token);
        await Task.Delay(50);   // ContinueWith tozalashi
        Assert.Equal(0, poller.PendingUsers);   // yozuvlar tozalandi
        return done;
    }

    [Fact]
    public async Task Same_user_updates_are_processed_in_order()
    {
        using var h = new BotHarness();
        var done = await RunSlowFirst(h, Upd(1, 5, "Shahar") + "," + Upd(2, 5, "choy"));
        Assert.Equal(["first", "second"], done);
    }

    [Fact]
    public async Task Different_users_run_concurrently()
    {
        using var h = new BotHarness();
        var done = await RunSlowFirst(h, Upd(1, 5, "Shahar") + "," + Upd(2, 6, "choy"));
        Assert.Equal(["second", "first"], done);
    }

    [Fact]
    public async Task Unauthorized_backs_off_long_and_stays_cancellable()
    {
        using var h = new BotHarness();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var polls = 0;
        h.Fake.RespondRaw = (_, _) => { polls++; return (401, "{\"ok\":false,\"error_code\":401,\"description\":\"Unauthorized\"}"); };
        await new Poller(h.Api, h.Router) { RetryDelay = TimeSpan.Zero, AuthRetryDelay = TimeSpan.FromHours(1) }.RunAsync(cts.Token);
        Assert.Equal(1, polls);
    }
}
