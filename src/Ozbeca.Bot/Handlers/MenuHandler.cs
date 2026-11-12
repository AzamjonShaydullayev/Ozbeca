using System.Text.Json;
using Ozbeca.Bot.Api;
using System.Globalization;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Text;
using Ozbeca.Bot.Ui;
using Ozbeca.Bot.Widget;

namespace Ozbeca.Bot.Handlers;

/// <summary>
/// /start menyusi va bo'limlar — hammasi bitta xabar ichida: tugma bosilsa o'sha xabar tahrirlanadi, har bo'limda "Orqaga".
/// Callback'lar: m:0|s|h|g|t|f|y (ekran), set:r|c|w|f (sozlama), g:i / g:ij (o'yin; 2 katakda g:i — birinchi tanlov).
/// </summary>
internal sealed class MenuHandler(Sender sender, Store store, PremiumEmoji emoji)
{
    private const string Screens = "0shgtfy";
    private const string LettersTable = "Sh → Ş      Ch → Ç\nOʻ → Ö      Gʻ → Ğ\n'   → ʼ   (tutuq)";
    private const string Bars = "▁▂▃▄▅▆▇█";

    /// <summary>Yangi xabar (buyruqlar). screen: Screens'dan biri.</summary>
    public Task Send(char screen, long chatId, UserRow u, string? name, CancellationToken ct)
    {
        var (t, e, kb) = Render(screen, u, name, null, null);
        return sender.Send("sendMessage", Payload(chatId, t, e, kb), ct);
    }

    /// <summary>m:/set:/g: tugmalari. answerCallbackQuery'ni o'zi beradi (o'yinda — natija bilan).</summary>
    public async Task OnCallback(JsonElement cq, UserRow u, CancellationToken ct)
    {
        var data = cq.Str("data") ?? "";
        string? note = null, partial = null;
        char screen;
        if (data is ['m', ':', var c] && Screens.Contains(c)) screen = c;
        else if (data.StartsWith("set:", StringComparison.Ordinal) && Toggle(u.Settings, data) is { } next)
        {
            store.SaveSettings(u.Id, next);
            u = u with { Settings = next };
            screen = 's';
        }
        else if (data.StartsWith("g:", StringComparison.Ordinal))
        {
            (u, note, partial) = Answer(u, data);
            screen = 'g';
        }
        else { await AnswerCallback(cq, null, ct); return; }

        await AnswerCallback(cq, note, ct);
        var (t, e, kb) = Render(screen, u, cq.Obj("from")?.Str("first_name"), note, partial);
        var msg = cq.Obj("message");
        if (msg?.Long("date") is > 0 && msg.Value.Obj("chat")?.Long("id") is { } chatId && msg.Value.Long("message_id") is { } mid)
        {
            var p = Payload(chatId, t, e, kb);
            p["message_id"] = mid;
            try { await sender.Send("editMessageText", p, ct); return; }
            catch (BotApiException ex) when (ex.Description.Contains("not modified", StringComparison.OrdinalIgnoreCase)) { return; }
            catch (BotApiException) { }   // tahrirlab bo'lmadi (eski/o'chirilgan) — yangi xabar
        }
        await sender.Send("sendMessage", Payload(u.Id, t, e, kb), ct);
    }

    private static Settings? Toggle(Settings s, string data) => data switch
    {
        "set:r" => s with { Reverse = !s.Reverse },
        "set:c" => s with { Cyrillic = !s.Cyrillic },
        "set:w" => s with { Dictionary = !s.Dictionary },
        "set:f" => s with { LongAsFile = !s.LongAsFile },
        _ => null,
    };

    /// <summary>O'yin javobi. note — natija (toast va xabar boshida), partial — 2 katakning birinchi tanlovi.</summary>
    private (UserRow U, string? Note, string? Partial) Answer(UserRow u, string data)
    {
        if (Game.Unpack(u.GameAnswer) is not { } cur) return (u, null, null);
        var picks = data[2..];
        if (cur.Answer.Length == 2 && picks.Length == 1 && picks[0] >= '0' && picks[0] - '0' < cur.Options.Length)
            return (u, null, picks);
        var now = Clock.Ms();
        if (Game.Check(cur, data, now) is not { } right) return (u, null, null);   // eskirgan tugma — joriy topishmoq ko'rsatiladi

        var streak = right ? u.Streak + 1 : 0;
        u = u with { Streak = streak, Best = Math.Max(u.Best, streak) };
        NewPuzzle(u);   // har javobdan keyin yangi topishmoq — takror hisoblanmaydi
        u = store.Get(u.Id) ?? u;
        var note = right ? "✅ " + Texts.GameRight
            : Game.Expired(cur, now) ? "⏰ " + Texts.GameTimeout + ". " + Texts.GameWrong + ": " + cur.Answer
            : "❌ " + Texts.GameWrong + ": " + cur.Answer;
        return (u, note, null);
    }

