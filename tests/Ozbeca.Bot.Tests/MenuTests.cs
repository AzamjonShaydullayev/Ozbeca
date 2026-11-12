using System.Text.Json;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;
using Ozbeca.Bot.Widget;

namespace Ozbeca.Bot.Tests;

/// <summary>/start menyusi, bo'limlar (bitta xabar ichida tahrirlanadi) va "Harfni top" o'yini. Clock'ni boshqaradi.</summary>
[Collection("Clock")]
public sealed class MenuTests : IDisposable
{
    private readonly BotHarness _h = new();
    private readonly Func<DateTime> _clock = Clock.UtcNow;
    private DateTime _now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    public MenuTests() => Clock.UtcNow = () => _now;

    public void Dispose()
    {
        Clock.UtcNow = _clock;
        _h.Dispose();
    }

    private const string Targets = "ÖĞŞÇöğşç";

    private static object Cmd(int len) => new[] { new { type = "bot_command", offset = 0, length = len } };

    private Task Tap(string data) => _h.Callback(data, "menyu", null);

    private JsonElement LastEdit() => _h.Sent("editMessageText")[^1];

    private string? Toast() => _h.Sent("answerCallbackQuery")[^1].TryGetProperty("text", out var t) ? t.GetString() : null;

    private static List<List<JsonElement>> Rows(JsonElement msg) =>
        msg.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray().Select(r => r.EnumerateArray().ToList()).ToList();

    private static List<string?> Data(JsonElement msg) =>
        Rows(msg).SelectMany(r => r).Select(b => b.TryGetProperty("callback_data", out var d) ? d.GetString() : null).ToList();

    private void SetPuzzle(Puzzle p, int streak = 0, int best = 0)
    {
        _h.Store.Touch(1);
        _h.Store.SetGame(1, Game.Pack(p), streak, best);
    }

    private static long Ms(DateTime t) => t.Ticks / TimeSpan.TicksPerMillisecond;

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

    // ---------- menyu ----------

    [Theory]
    [InlineData("/start", 6)]
    [InlineData("/menu", 5)]
    public async Task Start_shows_greeting_and_colored_menu(string cmd, int len)
    {
        await _h.Text(cmd, entities: Cmd(len));
        var msg = Assert.Single(_h.Sent());
        var text = msg.GetProperty("text").GetString()!;
        Assert.Contains(Texts.GreetHello, text);
        Assert.Contains(Texts.GreetGo, text);
        Assert.Equal([["m:s", "m:h"], ["m:g", "m:t"], ["m:f", "m:y"]],
            Rows(msg).Select(r => r.Select(b => b.GetProperty("callback_data").GetString()).ToList()));
        Assert.All(Rows(msg).SelectMany(r => r), b => Assert.True(b.TryGetProperty("style", out _)));
    }

    [Theory]
    [InlineData("m:s")]
    [InlineData("m:h")]
    [InlineData("m:g")]
    [InlineData("m:t")]
    [InlineData("m:f")]
    [InlineData("m:y")]
    public async Task Section_edits_same_message_with_back_button(string data)
    {
        await Tap(data);
        var edit = Assert.Single(_h.Sent("editMessageText"));
        Assert.Equal(5, edit.GetProperty("message_id").GetInt32());
        Assert.Equal("m:0", Data(edit)[^1]);
        Assert.Empty(_h.Sent());
        Assert.Single(_h.Sent("answerCallbackQuery"));

        await Tap("m:0");
        Assert.Contains(Texts.GreetHello, LastEdit().GetProperty("text").GetString());
        Assert.Empty(_h.Sent());
    }

    [Fact]
    public async Task Failed_edit_sends_new_message_not_modified_is_ignored()
    {
        _h.Fake.Respond = (m, _) => m == "editMessageText"
            ? "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: message to edit not found\"}" : null!;
        await Tap("m:t");
        Assert.Single(_h.Sent());

        _h.Fake.Respond = (m, _) => m == "editMessageText"
            ? "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: message is not modified\"}" : null!;
        await Tap("m:t");
        Assert.Single(_h.Sent());
    }

    [Fact]
    public async Task Unknown_callback_only_answers()
    {
        await Tap("m:x");
        await Tap("set:x");
        Assert.Empty(_h.Sent("editMessageText"));
        Assert.Equal(2, _h.Sent("answerCallbackQuery").Count);
    }

    [Theory]
    [InlineData("/sozlama", "set:r")]
    [InlineData("/oyin", "m:0")]
    [InlineData("/harflar", "m:0")]
    [InlineData("/statistika", "m:0")]
    public async Task Commands_send_section_message(string cmd, string expected)
    {
        await _h.Text(cmd, entities: Cmd(cmd.Length));
        Assert.Contains(expected, Data(Assert.Single(_h.Sent())));
    }

