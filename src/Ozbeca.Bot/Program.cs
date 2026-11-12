using System.Runtime.InteropServices;
using Ozbeca.Bot;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;

DotEnv.Load(".env");

BotConfig cfg;
try { cfg = BotConfig.FromEnv(); }
catch (InvalidOperationException e)
{
    Log.Error("Sozlama xatosi: " + e.Message);   // xabarlarda qiymat yo'q
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; cts.Cancel(); });

try
{
    var dbDir = Path.GetDirectoryName(Path.GetFullPath(cfg.DbPath));
    if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);
    Directory.CreateDirectory(cfg.WorkDir);

    using var handler = new SocketsHttpHandler();
    var api = new BotApi(handler, cfg.Token);
    var store = new Store(cfg.DbPath);
    var sender = new Sender(api);
    var router = new Router(api, sender, store, PremiumEmoji.LoadEmbedded(cfg.PremiumEmoji), cfg);

    var me = await api.Call("getMe", null, cts.Token);
    Log.Info($"Bot ishga tushdi: @{me.Str("username")}");
    await api.Call("setMyCommands", new
    {
        commands = new[]
        {
            new { command = "start", description = Texts.CmdStart },
            new { command = "sozlama", description = Texts.CmdSettings },
            new { command = "help", description = Texts.CmdHelp },
        },
    }, cts.Token);

    await new Poller(api, router).RunAsync(cts.Token);
    Log.Info("Bot to'xtatildi.");
    return 0;
}
catch (OperationCanceledException) { return 0; }
catch (Exception e)
{
    // Token va xabar matni yozilmaydi: faqat tur (BotApiException uchun kod).
    Log.Error("Ishga tushmadi: " + (e is BotApiException b ? $"Telegram code={b.Code}" : e.GetType().Name));
    return 1;
}
