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
}
