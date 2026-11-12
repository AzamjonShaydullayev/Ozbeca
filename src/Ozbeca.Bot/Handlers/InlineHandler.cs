using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Text;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Handlers;

/// <summary>Inline rejim: ikki natija (yangi/eski alifbo), faqat natija matni. Foydalanuvchi matni saqlanmaydi va logga yozilmaydi.</summary>
internal sealed class InlineHandler(Sender sender, Store store)
{
    private const int MaxQuery = 256;
    private const int MaxDescription = 100;

    public async Task HandleQuery(JsonElement q, CancellationToken ct)
    {
        if (q.Str("id") is not { } id || q.Obj("from") is not { } from || from.Long("id") is not { } userId) return;
        var query = (q.Str("query") ?? "").Trim();
        query = Cut(query, MaxQuery);

        var results = new List<object>();
        if (query.Length > 0)
        {
            // foydalanuvchi yo'q bo'lsa Touch qilinmaydi (inline orqali "user" bo'lib qolmaydi)
            var settings = store.Get(userId)?.Settings ?? new Settings();
            results.Add(Article("n", Texts.InlineNewTitle, query, settings, reverse: false));
            results.Add(Article("o", Texts.InlineOldTitle, query, settings, reverse: true));
        }
        await sender.Send("answerInlineQuery", new()
        {
            ["inline_query_id"] = id,
            ["results"] = results,
            ["cache_time"] = 10,
            ["is_personal"] = true,
            ["button"] = new { text = Texts.InlineLongButton, start_parameter = "inline" },
        }, ct);
    }

    public Task HandleChosen(JsonElement r)
    {
        var chars = (r.Str("query") ?? "").Length;
        store.AddUsage("inline", chars);   // user qatori shart emas
        if (r.Obj("from")?.Long("id") is { } userId) store.AddChars(userId, chars);   // shaxsiy statistika — faqat mavjud userga
        return Task.CompletedTask;
    }

    private static object Article(string id, string title, string query, Settings s, bool reverse)
    {
        var text = TextPipeline.Convert(query, [], s.ToOptions() with { Reverse = reverse }).Text;
        return new
        {
            type = "article", id, title,
            description = Cut(text, MaxDescription),
            input_message_content = new { message_text = text },
        };
    }

    /// <summary>max UTF-16 birlikkacha; surrogate juft (emoji) bo'linmaydi — yolg'iz yarmi Telegram'da 400 beradi.</summary>
    private static string Cut(string s, int max) =>
        s.Length <= max ? s : s[..(char.IsHighSurrogate(s[max - 1]) ? max - 1 : max)];
}
