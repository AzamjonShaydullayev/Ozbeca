using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Ui;
using Ozbeca.Core;

namespace Ozbeca.Bot.Handlers;

/// <summary>
/// Lichkadagi hujjatni o'girish: tekshiruv (nom, hajm) -> yuklash (WORK_DIR/guid) -> o'girish -> yuborish.
/// Fayl mazmuni va nomi logga yozilmaydi; ish papkasi har holda o'chiriladi.
/// </summary>
internal sealed class FileHandler(BotApi api, Sender sender, Store store, PremiumEmoji emoji, string workDir,
    long maxBytes = 20_000_000, long maxUnzipped = 200_000_000)
{
    private readonly ConcurrentDictionary<long, byte> _busy = new();

    public async Task Handle(JsonElement message, UserRow user, CancellationToken ct)
    {
        var doc = message.Obj("document");
        var fileId = doc?.Str("file_id");
        var rawName = doc?.Str("file_name");
        var size = doc?.Long("file_size");

        // Ishonch chegarasi: nom, id, hajm — yuklashdan oldin tekshiriladi.
        if (fileId is not { Length: > 0 and <= 256 } || string.IsNullOrWhiteSpace(rawName) || !DocumentConverter.IsSupported(rawName))
        { await Say(user.Id, Texts.FileUnsupported, ct); return; }
        if (size > maxBytes) { await Say(user.Id, Texts.FileTooBig, ct); return; }

        if (!_busy.TryAdd(user.Id, 0)) { await Say(user.Id, Texts.FileBusy, ct); return; }
        var dir = Path.Combine(workDir, Guid.NewGuid().ToString("N"));
        try
        {
            await Process(user, fileId, rawName, size, dir, ct);
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            _busy.TryRemove(user.Id, out _);
        }
    }

    private async Task Process(UserRow user, string fileId, string rawName, long? size, string dir, CancellationToken ct)
    {
        var reverse = user.Settings.Reverse;
        try { await api.Call("sendChatAction", new { chat_id = user.Id, action = "upload_document" }, ct); }
        catch (BotApiException) { }   // bezak, o'girishga xalaqit bermaydi

        var safe = SafeName(rawName);
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, safe);
        try
        {
            var info = await api.Call("getFile", new { file_id = fileId }, ct);
            var remote = info.Str("file_path") ?? throw new InvalidDataException("no file_path");
            await using var fs = File.Create(input);
            await api.Download(remote, fs, maxBytes, ct);
        }
        catch (BotApiException) { await Say(user.Id, Texts.FileTooBig, ct); return; }   // getFile 400: Telegram 20 MB dan kattani bermaydi
        catch (InvalidDataException) { await Say(user.Id, Texts.FileTooBig, ct); return; }

        var output = DocumentConverter.OutputPath(input, reverse);
        try
        {
            if (DocumentConverter.OfficeExtensions.Contains(Path.GetExtension(input).ToLowerInvariant()))
            {
                // ponytail: Length — ZIP sarlavhasidagi e'lon qilingan hajm; haqiqiy oqim chegarasi yo'q. Kerak bo'lsa sanovchi oqim.
                using var zip = ZipFile.OpenRead(input);
                if (zip.Entries.Sum(e => e.Length) > maxUnzipped) { await Say(user.Id, Texts.FileBroken, ct); return; }
            }
            if (!DocumentConverter.Convert(input, output, user.Settings.ToOptions())) { await Say(user.Id, Texts.FileNoChange, ct); return; }
        }
        catch (InvalidDataException) { await Say(user.Id, Texts.FileBroken, ct); return; }
        catch (NotSupportedException) { await Say(user.Id, Texts.FileUnsupported, ct); return; }

        await SendResult(user, output, Path.GetFileName(output), ct);
        store.AddUsage("file", 0);
        Log.Info($"file ok user={user.Id} bytes={size ?? -1}");
    }

    private async Task SendResult(UserRow user, string path, string name, CancellationToken ct)
    {
        var (caption, ents) = new MessageBuilder(emoji, user.Id).Emoji("success")
            .Text(" " + (user.Settings.Reverse ? Texts.FileDoneOld : Texts.FileDone)).Build();
        var fields = new Dictionary<string, string> { ["chat_id"] = user.Id.ToString(), ["caption"] = caption };
        if (ents.Count > 0) fields["caption_entities"] = JsonSerializer.Serialize(ents.Select(e => e.ToJson()), Json.Options);
        while (true)
        {
            await using var fs = File.OpenRead(path);
            try { await api.Upload("sendDocument", fields, "document", name, fs, ct); return; }
            catch (BotApiException e) when (e.Code == 400 && fields.Remove("caption_entities")) { }   // premium emoji rad etildi — bezaksiz qayta
        }
    }

    private static string SafeName(string name)
    {
        var n = Path.GetFileName(name.Replace('\\', '/'));
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Length == 0 || n.StartsWith('.') ? "file" + Path.GetExtension(n) : n;
    }

    private Task Say(long chatId, string text, CancellationToken ct) =>
        sender.Send("sendMessage", new() { ["chat_id"] = chatId, ["text"] = text }, ct);
}
