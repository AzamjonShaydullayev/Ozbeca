using System.Text.Json;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

public sealed class PrivateHandlerTests : IDisposable
{
    private readonly BotHarness _h = new();
    public void Dispose() => _h.Dispose();

    private static string TextOf(JsonElement b) => b.GetProperty("text").GetString()!;

    private static JsonElement Entity(JsonElement b, string type) =>
        b.GetProperty("entities").EnumerateArray().Single(e => e.GetProperty("type").GetString() == type);

    private static List<JsonElement> Buttons(JsonElement b) =>
        b.GetProperty("reply_markup").GetProperty("inline_keyboard")[0].EnumerateArray().ToList();

    [Fact]
    public async Task Text_converted_with_quote_and_buttons()
    {
        await _h.Text("Shahar choy");
        var b = Assert.Single(_h.Sent());
        var text = TextOf(b);
        Assert.Contains(Texts.NewHeader, text);
        var at = text.IndexOf("Şahar çoy", StringComparison.Ordinal);
        Assert.True(at > 0);
        var q = Entity(b, "blockquote");
        Assert.Equal(at, q.GetProperty("offset").GetInt32());
        Assert.Equal(9, q.GetProperty("length").GetInt32());
        var btns = Buttons(b);
        Assert.Equal("Şahar çoy", btns[0].GetProperty("copy_text").GetProperty("text").GetString());
        Assert.Equal("rev:n", btns[1].GetProperty("callback_data").GetString());
        Assert.Equal(11, _h.Store.Get(1)!.CharsToday);
        Assert.Equal((1, 11L), _h.Store.GetStats().Today["text"]);
    }

    [Fact]
    public async Task Reverse_setting_converts_to_old()
    {
        _h.Store.Touch(1);
        _h.Store.SaveSettings(1, new Settings(Reverse: true));
        await _h.Text("Şahar");
        var b = Assert.Single(_h.Sent());
        Assert.EndsWith("Shahar", TextOf(b));
        Assert.Contains(Texts.OldHeader, TextOf(b));
        Assert.Equal("rev:o", Buttons(b)[1].GetProperty("callback_data").GetString());
    }

    [Fact]
    public async Task Unchanged_text_replies_no_change()
    {
        await _h.Text("Şahar");
        Assert.Equal(Texts.NoChange, TextOf(Assert.Single(_h.Sent())));
        Assert.Equal(0, _h.Store.Get(1)!.CharsToday);
        Assert.Empty(_h.Store.GetStats().Today);
    }

    [Fact]
    public async Task User_bold_is_preserved()
    {
        await _h.Text("Shahar choy", entities: new[] { new { type = "bold", offset = 0, length = 6 } });
        var b = Assert.Single(_h.Sent());
        var bold = Entity(b, "bold");
        Assert.Equal(Entity(b, "blockquote").GetProperty("offset").GetInt32(), bold.GetProperty("offset").GetInt32());
        Assert.Equal(5, bold.GetProperty("length").GetInt32());
    }

    [Fact]
    public async Task Photo_caption_is_converted()
    {
        await _h.Msg(new() { ["photo"] = new[] { new { file_id = "x" } }, ["caption"] = "Shahar" });
        var b = Assert.Single(_h.Sent());
        Assert.EndsWith("Şahar", TextOf(b));
        Assert.Empty(_h.Sent("sendPhoto"));
    }

    [Fact]
    public async Task Sticker_gets_send_text_hint()
    {
        await _h.Msg(new() { ["sticker"] = new { file_id = "x" } });
        Assert.Equal(Texts.SendText, TextOf(Assert.Single(_h.Sent())));
    }

    [Fact]
    public async Task Reverse_button_converts_quote_back()
    {
        var shown = "✅ Yangi alifboda:\nŞahar çoy";
        var off = shown.IndexOf('Ş');
        await _h.Callback("rev:n", shown, new[]
        {
            new { type = "blockquote", offset = off, length = 9 },
            new { type = "bold", offset = off, length = 5 },
        });
        var b = Assert.Single(_h.Sent());
        Assert.EndsWith("Shahar choy", TextOf(b));
        Assert.Contains(Texts.OldHeader, TextOf(b));
        Assert.Equal(6, Entity(b, "bold").GetProperty("length").GetInt32());
        Assert.Equal("rev:o", Buttons(b)[^1].GetProperty("callback_data").GetString());
        Assert.Single(_h.Sent("answerCallbackQuery"));
    }

    [Fact]
    public async Task Reverse_button_on_inaccessible_message_only_answers()
    {
        await _h.Callback("rev:n", null, null);
        Assert.Empty(_h.Sent());
        Assert.Single(_h.Sent("answerCallbackQuery"));
    }

    [Fact]
    public async Task Group_message_ignored()
    {
        await _h.Text("Shahar", chatType: "group");
        await _h.Msg(new() { ["text"] = "Shahar" }, isBot: true);
        Assert.Empty(_h.Fake.Calls);
    }

    [Fact]
    public async Task Commands()
    {
        await _h.Text("/start inline");
        await _h.Text("/help@ozbecauzbot");
        await _h.Text("/nimadir");
        var s = _h.Sent();
        Assert.Contains(Texts.Greeting, TextOf(s[0]));
        Assert.Equal(Texts.Help, TextOf(s[1]));
        Assert.Equal(Texts.Help, TextOf(s[2]));
    }

    [Fact]
    public async Task Document_is_routed_to_file_hook()
    {
        await _h.Msg(new() { ["document"] = new { file_id = "x" } });
        Assert.Equal(Texts.FileUnsupported, TextOf(Assert.Single(_h.Sent())));
    }

    private static string Long() => string.Concat(Enumerable.Repeat("Shahar ", 1700));

    [Fact]
    public async Task Long_text_split_into_parts()
    {
        await _h.Text(Long());
        var parts = _h.Sent();
        Assert.Equal(3, parts.Count);
        Assert.All(parts, p => Assert.True(TextOf(p).Length <= 4096));
        Assert.Contains(Texts.NewHeader, TextOf(parts[0]));
        Assert.DoesNotContain(Texts.NewHeader, TextOf(parts[1]));
        Assert.False(parts[0].TryGetProperty("reply_markup", out _));
        Assert.False(parts[1].TryGetProperty("reply_markup", out _));
        Assert.Single(Buttons(parts[2]));   // Copy yo'q (> 256), faqat Teskari
        Assert.All(parts, p => Entity(p, "expandable_blockquote"));
    }

    [Fact]
    public async Task Long_text_as_file_when_enabled()
    {
        _h.Store.Touch(1);
        _h.Store.SaveSettings(1, new Settings(LongAsFile: true));
        await _h.Text(Long());
        Assert.Empty(_h.Sent());
        var d = Assert.Single(_h.Sent("sendDocument"));
        Assert.Equal("file:natija.txt", d.GetProperty("document").GetString());
        Assert.Contains(Texts.NewHeader, d.GetProperty("caption").GetString());
    }

    [Fact]
    public async Task Flood_limit_blocks_31st_message()
    {
        for (var i = 0; i < 30; i++) await _h.Text("Shahar");
        Assert.Equal(30, _h.Sent().Count);
        await _h.Text("Shahar");
        Assert.Equal(Texts.Flood, TextOf(_h.Sent()[30]));
        await _h.Text("Shahar");
        await _h.Text("Shahar");
        Assert.Equal(31, _h.Sent().Count);   // keyingilari jim tashlanadi
        await _h.Text("Shahar", uid: 2);     // boshqa user ta'sirlanmaydi
        Assert.Equal(32, _h.Sent().Count);
    }
}
