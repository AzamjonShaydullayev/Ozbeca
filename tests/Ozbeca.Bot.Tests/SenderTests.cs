using Ozbeca.Bot.Api;
using Ozbeca.Bot.Text;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

public class SenderTests
{
    private static Dictionary<string, object?> Payload() => new()
    {
        ["chat_id"] = 1,
        ["text"] = "A b",
        ["entities"] = new List<object>
        {
            new Entity("custom_emoji", 0, 1, CustomEmojiId: "99").ToJson(),
            new Entity("bold", 2, 1).ToJson(),
        },
        ["reply_markup"] = Buttons.Markup(new object[]
        {
            Buttons.Callback("T", "d", "primary", "111"), Buttons.Copy("Nusxa", "c"),
        }),
    };

    [Fact]
    public async Task Sender_retries_without_custom_emoji_on_400()
    {
        var fake = new FakeTelegram
        {
            Respond = (_, _) => Flip()
        };
        var sender = new Sender(new BotApi(fake, "1:X"));
        var payload = Payload();
        await sender.Send("sendMessage", payload);

        var calls = fake.Calls;
        Assert.Equal(2, calls.Count);
        var first = calls[0].Body.GetRawText();
        Assert.Contains("custom_emoji", first);
        Assert.Contains("icon_custom_emoji_id", first);
        var second = calls[1].Body;
        var ents = second.GetProperty("entities").EnumerateArray().ToList();
        Assert.Single(ents);
        Assert.Equal("bold", ents[0].GetProperty("type").GetString());
        var raw = second.GetRawText();
        Assert.DoesNotContain("icon_custom_emoji_id", raw);
        Assert.DoesNotContain("\"style\"", raw);
        Assert.Contains("copy_text", raw);
        // chaqiruvchining payload'i buzilmagan
        Assert.Equal(2, ((List<object>)payload["entities"]!).Count);
    }

    private bool _sent;
    private string Flip()
    {
        if (_sent) return FakeTelegram.DefaultResponse;
        _sent = true;
        return "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: invalid custom emoji identifier\"}";
    }

    [Fact]
    public async Task Sender_waits_retry_after_on_429()
    {
        var n = 0;
        var fake = new FakeTelegram
        {
            Respond = (_, _) => n++ == 0
                ? "{\"ok\":false,\"error_code\":429,\"description\":\"Too Many\",\"parameters\":{\"retry_after\":1}}"
                : FakeTelegram.DefaultResponse
        };
        TimeSpan? waited = null;
        var sender = new Sender(new BotApi(fake, "1:X")) { Delay = (t, _) => { waited = t; return Task.CompletedTask; } };
        await sender.Send("sendMessage", Payload());
        Assert.Equal(2, fake.Calls.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), waited);
    }

    [Fact]
    public async Task Sender_rethrows_other_errors()
    {
        var fake = new FakeTelegram
        {
            Respond = (_, _) => "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: chat not found\"}"
        };
        var sender = new Sender(new BotApi(fake, "1:X"));
        await Assert.ThrowsAsync<BotApiException>(() => sender.Send("sendMessage", Payload()));
        Assert.Single(fake.Calls);
    }

    [Fact]
    public async Task Null_values_are_not_sent()
    {
        var fake = new FakeTelegram();
        await new Sender(new BotApi(fake, "1:X")).Send("sendMessage", new()
        {
            ["chat_id"] = 1, ["text"] = "a", ["reply_markup"] = null,
            ["extra"] = new Dictionary<string, object?> { ["k"] = null, ["v"] = 1 },
        });
        var body = Assert.Single(fake.Calls).Body;
        Assert.DoesNotContain("null", body.GetRawText());
        Assert.False(body.TryGetProperty("reply_markup", out _));
        Assert.Equal(1, body.GetProperty("extra").GetProperty("v").GetInt32());
    }

    [Fact]
    public async Task Callback_answer_without_text_has_no_null_text()
    {
        using var h = new BotHarness();
        h.Store.Touch(5);
        await h.Callback("bc:go:77", null, null, uid: 5);   // admin emas: javob matnsiz
        var a = Assert.Single(h.Sent("answerCallbackQuery"));
        Assert.False(a.TryGetProperty("text", out _), a.GetRawText());
    }

    [Fact]
    public async Task Sender_strips_inside_inline_results()
    {
        var fake = new FakeTelegram { Respond = (_, _) => Flip() };
        var sender = new Sender(new BotApi(fake, "1:X"));
        var payload = new Dictionary<string, object?>
        {
            ["inline_query_id"] = "q",
            ["results"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "article", ["id"] = "1", ["title"] = "t",
                    ["input_message_content"] = new Dictionary<string, object?>
                    {
                        ["message_text"] = "A b",
                        ["entities"] = new List<object> { new Entity("custom_emoji", 0, 1, CustomEmojiId: "9").ToJson(), new Entity("bold", 2, 1).ToJson() },
                    },
                    ["reply_markup"] = Buttons.Markup(new object[] { Buttons.Callback("T", "d", "success", "5") }),
                },
            },
        };
        await sender.Send("answerInlineQuery", payload);
        var raw = fake.Calls[1].Body.GetRawText();
        Assert.DoesNotContain("custom_emoji", raw);
        Assert.DoesNotContain("icon_custom_emoji_id", raw);
        Assert.DoesNotContain("\"style\"", raw);
        Assert.Contains("bold", raw);
    }
}
