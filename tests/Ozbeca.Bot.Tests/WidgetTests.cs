using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;
using Ozbeca.Bot.Widget;

namespace Ozbeca.Bot.Tests;

/// <summary>Clock'ni boshqaradi (taymerli o'yin, kunlik statistika) — "Clock" kolleksiyasida.</summary>
[Collection("Clock")]
public sealed class WidgetTests : IDisposable
{
    private readonly BotHarness _h = new();
    private readonly Func<DateTime> _clock = Clock.UtcNow;
    private DateTime _now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    public WidgetTests() => Clock.UtcNow = () => _now;

    public void Dispose()
    {
        Clock.UtcNow = _clock;
        _h.Dispose();
    }

    private const string Targets = "ÖĞŞÇöğşç";

    private Task Query(string data, long uid = 1, bool inline = false) =>
        _h.Handle(new
        {
            update_id = 1,
            widget_query = inline
                ? (object)new { id = "wq", from = new { id = uid, is_bot = false }, inline_message_id = "im1", chat_instance = "ci", type = new { type = "data" }, data }
                : new { id = "wq", from = new { id = uid, is_bot = false }, chat = new { id = uid, type = "private" }, message_id = 7, chat_instance = "ci", type = new { type = "data" }, data },
        });

    private string? Answer() => _h.Sent("answerWidgetQuery")[^1].GetProperty("data").GetString();

    private static JsonElement ChatData(JsonElement body) => JsonDocument.Parse(body.GetProperty("chat_data").GetString()!).RootElement;

    private static object Cmd(int len) => new[] { new { type = "bot_command", offset = 0, length = len } };