    private void NewPuzzle(UserRow u) =>
        store.SetGame(u.Id, Game.Pack(Game.Next(Random.Shared, Game.LevelFor(u.Streak), Clock.Ms())), u.Streak, u.Best);

    /// <summary>Saqlangan topishmoq; yo'q bo'lsa — yangisi; taymerli va vaqti o'tgan bo'lsa — vaqt tugadi (R21): seriya 0.</summary>
    private (UserRow U, Puzzle P) CurrentPuzzle(UserRow u)
    {
        var p = Game.Unpack(u.GameAnswer);
        if (p != null && !Game.Expired(p, Clock.Ms())) return (u, p);
        if (p != null) u = u with { Streak = 0 };
        NewPuzzle(u);
        u = store.Get(u.Id) ?? u;
        return (u, Game.Unpack(u.GameAnswer)!);
    }

    private (string Text, List<Entity> Entities, object Markup) Render(char screen, UserRow u, string? name, string? note, string? partial)
    {
        var b = new MessageBuilder(emoji, u.Id);
        object kb;
        switch (screen)
        {
            case 's':
                Title(b, "⚙️", Texts.SettingsTitle);
                var s = u.Settings;
                kb = Buttons.Markup(
                    [Buttons.Callback("🔁 " + Texts.SetDirection + ": " + (s.Reverse ? Texts.SetNewToOld : Texts.SetOldToNew), "set:r", "primary")],
                    [Mark(s.Cyrillic, Texts.SetCyrillic, "set:c")],
                    [Mark(s.Dictionary, Texts.SetWords, "set:w")],
                    [Mark(s.LongAsFile, Texts.SetFile, "set:f")],
                    [Back(u)]);
                break;
            case 'h':
                Title(b, "🆕", Texts.CmdLetters).NewLine().Bold(LettersTable).NewLine().NewLine().Emoji("👇").Text(" " + Texts.LettersHint);
                kb = Buttons.Markup(
                    "ŞÇÖĞʼ".Select(ch => Buttons.Copy(ch.ToString(), ch.ToString(), "primary")).ToArray(),
                    "şçöğ".Select(ch => Buttons.Copy(ch.ToString(), ch.ToString())).ToArray(),
                    [Back(u)]);
                break;
            case 'g':
                Puzzle p;
                (u, p) = CurrentPuzzle(u);
                Title(b, "🎮", Texts.GameTitle);
                if (note != null) b.Text(note).NewLine();
                b.Emoji("🔥").Text($" {Texts.GameStreak}: {u.Streak}   ").Emoji("🏆").Text($" {Texts.GameBest}: {u.Best}").NewLine().NewLine();
                var word = p.Masked.ToUpperInvariant();
                if (partial != null) word = ReplaceFirst(word, '?', p.Options[partial[0] - '0']);
                b.Bold(string.Join(" ", word.ToCharArray())).NewLine().NewLine()
                 .Text(p.Answer.Length == 2 ? Texts.GameAsk2 : Texts.GameAsk);
                if (p.Timer > 0) b.NewLine().Emoji("⏰").Text(" " + Texts.GameTimer);
                kb = Buttons.Markup(
                    p.Options.Select((o, i) => Buttons.Callback(o.ToString(), "g:" + partial + i, "primary")).ToArray(),
                    [Back(u)]);
                break;
            case 't':
                var week = store.UserWeek(u.Id);
                var (chars, files) = store.UserTotals(u.Id);
                var max = Math.Max(1, week.Max());
                Title(b, "📊", Texts.CmdStats).NewLine()
                    .Text($"{Texts.StatToday}: ").Bold($"{Num(week[6])} {Texts.StatChars}").NewLine()
                    .Text($"{Texts.StatWeek}: ").Bold($"{Num(week.Sum())} {Texts.StatChars}").NewLine()
                    .Text(string.Concat(week.Select(d => d == 0 ? '·' : Bars[(int)((long)d * (Bars.Length - 1) / max)]))).NewLine().NewLine()
                    .Text($"{Texts.StatUsers}: {Num(chars)} {Texts.StatChars} · {Texts.StatFile}: {files}").NewLine()
                    .Emoji("🏆").Text($" {Texts.MenuGame}: {Texts.GameBest.ToLowerInvariant()} {u.Best}");
                kb = Buttons.Markup([Back(u)]);
                break;
            case 'f':
                Title(b, "📎", Texts.MenuFile).NewLine().Text(Texts.FileHelp);
                kb = Buttons.Markup([Back(u)]);
                break;
            case 'y':
                Title(b, "❓", Texts.MenuHelp).NewLine().Text(Texts.Help);
                kb = Buttons.Markup([Back(u)]);
                break;
            default:   // '0' — salomlashuv va menyu
                b.Emoji("greeting").Text(" " + Texts.GreetHello);
                if (!string.IsNullOrWhiteSpace(name)) b.Text(", ").Bold(name.Length > 32 ? name[..32] : name);
                b.Text("!").NewLine().Text(Texts.GreetIntro).NewLine().NewLine()
                 .Emoji("✍️").Text(" " + Texts.GreetText).NewLine()
                 .Emoji("📎").Text(" " + Texts.GreetFile).NewLine()
                 .Emoji("🔁").Text(" " + Texts.GreetReverse).NewLine()
                 .Emoji("💬").Text(" " + Texts.GreetInline).NewLine().NewLine()
                 .Emoji("👇").Text(" ").Bold(Texts.GreetGo);
                kb = Buttons.Markup(
                    [Btn(u, "⚙️", Texts.MenuSettings, "m:s", "primary"), Btn(u, "🆕", Texts.MenuLetters, "m:h", "primary")],
                    [Btn(u, "🎮", Texts.MenuGame, "m:g", "success"), Btn(u, "📊", Texts.MenuStats, "m:t", "success")],
                    [Btn(u, "📎", Texts.MenuFile, "m:f", "primary"), Btn(u, "❓", Texts.MenuHelp, "m:y", "primary")]);
                break;
        }
        var (t, e) = b.Build();
        return (t, e, kb);
    }

