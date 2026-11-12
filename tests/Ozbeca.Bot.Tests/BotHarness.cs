using System.Text.Json;
using Microsoft.Data.Sqlite;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

/// <summary>FakeTelegram + vaqtinchalik Store + Router. Update JSON yig'uvchi yordamchilar bilan.</summary>
internal sealed class BotHarness : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ozbeca-{Guid.NewGuid():N}.db");
    public FakeTelegram Fake { get; } = new();
    public Store Store { get; }
    public Router Router { get; }
    public BotApi Api { get; }
    public BotConfig Config { get; }
    private int _updateId;

    public BotHarness()
    {
        Config = new BotConfig("1:TEST", new HashSet<long> { 99 }, _path, Path.GetTempPath(), false);
        Api = new BotApi(Fake, Config.Token);
        Store = new Store(_path);
        Router = new Router(Api, new Sender(Api), Store, PremiumEmoji.LoadEmbedded(false), Config);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            try { File.Delete(f); } catch (IOException) { }
    }

    public static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o);

    public Task Handle(object update) => Router.Handle(Json(update), CancellationToken.None);

    /// <summary>message update; extra — message maydonlari (text, caption, sticker, ...).</summary>
    public Task Msg(Dictionary<string, object?> extra, long uid = 1, string chatType = "private", bool isBot = false)
    {
        var m = new Dictionary<string, object?>
        {
            ["message_id"] = 10, ["date"] = 1,
            ["chat"] = new { id = uid, type = chatType },
            ["from"] = new { id = uid, is_bot = isBot },
        };
        foreach (var (k, v) in extra) m[k] = v;
        return Handle(new { update_id = ++_updateId, message = m });
    }

    public Task Text(string text, long uid = 1, object? entities = null, string chatType = "private") =>
        Msg(new() { ["text"] = text, ["entities"] = entities }, uid, chatType);

    /// <summary>Javob xabaridagi blockquote ustidagi tugma bosilishi.</summary>
    public Task Callback(string data, string? messageText, object? entities, long uid = 1) =>
        Handle(new
        {
            update_id = ++_updateId,
            callback_query = new
            {
                id = "cb1", from = new { id = uid, is_bot = false }, data,
                message = messageText == null
                    ? (object)new { message_id = 5, date = 0, chat = new { id = uid, type = "private" } }
                    : new { message_id = 5, date = 1, chat = new { id = uid, type = "private" }, text = messageText, entities },
            },
        });

    public List<JsonElement> Sent(string method = "sendMessage") =>
        Fake.Calls.Where(c => c.Method == method).Select(c => c.Body).ToList();
}