    [Fact]
    public async Task Settings_toggle_saves_and_redraws()
    {
        await Tap("set:c");
        Assert.False(_h.Store.Get(1)!.Settings.Cyrillic);
        var btn = Rows(LastEdit())[1][0].GetProperty("text").GetString()!;
        Assert.StartsWith("⬜", btn);
        Assert.Contains(Texts.SetCyrillic, btn);
        await Tap("set:r");
        await Tap("set:w");
        await Tap("set:f");
        var s = _h.Store.Get(1)!.Settings;
        Assert.Equal((true, false, true), (s.Reverse, s.Dictionary, s.LongAsFile));
        Assert.Contains(Texts.SetNewToOld, Rows(LastEdit())[0][0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Letters_have_copy_buttons()
    {
        await Tap("m:h");
        var copies = Rows(LastEdit()).SelectMany(r => r).Where(b => b.TryGetProperty("copy_text", out _))
            .Select(b => b.GetProperty("copy_text").GetProperty("text").GetString()).ToList();
        Assert.Equal(["Ş", "Ç", "Ö", "Ğ", "ʼ", "ş", "ç", "ö", "ğ"], copies);
    }

    [Fact]
    public async Task Stats_show_only_own_numbers()
    {
        _h.Store.Touch(1);
        _h.Store.Touch(2);
        _h.Store.AddChars(1, 100);
        _now = _now.AddDays(1);
        _h.Store.AddChars(1, 1042);
        _h.Store.AddFile(1);
        _h.Store.AddChars(2, 999);
        _h.Store.SetGame(1, null, 0, 9);
        await Tap("m:t");
        var text = LastEdit().GetProperty("text").GetString()!;
        Assert.Contains("1 042", text);
        Assert.Contains("1 142", text);
        Assert.DoesNotContain("999", text);
        Assert.Contains(" 9", text);
    }

    // ---------- o'yin ----------

    [Fact]
    public async Task Correct_answer_increments_streak_wrong_resets()
    {
        SetPuzzle(new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, Ms(_now)), 2, 5);
        await Tap("g:1");
        var u = _h.Store.Get(1)!;
        Assert.Equal((3, 5), (u.Streak, u.Best));
        Assert.StartsWith("✅", Toast());
        var next = Game.Unpack(u.GameAnswer)!;
        Assert.Contains(string.Join(" ", next.Masked.ToUpperInvariant().ToCharArray()), LastEdit().GetProperty("text").GetString());

        var wrong = next.Options.IndexOf(next.Options.First(c => c != next.Answer[0]));
        await Tap("g:" + wrong + (next.Answer.Length == 2 ? "0" : ""));
        u = _h.Store.Get(1)!;
        Assert.Equal((0, 5), (u.Streak, u.Best));
        Assert.EndsWith(next.Answer, Toast());
    }

    [Fact]
    public async Task Two_slots_first_pick_is_shown_then_scored()
    {
        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 1, Ms(_now)), 6, 6);
        await Tap("g:2");
        Assert.Equal(6, _h.Store.Get(1)!.Streak);
        Assert.Equal(["g:20", "g:21", "g:22", "g:23", "m:0"], Data(LastEdit()));
        Assert.Contains("Q Ö Ş I Q ? I", LastEdit().GetProperty("text").GetString());
        await Tap("g:23");
        Assert.Equal(7, _h.Store.Get(1)!.Streak);

        SetPuzzle(new Puzzle("qö?iq?i", "ÇŞĞÖ", "ŞÇ", 1, Ms(_now)), 3, 7);
        await Tap("g:12");   // birinchisi to'g'ri, ikkinchisi yo'q
        Assert.Equal((0, 7), (_h.Store.Get(1)!.Streak, _h.Store.Get(1)!.Best));
    }

    [Theory]
    [InlineData(0, "g:0x")]
    [InlineData(0, "g:00")]
    [InlineData(0, "g:3")]
    [InlineData(1, "g:4")]
    [InlineData(1, "g:04")]
    [InlineData(1, "g:012")]
    [InlineData(1, "g:")]
    public async Task Stale_or_malformed_answer_changes_nothing(int level, string data)
    {
        var p = level == 0 ? new Puzzle("?ahar", "ÇŞÖ", "Ş", 0, Ms(_now)) : new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 1, Ms(_now));
        SetPuzzle(p, 3, 4);
        await Tap(data);
        var u = _h.Store.Get(1)!;
        Assert.Equal((3, 4, p), (u.Streak, u.Best, Game.Unpack(u.GameAnswer)));
        Assert.Null(Toast());
    }

    [Fact]
    public async Task Timed_answer_within_grace_counts_after_13_seconds_is_timeout()
    {
        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now)), 10, 10);
        _now = _now.AddSeconds(12.9);
        await Tap("g:23");
        Assert.Equal(11, _h.Store.Get(1)!.Streak);
        Assert.Contains(Texts.GameTimer, LastEdit().GetProperty("text").GetString());

        SetPuzzle(new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now)), 12, 12);
        _now = _now.AddSeconds(13.1);
        await Tap("g:23");
        var u = _h.Store.Get(1)!;
        Assert.Equal((0, 12), (u.Streak, u.Best));
        Assert.Contains(Texts.GameTimeout, Toast());
        Assert.Equal(0, Game.Unpack(u.GameAnswer)!.Level);
    }

    [Fact]
    public async Task Reopening_game_keeps_puzzle_expired_timed_one_is_timeout()
    {
        var timed = new Puzzle("qö?iq?i", "ÖĞŞÇ", "ŞÇ", 2, Ms(_now));
        SetPuzzle(timed, 12, 12);
        _now = _now.AddSeconds(5);
        await _h.Text("/oyin", entities: Cmd(5));
        await Tap("m:g");
        Assert.Equal(timed, Game.Unpack(_h.Store.Get(1)!.GameAnswer));
        Assert.Equal(12, _h.Store.Get(1)!.Streak);

        _now = _now.AddSeconds(9);
        await Tap("m:g");
        var u = _h.Store.Get(1)!;
        Assert.Equal((0, 12), (u.Streak, u.Best));
        Assert.Equal(0, Game.Unpack(u.GameAnswer)!.Level);
    }

    [Fact]
    public async Task Level_rises_at_five()
    {
        SetPuzzle(new Puzzle("?ay", "ÇŞÖ", "Ç", 0, Ms(_now)), 4, 4);
        await Tap("g:0");
        var u = _h.Store.Get(1)!;
        Assert.Equal((5, 5), (u.Streak, u.Best));
        Assert.Equal(1, Game.Unpack(u.GameAnswer)!.Level);
        Assert.Contains(Texts.GameAsk2, LastEdit().GetProperty("text").GetString());
    }
}
