using System.Text.Json;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

[Collection("Clock")]   // Store kunini (Clock) o'qiydi — Clock'ni almashtiruvchi testlar bilan parallel emas
public sealed class InlineTests : IDisposable
{
    private readonly BotHarness _h = new();
    public void Dispose() => _h.Dispose();

    private Task Query(string q, long uid = 7) => _h.Handle(new
    {
        update_id = 1,
        inline_query = new { id = "iq1", from = new { id = uid, is_bot = false }, query = q, offset = "" },
    });

    private JsonElement Answer() => Assert.Single(_h.Sent("answerInlineQuery"));

    private static string Msg(JsonElement r) => r.GetProperty("input_message_content").GetProperty("message_text").GetString()!;

    [Fact]
    public async Task Two_results_new_and_old()
    {
        await Query("Shahar");
        var a = Answer();
        Assert.Equal("iq1", a.GetProperty("inline_query_id").GetString());
        var rs = a.GetProperty("results");
        Assert.Equal(2, rs.GetArrayLength());
        Assert.Equal("n", rs[0].GetProperty("id").GetString());
        Assert.Equal(Texts.InlineNewTitle, rs[0].GetProperty("title").GetString());
        Assert.Equal("Şahar", Msg(rs[0]));
        Assert.Equal("o", rs[1].GetProperty("id").GetString());
        Assert.Equal(Texts.InlineOldTitle, rs[1].GetProperty("title").GetString());
        Assert.Equal("Shahar", Msg(rs[1]));
        Assert.Equal(10, a.GetProperty("cache_time").GetInt32());
        Assert.True(a.GetProperty("is_personal").GetBoolean());
        var btn = a.GetProperty("button");
        Assert.Equal(Texts.InlineLongButton, btn.GetProperty("text").GetString());
        Assert.Equal("inline", btn.GetProperty("start_parameter").GetString());

    }

    [Fact]
    public async Task Already_new_query_still_gives_old()
    {
        await Query("Şahar");
        var rs = Answer().GetProperty("results");
        Assert.Equal("Shahar", Msg(rs[1]));
    }

    [Fact]
    public async Task Message_text_has_no_decoration()
    {
        await Query("Shahar choy");
        var rs = Answer().GetProperty("results");
        foreach (var r in rs.EnumerateArray())
        {
            Assert.False(r.GetProperty("input_message_content").TryGetProperty("entities", out _));
            Assert.Equal(r.GetProperty("description").GetString(), Msg(r));
        }
    }

    [Fact]
    public async Task Description_capped_at_100()
    {
        await Query(new string('a', 250));
        var rs = Answer().GetProperty("results");
        Assert.Equal(100, rs[0].GetProperty("description").GetString()!.Length);
        Assert.Equal(250, Msg(rs[0]).Length);
    }

    [Fact]
    public async Task Cuts_do_not_split_surrogate_pairs()
    {
        await Query(new string('a', 255) + "😀");   // 257 birlik: 256 da kesish juftni bo'lardi
        Assert.Equal(new string('a', 255), Msg(Answer().GetProperty("results")[0]));

        using var h2 = new BotHarness();
        await h2.Handle(new { update_id = 1, inline_query = new { id = "iq2", from = new { id = 7, is_bot = false }, query = new string('a', 99) + "😀b", offset = "" } });
        var r = Assert.Single(h2.Sent("answerInlineQuery")).GetProperty("results")[0];
        Assert.Equal(new string('a', 99), r.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Empty_query_returns_button_only()
    {
        await Query("  ");
        var a = Answer();
        Assert.Equal(0, a.GetProperty("results").GetArrayLength());
        Assert.Equal("inline", a.GetProperty("button").GetProperty("start_parameter").GetString());
    }

    [Fact]
    public async Task Cyrillic_query_converted_when_user_setting_on()
    {
        _h.Store.Touch(7);
        await Query("Шаҳар");
        Assert.Equal("Şahar", Msg(Answer().GetProperty("results")[0]));
    }

    [Fact]
    public async Task Cyrillic_query_untouched_when_setting_off()
    {
        _h.Store.Touch(7);
        _h.Store.SaveSettings(7, new Settings(Cyrillic: false));
        await Query("Шаҳар");
        Assert.Equal("Шаҳар", Msg(Answer().GetProperty("results")[0]));
    }

    [Fact]
    public async Task Query_does_not_create_user()
    {
        await Query("Shahar");
        Assert.Null(_h.Store.Get(7));
    }

    [Fact]
    public async Task Chosen_result_counts_inline_usage()
    {
        await _h.Handle(new
        {
            update_id = 2,
            chosen_inline_result = new { result_id = "n", from = new { id = 7, is_bot = false }, query = "Shahar" },
        });
        Assert.Equal((1, 6L), _h.Store.GetStats().Today["inline"]);
        Assert.Null(_h.Store.Get(7));

        _h.Store.Touch(7);   // botdan foydalanuvchi — shaxsiy statistikaga ham yoziladi
        await _h.Handle(new
        {
            update_id = 3,
            chosen_inline_result = new { result_id = "n", from = new { id = 7, is_bot = false }, query = "Shahar" },
        });
        Assert.Equal((6L, 0), _h.Store.UserTotals(7));
    }
}
