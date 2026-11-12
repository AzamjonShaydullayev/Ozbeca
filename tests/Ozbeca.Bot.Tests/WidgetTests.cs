using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;
using Ozbeca.Bot.Widget;

namespace Ozbeca.Bot.Tests;

public sealed class WidgetTests : IDisposable
{
    private readonly BotHarness _h = new();
    public void Dispose() => _h.Dispose();

    private const string Targets = "ÖĞŞÇöğşç";

    private Task Query(string data, long uid = 1, bool inline = false) =>
        _h.Handle(new
        {
            update_id = 1,
            widget_query = inline
                ? (object)new { id = "wq", from = new { id = uid, is_bot = false }, inline_message_id = "im1", chat_instance = "ci", type = new { type = "data" }, data }
                : new { id = "wq", from = new { id = uid, is_bot = false }, chat = new { id = uid, type = "private" }, message_id = 7, chat_instance = "ci", type = new { type = "data" }, data },
        });

    private string? Answer() => Assert.Single(_h.Sent("answerWidgetQuery")).GetProperty("data").GetString();

    private static JsonElement ChatData(JsonElement body) => JsonDocument.Parse(body.GetProperty("chat_data").GetString()!).RootElement;

    private static object Cmd(int len) => new[] { new { type = "bot_command", offset = 0, length = len } };

