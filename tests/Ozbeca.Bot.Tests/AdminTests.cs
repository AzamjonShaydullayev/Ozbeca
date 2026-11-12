using Ozbeca.Bot.Api;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

[Collection("Clock")]
public class AdminTests
{
    private static readonly object[] Cmd0 = [new { type = "bot_command", offset = 0, length = 5 }];
    private const string Blocked403 = "{\"ok\":false,\"error_code\":403,\"description\":\"Forbidden: bot was blocked by the user\"}";

    private static Broadcaster Make(BotHarness h, int delayMs = 0) =>
        new(h.Api, h.Store, TimeSpan.FromMilliseconds(delayMs)) { Wait = (_, _) => Task.CompletedTask };

    [Fact]
    public async Task Stat_only_for_admin()
    {
        using var h = new BotHarness();
        h.Store.AddUsage("text", 120);
        await h.Text("/stat", uid: 1, entities: Cmd0);
        Assert.DoesNotContain(h.Sent(), b => b.Str("text")!.Contains(Texts.StatTitle));
        Assert.Contains(h.Sent(), b => b.Str("text") == Texts.Help);

        await h.Text("/stat", uid: 99, entities: Cmd0);
        var stat = h.Sent().Last();
        Assert.Equal(99, stat.Long("chat_id"));
        Assert.Contains(Texts.StatTitle, stat.Str("text"));
        Assert.Contains("120", stat.Str("text"));
    }

    [Fact]
    public async Task Send_as_reply_asks_confirmation_with_buttons()
    {
        using var h = new BotHarness();
        h.Store.Touch(1); h.Store.Touch(2);
        await h.Msg(new() { ["text"] = "/send", ["entities"] = new object[] { new { type = "bot_command", offset = 0, length = 5 } },
            ["reply_to_message"] = new { message_id = 77 } }, uid: 99);
        var m = h.Sent().Last();
        Assert.StartsWith("3 ", m.Str("text"));   // 1, 2 va adminning o'zi
        var btns = m.GetProperty("reply_markup").GetProperty("inline_keyboard")[0];
        Assert.Equal("bc:go:77", btns[0].Str("callback_data"));
        Assert.Equal("success", btns[0].Str("style"));
        Assert.Equal("bc:no", btns[1].Str("callback_data"));
        Assert.Equal("danger", btns[1].Str("style"));

        await h.Text("/send", uid: 99, entities: Cmd0);   // reply'siz
        Assert.Equal(Texts.BroadcastAsk, h.Sent().Last().Str("text"));
    }

    [Fact]
    public async Task Broadcast_callback_from_non_admin_is_ignored()
    {
        using var h = new BotHarness();
        h.Store.Touch(5);
        await h.Callback("bc:go:77", null, null, uid: 5);
        Assert.Empty(h.Sent("copyMessage"));
        Assert.Single(h.Sent("answerCallbackQuery"));
    }

    [Fact]
    public async Task Broadcast_marks_403_blocked()
    {
        using var h = new BotHarness();
        foreach (var id in new long[] { 1, 2, 3 }) h.Store.Touch(id);
        h.Fake.Respond = (m, b) => m == "copyMessage" && b.Long("chat_id") == 2 ? Blocked403 : FakeTelegram.DefaultResponse;
        var (sent, failed) = await Make(h).Run(99, 5, [1, 2, 3], new Progress<(int, int, int)>(), CancellationToken.None);
        Assert.Equal((2, 1), (sent, failed));
        Assert.Equal(1, h.Store.GetStats().Blocked);
        Assert.True(h.Store.Get(2)!.Blocked);
    }

    [Fact]
    public async Task Broadcast_retries_after_429()
    {
        using var h = new BotHarness();
        h.Store.Touch(1);
        var n = 0;
        h.Fake.Respond = (m, _) => m == "copyMessage" && n++ == 0
            ? "{\"ok\":false,\"error_code\":429,\"description\":\"Too Many Requests\",\"parameters\":{\"retry_after\":3}}"
            : FakeTelegram.DefaultResponse;
        TimeSpan? waited = null;
        var b = Make(h); b.Wait = (t, _) => { waited = t; return Task.CompletedTask; };
        var (sent, failed) = await b.Run(99, 5, [1], new Progress<(int, int, int)>(), CancellationToken.None);
        Assert.Equal((1, 0), (sent, failed));
        Assert.Equal(TimeSpan.FromSeconds(3), waited);
        Assert.Equal(2, h.Sent("copyMessage").Count);
    }

    [Fact]
    public async Task Stop_cancels_broadcast()
    {
        using var h = new BotHarness();
        var ids = Enumerable.Range(1, 20).Select(i => (long)i).ToList();
        foreach (var id in ids) h.Store.Touch(id);
        using var cts = new CancellationTokenSource();
        var b = Make(h, 50); b.Wait = Task.Delay;
        var task = b.Run(99, 5, ids, new Progress<(int, int, int)>(), cts.Token);
        await Task.Delay(120);
        cts.Cancel();
        var (sent, _) = await task;
        Assert.InRange(sent, 1, 19);
    }