    private void SetPuzzle(Puzzle p, int streak = 0, int best = 0)
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, Game.Pack(p), streak, best);
    }

    private static long Ms(DateTime t) => t.Ticks / TimeSpan.TicksPerMillisecond;

    // ---------- HTML va chat_data hajmlari ----------

    [Theory]
    [InlineData('s')]
    [InlineData('h')]
    [InlineData('g')]
    [InlineData('t')]
    public void Html_within_limits(char kind)
    {
        var html = CardSender.Html[kind];
        var bytes = Encoding.UTF8.GetBytes(html);
        Assert.True(bytes.Length <= 32768, $"html {bytes.Length} bayt");
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true)) z.Write(bytes);
        Assert.True(ms.Length <= 4096, $"zlib {ms.Length} bayt");
        Assert.Contains("TelegramWidget", html);
    }

    [Fact]
    public void Chat_data_fits_1024_bytes_for_every_kind()
    {
        var longest = Game.Words.Concat(Game.LongWords).MaxBy(w => Encoding.UTF8.GetByteCount(w))!;
        var u = new UserRow(long.MaxValue, new Settings(true, true, true, true), int.MaxValue, int.MaxValue, int.MaxValue, null, false);
        var all = new[]
        {
            WidgetState.Settings(u.Settings),
            WidgetState.Letters,
            WidgetState.Game(u, new Puzzle("??" + longest[2..], "ÖĞŞÇ", "ÖĞ", 2, long.MaxValue)),
            WidgetState.Stats(Enumerable.Repeat(int.MaxValue, 7).ToArray(), (long.MaxValue, int.MaxValue), int.MaxValue),
        };
        foreach (var json in all)
        {
            Assert.True(Encoding.UTF8.GetByteCount(json) <= 1024, json);
            JsonDocument.Parse(json);
        }
        Assert.Equal("{\"k\":\"h\"}", WidgetState.Letters);
        var s = JsonDocument.Parse(all[0]).RootElement;
        Assert.Equal(("s", 1, 1, 1, 1), (s.GetProperty("k").GetString(), s.GetProperty("d").GetInt32(), s.GetProperty("c").GetInt32(),
            s.GetProperty("w").GetInt32(), s.GetProperty("f").GetInt32()));
    }

    [Fact]
    public void Game_chat_data_has_no_answer()
    {
        var u = new UserRow(1, new Settings(), 0, 3, 7, null, false);
        var g = JsonDocument.Parse(WidgetState.Game(u, new Puzzle("qö?iq?i", "ÇŞĞÖ", "ŞÇ", 2, 5))).RootElement;
        Assert.Equal(["k", "s", "b", "l", "q", "o", "t"], g.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("g", 3, 7, 2, "qö?iq?i", "ÇŞĞÖ", 10),
            (g.GetProperty("k").GetString(), g.GetProperty("s").GetInt32(), g.GetProperty("b").GetInt32(), g.GetProperty("l").GetInt32(),
             g.GetProperty("q").GetString(), g.GetProperty("o").GetString(), g.GetProperty("t").GetInt32()));
        Assert.Equal(0, JsonDocument.Parse(WidgetState.Game(u, new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, 5))).RootElement.GetProperty("t").GetInt32());
    }

    // ---------- so'zlar va topishmoqlar ----------

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
    public void Long_words_have_two_targets_and_7_to_12_letters()
    {
        Assert.True(Game.LongWords.Count >= 120, $"{Game.LongWords.Count} ta so'z");
        Assert.Equal(Game.LongWords.Count, Game.LongWords.Distinct().Count());
        foreach (var w in Game.LongWords)
        {
            Assert.True(w.Count(c => Targets.Contains(c)) >= 2, w);
            Assert.InRange(w.Length, 7, 12);
            Assert.Equal(w.ToLowerInvariant(), w);
            Assert.DoesNotContain('\'', w);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(1000, 2)]
    public void Level_follows_streak(int streak, int level) => Assert.Equal(level, Game.LevelFor(streak));

    [Fact]
    public void Easy_puzzle_has_one_slot_and_three_options()
    {
        var rnd = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            var p = Game.Next(rnd, 0, 123);
            Assert.Equal((0, 0, 123L), (p.Level, p.Timer, p.IssuedMs));
            Assert.Equal(3, p.Options.Length);
            Assert.Equal(3, p.Options.Distinct().Count());
            var a = Assert.Single(p.Answer);
            Assert.Single(p.Options, c => c == a);
            Assert.All(p.Options, c => Assert.Contains(c, "ÖĞŞÇ"));
            Assert.Single(p.Masked, c => c == '?');
            Assert.Contains(p.Masked.Replace("?", char.ToLowerInvariant(a).ToString()), Game.Words);
            Assert.Equal(p, Game.Unpack(Game.Pack(p)));
        }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 10)]
    public void Hard_puzzle_has_two_slots_and_four_options(int level, int timer)
    {
        var rnd = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            var p = Game.Next(rnd, level, 99);
            Assert.Equal((level, timer), (p.Level, p.Timer));
            Assert.Equal("ÇÖĞŞ", string.Concat(p.Options.Order()));
            Assert.Equal(2, p.Answer.Length);
            Assert.Equal(2, p.Masked.Count(c => c == '?'));
            var word = p.Masked;
            foreach (var a in p.Answer) word = ReplaceFirst(word, '?', char.ToLowerInvariant(a));
            Assert.Contains(word, Game.LongWords);
            Assert.Equal(p, Game.Unpack(Game.Pack(p)));
        }
    }

    private static string ReplaceFirst(string s, char from, char to)
    {
        var i = s.IndexOf(from);
        return s[..i] + to + s[(i + 1)..];
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ŞÇŞÖ?ahar")]                 // eski 1-harfli format
    [InlineData("0|Ş|ÇŞ|1|?ahar")]            // variantlar kam
    [InlineData("0|X|ÇŞÖ|1|?ahar")]           // javob variantlarda yo'q
    [InlineData("1|Ş|ÖĞŞÇ|1|?a?ar")]          // kataklar soni ≠ javob
    [InlineData("3|ŞÇ|ÖĞŞÇ|1|?a?ar")]         // daraja
    [InlineData("1|ŞÇ|ÖĞŞÇ|x|?a?ar")]         // vaqt
    [InlineData("1|ŞÇ|ÖĞŞŞ|1|?a?ar")]         // takror variant
    public void Unpack_rejects_invalid(string? s) => Assert.Null(Game.Unpack(s));

    // ---------- so'rovlar ----------

    [Fact]
    public async Task Direction_request_saves_and_edits_card()
    {
        await Query("d:1");
        Assert.True(_h.Store.Get(1)!.Settings.Reverse);
        var edit = Assert.Single(_h.Sent("editMessageWidgetData"));
        Assert.Equal(1, edit.GetProperty("chat_id").GetInt64());
        Assert.Equal(7, edit.GetProperty("message_id").GetInt32());
        Assert.Equal(("s", 1), (ChatData(edit).GetProperty("k").GetString(), ChatData(edit).GetProperty("d").GetInt32()));
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
        SetPuzzle(new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, Ms(_now)), 2, 5);
        await Query("g:1");
        var u = _h.Store.Get(1)!;
        Assert.Equal((3, 5), (u.Streak, u.Best));
        Assert.Equal("ok", Answer());
        var next = Game.Unpack(u.GameAnswer)!;
        var shown = ChatData(_h.Sent("editMessageWidgetData")[^1]);
        Assert.Equal(next.Masked, shown.GetProperty("q").GetString());
        Assert.Equal(next.Options, shown.GetProperty("o").GetString());
        Assert.Equal(3, shown.GetProperty("s").GetInt32());
        Assert.False(shown.TryGetProperty("a", out _));

        var wrong = next.Options.IndexOf(next.Options.First(c => c != next.Answer[0]));
        await Query("g:" + wrong);
        u = _h.Store.Get(1)!;
        Assert.Equal((0, 5), (u.Streak, u.Best));
        Assert.Equal("no:" + next.Answer, Answer());
        Assert.NotEqual(next, Game.Unpack(u.GameAnswer));
    }

    [Fact]
    public async Task Best_follows_streak_and_level_rises_at_five()
    {
        SetPuzzle(new Puzzle("?ay", "ÇŞÖ", "Ç", 0, Ms(_now)), 4, 4);
        await Query("g:0");
        var u = _h.Store.Get(1)!;
        Assert.Equal((5, 5), (u.Streak, u.Best));
        var p = Game.Unpack(u.GameAnswer)!;
        Assert.Equal(1, p.Level);
        Assert.Equal(1, ChatData(_h.Sent("editMessageWidgetData")[^1]).GetProperty("l").GetInt32());
    }

    [Fact]
    public async Task Two_slots_correct_and_wrong_in_slot_order()
    {
        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 1, Ms(_now)), 6, 6);
        await Query("g:23");
        Assert.Equal("ok", Answer());
        Assert.Equal(7, _h.Store.Get(1)!.Streak);

        SetPuzzle(new Puzzle("qö?iq?i", "ÇŞĞÖ", "ŞÇ", 1, Ms(_now)), 7, 7);
        await Query("g:01");   // ikkala katak noto'g'ri
        Assert.Equal("no:ŞÇ", Answer());
        Assert.Equal((0, 7), (_h.Store.Get(1)!.Streak, _h.Store.Get(1)!.Best));

        SetPuzzle(new Puzzle("qö?iq?i", "ÇŞĞÖ", "ŞÇ", 1, Ms(_now)), 3, 7);
        await Query("g:12");   // birinchisi to'g'ri, ikkinchisi yo'q
        Assert.Equal("no:ŞÇ", Answer());
    }

    [Fact]
    public async Task Answer_compared_case_insensitively()
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, $"1|şç|ÖĞŞÇ|{Ms(_now)}|qö?iq?i", 0, 0);
        await Query("g:23");
        Assert.Equal("ok", Answer());
        _h.Store.SetGame(1, $"1|şç|ÖĞŞÇ|{Ms(_now)}|qö?iq?i", 0, 0);
        await Query("g:00");
        Assert.Equal("no:ŞÇ", Answer());
    }

    [Theory]
    [InlineData(0, "g:0x")]
    [InlineData(0, "g:00")]     // 1 katakli: ikki indeks emas
    [InlineData(0, "g:3")]      // 3 variant
    [InlineData(0, "g:t")]      // taymer yo'q
    [InlineData(1, "g:0")]      // 2 katakli: bitta indeks yetmaydi
    [InlineData(1, "g:04")]
    [InlineData(1, "g:012")]
    [InlineData(1, "g:t")]
    [InlineData(1, "g:")]
    public async Task Malformed_game_request_answers_err_without_state_change(int level, string data)
    {
        var p = level == 0 ? new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, Ms(_now)) : new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 1, Ms(_now));
        SetPuzzle(p, 3, 4);
        await Query(data);
        Assert.Equal("err", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));
        var u = _h.Store.Get(1)!;
        Assert.Equal((3, 4, p), (u.Streak, u.Best, Game.Unpack(u.GameAnswer)));
    }

    [Fact]
    public async Task Timed_answer_within_grace_counts()
    {
        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now)), 10, 10);
        _now = _now.AddSeconds(12.9);
        await Query("g:23");
        Assert.Equal("ok", Answer());
        Assert.Equal(11, _h.Store.Get(1)!.Streak);
        var p = Game.Unpack(_h.Store.Get(1)!.GameAnswer)!;
        Assert.Equal((2, Ms(_now)), (p.Level, p.IssuedMs));   // yangi topishmoq — yangi vaqt
        Assert.Equal(10, ChatData(_h.Sent("editMessageWidgetData")[^1]).GetProperty("t").GetInt32());
    }

    [Fact]
    public async Task Timed_answer_after_13_seconds_is_wrong()
    {
        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now)), 12, 12);
        _now = _now.AddSeconds(13.1);
        await Query("g:23");
        Assert.Equal("no:ŞÇ", Answer());
        var u = _h.Store.Get(1)!;
        Assert.Equal((0, 12), (u.Streak, u.Best));
        Assert.Equal(0, Game.Unpack(u.GameAnswer)!.Level);   // seriya 0 — oson daraja
    }

    [Fact]
    public async Task Client_timeout_is_wrong_for_timed_puzzle()
    {
        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now)), 11, 11);
        _now = _now.AddSeconds(10);
        await Query("g:t");
        Assert.Equal("no:ŞÇ", Answer());
        Assert.Equal(0, _h.Store.Get(1)!.Streak);
    }

    [Fact]
    public async Task Replayed_answer_is_not_counted_twice()
    {
        SetPuzzle(new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, Ms(_now)), 4, 4);
        await Query("g:1");
        Assert.Equal("ok", Answer());
        await Query("g:1");   // topishmoq almashgan (2 katakli) — takror hisoblanmaydi
        Assert.Equal("err", Answer());
        Assert.Equal(5, _h.Store.Get(1)!.Streak);

        // xuddi shu darajada ham: javobdan keyin eski topishmoq yo'q
        var p = new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 1, Ms(_now));
        SetPuzzle(p, 5, 5);
        await Query("g:23");
        Assert.NotEqual(p, Game.Unpack(_h.Store.Get(1)!.GameAnswer));
    }

    [Fact]
    public async Task Refresh_game_keeps_puzzle_and_renews_stale_timed_one()
    {
        var easy = new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, Ms(_now));
        SetPuzzle(easy);
        _now = _now.AddHours(1);
        await Query("r:g");
        Assert.Equal("ok", Answer());
        Assert.Equal(easy, Game.Unpack(_h.Store.Get(1)!.GameAnswer));
        Assert.Equal("?ahar", ChatData(Assert.Single(_h.Sent("editMessageWidgetData"))).GetProperty("q").GetString());

        var timed = new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now));
        SetPuzzle(timed, 10, 10);
        await Query("r:g");
        Assert.Equal(timed, Game.Unpack(_h.Store.Get(1)!.GameAnswer));   // yangi — tegilmaydi
        _now = _now.AddMinutes(5);
        await Query("r:g");   // karta keyinroq ochildi: eski taymerli topishmoq yangilanadi
        var fresh = Game.Unpack(_h.Store.Get(1)!.GameAnswer)!;
        Assert.Equal((2, Ms(_now)), (fresh.Level, fresh.IssuedMs));
        Assert.Equal(10, _h.Store.Get(1)!.Streak);
    }

    [Fact]
    public async Task Unchanged_refresh_does_not_edit()
    {
        _h.Fake.Respond = (m, _) => m == "sendWidget" ? "{\"ok\":true,\"result\":{\"message_id\":7}}" : null!;
        await _h.Text("/sozlama", entities: Cmd(8));
        await Query("r:s");
        Assert.Equal("ok", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));

        await Query("d:1");
        Assert.Single(_h.Sent("editMessageWidgetData"));
        await Query("d:1");
        await Query("r:s");
        Assert.Single(_h.Sent("editMessageWidgetData"));
        Assert.Equal(4, _h.Sent("answerWidgetQuery").Count);
    }

    [Fact]
    public async Task Refresh_of_unknown_message_edits_and_tolerates_not_modified()
    {
        _h.Fake.Respond = (m, _) => m == "editMessageWidgetData"
            ? "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: message is not modified\"}" : null!;
        await Query("r:s");
        Assert.Single(_h.Sent("editMessageWidgetData"));
        Assert.Equal("ok", Answer());
        await Query("r:s");   // endi xotirada bor — edit yo'q
        Assert.Single(_h.Sent("editMessageWidgetData"));
    }

    [Fact]
    public async Task Stats_refresh_shows_own_week_and_totals()
    {
        _h.Store.Touch(1);
        _h.Store.Touch(2);
        _h.Store.AddChars(1, 100);
        _now = _now.AddDays(1);
        _h.Store.AddChars(1, 42);
        _h.Store.AddFile(1);
        _h.Store.AddChars(2, 999);   // boshqa user ko'rinmaydi
        _h.Store.SetGame(1, null, 0, 9);
        await Query("r:t");
        Assert.Equal("ok", Answer());
        var d = ChatData(Assert.Single(_h.Sent("editMessageWidgetData")));
        Assert.Equal("t", d.GetProperty("k").GetString());
        Assert.Equal(42, d.GetProperty("t").GetInt32());
        Assert.Equal([0, 0, 0, 0, 0, 100, 42], d.GetProperty("w").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal((142L, 1, 9), (d.GetProperty("a").GetInt64(), d.GetProperty("f").GetInt32(), d.GetProperty("b").GetInt32()));
        Assert.Equal(42, _h.Store.Get(1)!.CharsToday);
    }

    [Theory]
    [InlineData("zz")]
    [InlineData("r")]       // eski format
    [InlineData("r:x")]
    [InlineData("d:2")]
    [InlineData("t:x")]
    [InlineData("g:7")]
    public async Task Unknown_request_answers_err(string data)
    {
        await Query(data);
        Assert.Equal("err", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));
    }

    [Fact]
    public async Task Inline_widget_answers_err()
    {
        await Query("r:s", inline: true);
        Assert.Equal("err", Answer());
        Assert.Empty(_h.Sent("editMessageWidgetData"));
    }

    // ---------- menyu, buyruqlar, zaxira ----------

    [Theory]
    [InlineData("/start", 6)]
    [InlineData("/menu", 5)]
    public async Task Start_shows_menu_without_widget(string cmd, int len)
    {
        await _h.Text(cmd, entities: Cmd(len));
        var msg = Assert.Single(_h.Sent());
        Assert.Contains(Texts.Greeting, msg.GetProperty("text").GetString());
        var rows = msg.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray()
            .Select(r => r.EnumerateArray().Select(b => b.GetProperty("callback_data").GetString()).ToList()).ToList();
        Assert.Equal([["m:s", "m:h"], ["m:g", "m:t"], ["m:f", "m:y"]], rows);
        var texts = msg.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray()
            .SelectMany(r => r.EnumerateArray()).Select(b => b.GetProperty("text").GetString()!).ToList();
        Assert.Equal([Texts.MenuSettings, Texts.MenuLetters, Texts.MenuGame, Texts.MenuStats, Texts.MenuFile, Texts.MenuHelp],
            texts.Select(t => t[(t.IndexOf(' ') + 1)..]));
        Assert.Empty(_h.Sent("sendWidget"));
    }

    [Theory]
    [InlineData("m:s", 's')]
    [InlineData("m:h", 'h')]
    [InlineData("m:g", 'g')]
    [InlineData("m:t", 't')]
    public async Task Menu_button_sends_its_widget(string data, char kind)
    {
        await _h.Callback(data, "menyu", null);
        var w = Assert.Single(_h.Sent("sendWidget"));
        Assert.Equal(CardSender.Html[kind], w.GetProperty("html").GetString());
        Assert.True(w.GetProperty("allow_queries").GetBoolean());
        Assert.Equal(1, w.GetProperty("chat_id").GetInt64());
        Assert.EndsWith(Texts.CardHint, w.GetProperty("caption").GetString());
        Assert.Equal(kind.ToString(), ChatData(w).GetProperty("k").GetString());
        Assert.Single(_h.Sent("answerCallbackQuery"));
        Assert.Empty(_h.Sent());
    }

    [Fact]
    public async Task Game_widget_stores_new_puzzle_for_level()
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, null, 10, 10);
        await _h.Text("/oyin", entities: Cmd(5));
        var w = Assert.Single(_h.Sent("sendWidget"));
        var p = Game.Unpack(_h.Store.Get(1)!.GameAnswer)!;
        Assert.Equal((2, Ms(_now)), (p.Level, p.IssuedMs));
        var d = ChatData(w);
        Assert.Equal((p.Masked, p.Options, 2, 10), (d.GetProperty("q").GetString(), d.GetProperty("o").GetString(),
            d.GetProperty("l").GetInt32(), d.GetProperty("t").GetInt32()));
    }

    [Theory]
    [InlineData("/sozlama", 's')]
    [InlineData("/oyin", 'g')]
    [InlineData("/harflar", 'h')]
    [InlineData("/statistika", 't')]
    public async Task Commands_send_widgets(string cmd, char kind)
    {
        await _h.Text(cmd, entities: Cmd(cmd.Length));
        Assert.Equal(CardSender.Html[kind], Assert.Single(_h.Sent("sendWidget")).GetProperty("html").GetString());
        Assert.Empty(_h.Sent());
    }

    [Fact]
    public async Task File_and_help_buttons_send_texts()
    {
        await _h.Callback("m:f", "menyu", null);
        await _h.Callback("m:y", "menyu", null);
        await _h.Callback("m:x", "menyu", null);
        Assert.Equal([Texts.FileHelp, Texts.Help], _h.Sent().Select(m => m.GetProperty("text").GetString()));
        Assert.Equal(3, _h.Sent("answerCallbackQuery").Count);
        Assert.Empty(_h.Sent("sendWidget"));
    }

    [Theory]
    [InlineData("/oyin", 5)]
    [InlineData("/harflar", 8)]
    [InlineData("/statistika", 11)]
    public async Task Widget_failure_falls_back_to_caption(string cmd, int len)
    {
        _h.Fake.Respond = (m, _) => m == "sendWidget"
            ? "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: method not found\"}" : null!;
        await _h.Text(cmd, entities: Cmd(len));
        var w = Assert.Single(_h.Sent("sendWidget"));
        var msg = Assert.Single(_h.Sent());
        Assert.Equal(w.GetProperty("caption").GetString(), msg.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Settings_widget_failure_falls_back_to_keyboard()
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

    [Fact]
    public async Task Captions_show_current_state()
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, null, 3, 8);
        _h.Store.AddChars(1, 77);
        await _h.Text("/oyin", entities: Cmd(5));
        await _h.Text("/statistika", entities: Cmd(11));
        await _h.Text("/sozlama", entities: Cmd(8));
        var caps = _h.Sent("sendWidget").Select(w => w.GetProperty("caption").GetString()!).ToList();
        Assert.StartsWith(string.Format(Texts.CapGame, 3, 8), caps[0]);
        Assert.StartsWith(string.Format(Texts.CapStats, 77), caps[1]);
        Assert.StartsWith(string.Format(Texts.CapSettings, Texts.SetOldToNew), caps[2]);
    }
}
