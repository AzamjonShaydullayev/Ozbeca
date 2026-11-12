using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using System.Net.Http;
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
    long maxBytes = 20_000_000, long maxUnzipped = 30_000_000)
{
    /// <summary>Jarayon bo'yicha: bir vaqtda bitta o'girish (ConvertPackage barcha qismlarni xotiraga oladi).</summary>
    internal static readonly SemaphoreSlim Gate = new(1, 1);

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
        var ext = Path.GetExtension(safe).ToLowerInvariant();
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, "in" + ext);   // diskdagi nom qisqa va ruxsat etilgan; foydalanuvchi nomi faqat javob nomi uchun
        var output = Path.Combine(dir, "out" + ext);
        try
        {
            var info = await api.Call("getFile", new { file_id = fileId }, ct);
            var remote = info.Str("file_path") ?? throw new InvalidDataException("no file_path");
            await using var fs = File.Create(input);
            await api.Download(remote, fs, maxBytes, ct);
        }
        catch (BotApiException e) { await Say(user.Id, e.Description.Contains("too big", StringComparison.OrdinalIgnoreCase) ? Texts.FileTooBig : Texts.FileBroken, ct); return; }
        catch (InvalidDataException) { await Say(user.Id, Texts.FileTooBig, ct); return; }   // oqim maxBytes dan oshdi
        catch (Exception e) when (e is IOException or HttpRequestException || e is TaskCanceledException && !ct.IsCancellationRequested)
        { await Say(user.Id, Texts.FileBroken, ct); return; }

        await Gate.WaitAsync(ct);   // MemoryMax: bir vaqtda bitta o'girish
        try
        {
            if (DocumentConverter.OfficeExtensions.Contains(ext))
            {
                // E'lon qilingan Length haqiqiy chegara: ZipArchive oqimi shu hajmdan keyin ochishni to'xtatadi.
                using var zip = ZipFile.OpenRead(input);
                if (zip.Entries.Sum(e => e.Length) > maxUnzipped) { await Say(user.Id, Texts.FileBroken, ct); return; }
                ScanXml(zip);
            }
            if (!DocumentConverter.Convert(input, output, user.Settings.ToOptions())) { await Say(user.Id, Texts.FileNoChange, ct); return; }
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or IOException or OutOfMemoryException) { await Say(user.Id, Texts.FileBroken, ct); return; }
        catch (NotSupportedException) { await Say(user.Id, Texts.FileUnsupported, ct); return; }
        finally { Gate.Release(); }

        var replyName = Path.GetFileName(DocumentConverter.OutputPath(Path.Combine(dir, safe), reverse));
        await SendResult(user, output, replyName, ct);
        store.AddUsage("file", 0);
        Log.Info($"file ok user={user.Id} bytes={size ?? -1}");
    }

    private async Task SendResult(UserRow user, string path, string name, CancellationToken ct)
    {
        var (caption, ents) = new MessageBuilder(emoji, user.Id).Emoji("success")
            .Text(" " + (user.Settings.Reverse ? Texts.FileDoneOld : Texts.FileDone)).Build();
        var fields = new Dictionary<string, string> { ["chat_id"] = user.Id.ToString(), ["caption"] = caption };
        if (ents.Count > 0) fields["caption_entities"] = JsonSerializer.Serialize(ents.Select(e => e.ToJson()), Json.Options);
        bool retried429 = false, stripped = false;
        while (true)
        {
            await using var fs = File.OpenRead(path);
            try { await api.Upload("sendDocument", fields, "document", name, fs, ct); return; }
            catch (BotApiException e) when (e.Code == 429 && !retried429)
            {
                retried429 = true;
                await sender.Delay(TimeSpan.FromSeconds(Math.Clamp(e.RetryAfter ?? 1, 0, 30)), ct);
            }
            catch (BotApiException e) when (e.Code == 400 && !stripped && Sender.IsDecorationError(e.Description))
            {
                stripped = true;
                fields.Remove("caption_entities");
            }
        }
    }

    internal const int MaxXmlDepth = 64;
    internal const int MaxXmlElements = 1_000_000;

    /// <summary>
    /// Core XDocument bilan hammasini xotiraga oladi (DTD yoqilgan): oldin har .xml/.rels qismni oqim bilan tekshiramiz.
    /// DTD, chuqurlik > MaxXmlDepth yoki jami element > MaxXmlElements — XmlException/InvalidDataException (darhol to'xtaydi).
    /// </summary>
    internal static void ScanXml(ZipArchive zip)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        long elements = 0;
        foreach (var e in zip.Entries)
        {
            if (!e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
            using var r = XmlReader.Create(e.Open(), settings);
            while (r.Read())
            {
                if (r.NodeType != XmlNodeType.Element) continue;
                if (r.Depth > MaxXmlDepth || ++elements > MaxXmlElements) throw new InvalidDataException("XML too deep or too large");
            }
        }
    }

    /// <summary>Faqat javob nomi uchun: papka qismi olib tashlanadi, noto'g'ri belgilar "_", asos 80 belgigacha.</summary>
    internal static string SafeName(string name)
    {
        var n = Path.GetFileName(name.Replace((char)92, '/'));
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        var ext = Path.GetExtension(n);
        var stem = Path.GetFileNameWithoutExtension(n).TrimStart('.');
        if (stem.Length == 0) stem = "file";
        if (stem.Length > 80) stem = stem[..80];
        return stem + ext;
    }

    private Task Say(long chatId, string text, CancellationToken ct) =>
        sender.Send("sendMessage", new() { ["chat_id"] = chatId, ["text"] = text }, ct);
}