    private static MessageBuilder Title(MessageBuilder b, string glyph, string title) => b.Emoji(glyph).Text(" ").Bold(title).NewLine();

    private static object Mark(bool on, string text, string data) =>
        Buttons.Callback((on ? "✅ " : "⬜ ") + text, data, on ? "success" : null);

    private object Back(UserRow u) => Btn(u, "⬅️", Texts.BackButton, "m:0", null);

    /// <summary>Premium ikonka bor bo'lsa — icon_custom_emoji_id, yo'q bo'lsa emoji matn boshida.</summary>
    private object Btn(UserRow u, string glyph, string text, string data, string? style)
    {
        var id = emoji.Id(glyph, u.Id, Clock.Today());
        return Buttons.Callback(id == null ? glyph + " " + text : text, data, style, id);
    }

    private static string Num(long n) => n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');

    private static string ReplaceFirst(string s, char from, char to)
    {
        var i = s.IndexOf(from);
        return i < 0 ? s : s[..i] + to + s[(i + 1)..];
    }

    private static Dictionary<string, object?> Payload(long chatId, string text, List<Entity> ents, object markup)
    {
        var p = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId, ["text"] = text, ["reply_markup"] = markup,
            ["link_preview_options"] = new { is_disabled = true },
        };
        if (ents.Count > 0) p["entities"] = ents.Select(x => x.ToJson()).ToList();
        return p;
    }

    private async Task AnswerCallback(JsonElement cq, string? text, CancellationToken ct)
    {
        if (cq.Str("id") is not { } id) return;
        var p = new Dictionary<string, object?> { ["callback_query_id"] = id };
        if (text != null) p["text"] = text;
        try { await sender.Send("answerCallbackQuery", p, ct); }
        catch (BotApiException) { }   // eskirgan callback
    }
}
