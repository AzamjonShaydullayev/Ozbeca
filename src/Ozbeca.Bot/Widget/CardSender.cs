using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Widget;

/// <summary>
/// Bo'lim widget'lari (s — sozlama, h — harflar, g — o'yin, t — statistika): sendWidget + qisqa holat caption'i.
/// Widget rad etilsa — zaxira: sozlama uchun set: tugmali xabar, boshqalari uchun caption matni.
/// Oxirgi chat_data xotirada ((chat, message) → JSON), o'zgarmagan holat qayta edit qilinmaydi.
/// </summary>
public sealed class CardSender(Sender sender, Store store)
{
    internal static readonly IReadOnlyDictionary<char, string> Html = new Dictionary<char, string>
    {
        ['s'] = Load("sozlama.html"), ['h'] = Load("harflar.html"), ['g'] = Load("oyin.html"), ['t'] = Load("stat.html"),
    };

    private static string Load(string name)
    {
        using var s = typeof(CardSender).Assembly.GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException(name + " resursi yo'q");
        return new StreamReader(s).ReadToEnd();
    }

    private const int CacheMax = 10_000;
    private readonly Dictionary<(long, long), string> _last = new();
    private readonly Queue<(long, long)> _order = new();

    internal bool Unchanged(long chatId, long messageId, string data)
    {
        lock (_last) return _last.TryGetValue((chatId, messageId), out var d) && d == data;
    }

    internal void Remember(long chatId, long messageId, string data)
    {
        lock (_last)
        {
            var key = (chatId, messageId);
            if (!_last.TryAdd(key, data)) { _last[key] = data; return; }
            _order.Enqueue(key);
            if (_order.Count > CacheMax) _last.Remove(_order.Dequeue());   // eng eskisi
        }
    }

    /// <summary>
    /// Yangi topishmoq (daraja seriyadan) Store'ga yoziladi. u — Touch qilingan, Streak/Best — saqlanadigan qiymatlar.
    /// delayMs — klient taymeri shuncha keyin boshlanadi (javobdan keyingi natija ko'rsatilishi).
    /// </summary>
    internal Puzzle NewPuzzle(UserRow u, long delayMs = 0)
    {
        var p = Game.Next(Random.Shared, Game.LevelFor(u.Streak), Clock.Ms() + delayMs);
        store.SetGame(u.Id, Game.Pack(p), u.Streak, u.Best);
        return p;
    }

    /// <summary>
    /// Saqlangan topishmoq (yangi topishmoq bilan o'tkazib yuborib bo'lmaydi). Yo'q bo'lsa — yangisi; taymerli va vaqti
    /// o'tgan bo'lsa — vaqt tugadi (R21): seriya 0 (rekord qoladi), yangi topishmoq.
    /// </summary>
    internal (UserRow U, Puzzle P) CurrentPuzzle(UserRow u)
    {
        var p = Game.Unpack(u.GameAnswer);
        if (p != null && !Game.Expired(p, Clock.Ms())) return (u, p);
        if (p != null) u = u with { Streak = 0 };
        return (u, NewPuzzle(u));
    }

    internal string StatsState(UserRow u) => WidgetState.Stats(store.UserWeek(u.Id), store.UserTotals(u.Id), u.Best);

    /// <summary>kind: s|h|g|t. u — Touch qilingan user.</summary>
    public async Task Send(char kind, long chatId, UserRow u, CancellationToken ct)
    {
        Puzzle? puzzle = null;
        if (kind == 'g') (u, puzzle) = CurrentPuzzle(u);
        var (data, caption) = kind switch
        {
            's' => (WidgetState.Settings(u.Settings),
                    string.Format(Texts.CapSettings, u.Settings.Reverse ? Texts.SetNewToOld : Texts.SetOldToNew)),
            'h' => (WidgetState.Letters, Texts.CapLetters),
            'g' => (WidgetState.Game(u, puzzle!), string.Format(Texts.CapGame, u.Streak, u.Best)),
            't' => (StatsState(u), string.Format(Texts.CapStats, u.CharsToday)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        caption += "\n" + Texts.CardHint;
        try
        {
            var r = await sender.Send("sendWidget", new()
            {
                ["chat_id"] = chatId, ["html"] = Html[kind], ["chat_data"] = data, ["caption"] = caption, ["allow_queries"] = true,
            }, ct);
            if (r.Long("message_id") is { } mid) Remember(chatId, mid, data);
        }
        catch (BotApiException e)
        {
            Log.Info($"sendWidget rad etildi ({e.Code}) — zaxira");
            await sender.Send("sendMessage", kind == 's'
                ? new() { ["chat_id"] = chatId, ["text"] = Texts.SettingsTitle, ["reply_markup"] = Keyboard(u.Settings) }
                : new() { ["chat_id"] = chatId, ["text"] = caption }, ct);
        }
    }

    /// <summary>Zaxira tugmalari (set:r|c|w|f): sozlamani almashtiradi, xabarni yangilaydi, callback'ga o'zi javob beradi.</summary>
    public async Task OnCallback(JsonElement cq, UserRow u, CancellationToken ct)
    {
        var s = u.Settings;
        Settings? next = cq.Str("data") switch
        {
            "set:r" => s with { Reverse = !s.Reverse },
            "set:c" => s with { Cyrillic = !s.Cyrillic },
            "set:w" => s with { Dictionary = !s.Dictionary },
            "set:f" => s with { LongAsFile = !s.LongAsFile },
            _ => null,
        };
        var msg = cq.Obj("message");
        if (next != null && msg?.Long("date") is > 0) store.SaveSettings(u.Id, next);
        else next = null;   // noma'lum yoki eskirgan (inaccessible) xabar — faqat javob

        if (cq.Str("id") is { } id)
            try { await sender.Send("answerCallbackQuery", new() { ["callback_query_id"] = id }, ct); }
            catch (BotApiException) { }   // eskirgan callback

        if (next != null && msg!.Value.Obj("chat")?.Long("id") is { } chatId && msg.Value.Long("message_id") is { } mid)
            await sender.Send("editMessageText", new()
            {
                ["chat_id"] = chatId, ["message_id"] = mid, ["text"] = Texts.SettingsTitle, ["reply_markup"] = Keyboard(next),
            }, ct);
    }

    private static object Keyboard(Settings s)
    {
        static string Mark(bool on, string text) => (on ? "✅ " : "⬜ ") + text;
        return Buttons.Markup(
            [Buttons.Callback("🔁 " + (s.Reverse ? Texts.SetNewToOld : Texts.SetOldToNew), "set:r")],
            [Buttons.Callback(Mark(s.Cyrillic, Texts.SetCyrillic), "set:c")],
            [Buttons.Callback(Mark(s.Dictionary, Texts.SetWords), "set:w")],
            [Buttons.Callback(Mark(s.LongAsFile, Texts.SetFile), "set:f")]);
    }
}