    [Fact]
    public async Task Second_broadcast_while_running_is_refused()
    {
        using var h = new BotHarness();
        for (long i = 1; i <= 100; i++) h.Store.Touch(i);
        await h.Callback("bc:go:77", null, null, uid: 99);
        var run = h.Router.Admin.Current;
        Assert.NotNull(run);
        await h.Callback("bc:go:77", null, null, uid: 99);
        Assert.Contains(h.Sent("answerCallbackQuery"), b => b.Str("text") == Texts.BroadcastBusy);

        await h.Callback("bc:stop", null, null, uid: 99);
        await run!;
        Assert.Contains(h.Sent("editMessageText"), b => b.Str("text")!.StartsWith(Texts.BroadcastStopped));
        Assert.True(h.Sent("copyMessage").Count < 100);
    }

    [Fact]
    public async Task Http_timeout_counts_as_failed_and_run_continues()
    {
        using var h = new BotHarness();
        foreach (var id in new long[] { 1, 2, 3 }) h.Store.Touch(id);
        h.Fake.Respond = (m, b) => m == "copyMessage" && b.Long("chat_id") == 2 ? throw new TaskCanceledException() : FakeTelegram.DefaultResponse;
        var (sent, failed) = await Make(h).Run(99, 5, [1, 2, 3], new Progress<(int, int, int)>(), CancellationToken.None);
        Assert.Equal((2, 1), (sent, failed));
    }

    [Fact]
    public async Task Progress_edit_network_error_keeps_single_flight_and_final_message()
    {
        using var h = new BotHarness();
        for (long i = 1; i <= 30; i++) h.Store.Touch(i);
        h.Router.Admin.ProgressEvery = TimeSpan.FromMilliseconds(20);
        h.Fake.Respond = (m, b) => m == "editMessageText" && b.Str("text")!.StartsWith(Texts.BroadcastRunning)
            ? throw new HttpRequestException("net") : FakeTelegram.DefaultResponse;
        await h.Callback("bc:go:77", null, null, uid: 99);
        var run = h.Router.Admin.Current!;
        await Task.Delay(150);
        Assert.False(run.IsCompleted);
        await h.Callback("bc:go:78", null, null, uid: 99);
        Assert.Contains(h.Sent("answerCallbackQuery"), b => b.Str("text") == Texts.BroadcastBusy);
        await run;
        Assert.Equal(31, h.Sent("copyMessage").Count);   // 30 + admin, faqat bitta tarqatish
        Assert.Contains(h.Sent("editMessageText"), b => b.Str("text")!.StartsWith(Texts.BroadcastDone));
    }

    [Fact]
    public async Task Late_bc_go_for_same_message_is_refused_and_buttons_removed()
    {
        using var h = new BotHarness();
        h.Store.Touch(1);
        await h.Callback("bc:go:77", null, null, uid: 99);
        await h.Router.Admin.Current!;
        var copies = h.Sent("copyMessage").Count;
        Assert.Contains(h.Sent("editMessageText"), b => b.Str("text") == Texts.BroadcastRunning && b.Long("message_id") == 5
            && (!b.TryGetProperty("reply_markup", out var rm) || rm.ValueKind == System.Text.Json.JsonValueKind.Null));
        await h.Callback("bc:go:77", null, null, uid: 99);
        Assert.Equal(copies, h.Sent("copyMessage").Count);
        Assert.Contains(h.Sent("answerCallbackQuery"), b => b.Str("text") == Texts.BroadcastDone);
    }

    [Fact]
    public async Task Unparseable_bc_go_is_answered_and_starts_nothing()
    {
        using var h = new BotHarness();
        await h.Callback("bc:go:xyz", null, null, uid: 99);
        Assert.Single(h.Sent("answerCallbackQuery"));
        Assert.Empty(h.Sent("copyMessage"));
        Assert.Null(h.Router.Admin.Current);
    }

    [Fact]
    public async Task Same_message_id_from_two_admins_both_broadcast()
    {
        using var h = new BotHarness();
        h.Store.Touch(1);
        var admin = new Ozbeca.Bot.Handlers.AdminHandler(h.Api, new Sender(h.Api), h.Store, h.Config with { Admins = new HashSet<long> { 98, 99 } });
        foreach (var uid in new long[] { 99, 98 })
        {
            var cq = BotHarness.Json(new { id = "cb" + uid, from = new { id = uid, is_bot = false }, data = "bc:go:77",
                message = new { message_id = 5, date = 1, chat = new { id = uid, type = "private" } } });
            await admin.OnCallback(cq, h.Store.Touch(uid), CancellationToken.None);
            await admin.Current!;
        }
        Assert.Contains(h.Sent("copyMessage"), b => b.Long("from_chat_id") == 99);
        Assert.Contains(h.Sent("copyMessage"), b => b.Long("from_chat_id") == 98);
        Assert.DoesNotContain(h.Sent("answerCallbackQuery"), b => b.Str("text") == Texts.BroadcastDone);
    }
}