    [Fact]
    public void Card_html_within_limits()
    {
        var bytes = Encoding.UTF8.GetBytes(CardSender.Html);
        Assert.True(bytes.Length <= 32768, $"html {bytes.Length} bayt");
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true)) z.Write(bytes);
        Assert.True(ms.Length <= 4096, $"zlib {ms.Length} bayt");
        Assert.Contains("TelegramWidget", CardSender.Html);
    }

    [Fact]
    public void State_fits_1024_bytes_for_longest_word()
    {
        var longest = Game.Words.MaxBy(w => Encoding.UTF8.GetByteCount(w))!;
        var u = new UserRow(long.MaxValue, new Settings(true, true, true, true), int.MaxValue, int.MaxValue, int.MaxValue, null, false);
        var json = WidgetState.Build(u, new Puzzle("?" + longest[1..], "ÖĞŞ", 'Ö'));
        Assert.True(Encoding.UTF8.GetByteCount(json) <= 1024);
        var s = JsonDocument.Parse(json).RootElement;
        Assert.Equal(1, s.GetProperty("d").GetInt32());
        Assert.Equal(int.MaxValue, s.GetProperty("b").GetInt32());
        Assert.Equal("ÖĞŞ", s.GetProperty("o").GetString());
        // puzzle yo'q — q/o yo'q
        Assert.False(JsonDocument.Parse(WidgetState.Build(u, null)).RootElement.TryGetProperty("q", out _));
    }

    [Fact]
    public void Every_game_word_has_target_letter()
    {
        Assert.True(Game.Words.Count >= 150, $"{Game.Words.Count} ta so'z");
        Assert.Equal(Game.Words.Count, Game.Words.Distinct().Count());
        foreach (var w in Game.Words)
        {
            Assert.True(w.IndexOfAny(Targets.ToCharArray()) >= 0, w);
            Assert.InRange(w.Length, 4, 10);
            Assert.Equal(w.ToLowerInvariant(), w);   // ismlar (bosh harf) yo'q
            Assert.DoesNotContain('\'', w);
        }
    }

    [Fact]
    public void Puzzle_options_contain_answer_once()
    {
        var rnd = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            var p = Game.Next(rnd);
            Assert.Equal(3, p.Options.Length);
            Assert.Equal(3, p.Options.Distinct().Count());
            Assert.Single(p.Options, c => c == p.Answer);
            Assert.All(p.Options, c => Assert.Contains(c, "ÖĞŞÇ"));
            Assert.Single(p.Masked, c => c == '?');
            Assert.Contains(p.Masked.Replace("?", char.ToLowerInvariant(p.Answer).ToString()), Game.Words);
            Assert.Equal(p, Game.Unpack(Game.Pack(p)));
        }
    }

    [Fact]
    public async Task Direction_request_saves_and_edits_card()
    {
        await Query("d:1");
        Assert.True(_h.Store.Get(1)!.Settings.Reverse);
        var edit = Assert.Single(_h.Sent("editMessageWidgetData"));
        Assert.Equal(1, edit.GetProperty("chat_id").GetInt64());
        Assert.Equal(7, edit.GetProperty("message_id").GetInt32());
        Assert.Contains("\"d\":1", edit.GetProperty("chat_data").GetString());
        Assert.Equal("ok", Answer());
        Assert.Equal("wq", Assert.Single(_h.Sent("answerWidgetQuery")).GetProperty("widget_query_id").GetString());
    }

    [Fact]
    public async Task Toggles_flip_settings()
    {
        await Query("t:c");
        await Query("t:w");
        await Query("t:f");
        var s = _h.Store.Get(1)!.Settings;
        Assert.False(s.Cyrillic);
        Assert.False(s.Dictionary);
        Assert.True(s.LongAsFile);
        var last = ChatData(_h.Sent("editMessageWidgetData")[^1]);
        Assert.Equal((0, 0, 1), (last.GetProperty("c").GetInt32(), last.GetProperty("w").GetInt32(), last.GetProperty("f").GetInt32()));
    }

    [Fact]
    public async Task Correct_answer_increments_streak_wrong_resets()
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, Game.Pack(new Puzzle("?ahar", "ÇŞÖ", 'Ş')), 2, 5);
        await Query("g:1");
        var u = _h.Store.Get(1)!;
        Assert.Equal((3, 5), (u.Streak, u.Best));
        Assert.Equal("ok", Answer());
        var next = Game.Unpack(u.GameAnswer)!;
        Assert.NotNull(next);
        var shown = ChatData(_h.Sent("editMessageWidgetData")[^1]);
        Assert.Equal(next.Masked, shown.GetProperty("q").GetString());
        Assert.Equal(3, shown.GetProperty("s").GetInt32());

        var wrong = next.Options.IndexOf(next.Options.First(c => c != next.Answer));
        await Query("g:" + wrong);
        u = _h.Store.Get(1)!;
        Assert.Equal((0, 5), (u.Streak, u.Best));
        Assert.Equal("no:" + next.Answer, _h.Sent("answerWidgetQuery")[^1].GetProperty("data").GetString());
        Assert.NotEqual(next, Game.Unpack(u.GameAnswer));
    }

    [Fact]
    public async Task Best_follows_streak()
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, Game.Pack(new Puzzle("?ay", "ÇŞÖ", 'Ç')), 4, 4);
        await Query("g:0");
        Assert.Equal((5, 5), (_h.Store.Get(1)!.Streak, _h.Store.Get(1)!.Best));
    }

    [Fact]
    public async Task Unknown_request_answers_err()
    {
        await Query("zz");
        Assert.Equal("err", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));
    }

    [Fact]
    public async Task Game_answer_without_puzzle_or_bad_index_answers_err()
    {
        await Query("g:7");
        Assert.Equal("err", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));
    }

    [Fact]
    public async Task Inline_widget_answers_err()
    {
        await Query("r", inline: true);
        Assert.Equal("err", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));
    }

    [Fact]
    public async Task Refresh_shows_today_chars_and_keeps_puzzle()
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, Game.Pack(new Puzzle("?ahar", "ÇŞÖ", 'Ş')), 0, 0);
        _h.Store.AddChars(1, 42);
        // bir xil holat — Telegram "not modified" qaytaradi, baribir javob beramiz
        _h.Fake.Respond = (m, _) => m == "editMessageWidgetData"
            ? "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: message is not modified\"}" : null!;
        await Query("r");
        var shown = ChatData(Assert.Single(_h.Sent("editMessageWidgetData")));
        Assert.Equal(42, shown.GetProperty("t").GetInt32());
        Assert.Equal("?ahar", shown.GetProperty("q").GetString());
        Assert.Equal("ok", Answer());
    }

    [Fact]
    public async Task Start_sends_widget_with_allow_queries()
    {
        await _h.Text("/start", entities: Cmd(6));
        Assert.Contains(Texts.Greeting, Assert.Single(_h.Sent()).GetProperty("text").GetString());
        var w = Assert.Single(_h.Sent("sendWidget"));
        Assert.True(w.GetProperty("allow_queries").GetBoolean());
        Assert.Equal(Texts.CardCaption, w.GetProperty("caption").GetString());
        Assert.Equal(CardSender.Html, w.GetProperty("html").GetString());
        var p = Game.Unpack(_h.Store.Get(1)!.GameAnswer)!;
        Assert.Equal(p.Masked, ChatData(w).GetProperty("q").GetString());
        Assert.Equal(p.Options, ChatData(w).GetProperty("o").GetString());
    }

    [Fact]
    public async Task Sozlama_command_sends_card()
    {
        await _h.Text("/sozlama", entities: Cmd(8));
        Assert.Single(_h.Sent("sendWidget"));
        Assert.Empty(_h.Sent());
    }

    [Fact]
    public async Task Widget_failure_falls_back_to_keyboard()
    {
        _h.Fake.Respond = (m, _) => m == "sendWidget"
            ? "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: method not found\"}" : null!;
        await _h.Text("/sozlama", entities: Cmd(8));
        var msg = Assert.Single(_h.Sent());
        Assert.Equal(Texts.SettingsTitle, msg.GetProperty("text").GetString());
        var data = msg.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray()
            .SelectMany(r => r.EnumerateArray()).Select(b => b.GetProperty("callback_data").GetString()).ToList();
        Assert.Equal(["set:r", "set:c", "set:w", "set:f"], data);

        await _h.Callback("set:c", Texts.SettingsTitle, null);
        Assert.False(_h.Store.Get(1)!.Settings.Cyrillic);
        var edit = Assert.Single(_h.Sent("editMessageText"));
        Assert.Equal(5, edit.GetProperty("message_id").GetInt32());
        var btn = edit.GetProperty("reply_markup").GetProperty("inline_keyboard")[1][0].GetProperty("text").GetString()!;
        Assert.Contains(Texts.SetCyrillic, btn);
        Assert.StartsWith("⬜", btn);
        Assert.Single(_h.Sent("answerCallbackQuery"));

        await _h.Callback("set:r", Texts.SettingsTitle, null);
        Assert.True(_h.Store.Get(1)!.Settings.Reverse);
    }
}
