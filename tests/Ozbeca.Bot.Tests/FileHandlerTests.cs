using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;
using Ozbeca.Bot.Handlers;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

public class FileHandlerTests : IDisposable
{
    /// <summary>sendDocument multipart ichidagi fayl baytlarini ushlaydi (FakeTelegram faqat nomini saqlaydi).</summary>
    private sealed class Capture(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public byte[]? Sent;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.Content is MultipartFormDataContent mp)
                foreach (var part in mp)
                    if (part.Headers.ContentDisposition?.FileName != null) Sent = await part.ReadAsByteArrayAsync(ct);
            return await base.SendAsync(req, ct);
        }
    }

    private readonly BotHarness _h = new();
    private readonly string _work = Directory.CreateTempSubdirectory("ozbeca-work").FullName;
    private readonly Capture _cap;

    public FileHandlerTests() { _cap = new Capture(_h.Fake); }

    public void Dispose() { _h.Dispose(); Directory.Delete(_work, true); }

    private FileHandler Make(long maxUnzipped = 200_000_000)
    {
        var api = new BotApi(_cap, _h.Config.Token);
        return new FileHandler(api, new Sender(api), _h.Store, PremiumEmoji.LoadEmbedded(false), _work, 20_000_000, maxUnzipped);
    }

    private static byte[] Docx(string text)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            using (var s = zip.CreateEntry("[Content_Types].xml").Open()) s.Write(Encoding.UTF8.GetBytes("<Types/>"));
            using var d = zip.CreateEntry("word/document.xml").Open();
            d.Write(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>"
                + text + "</w:t></w:r></w:p></w:body></w:document>"));
        }
        return ms.ToArray();
    }

    private static JsonElement Msg(string? name, long? size = null, string id = "F1")
    {
        var doc = new Dictionary<string, object?> { ["file_id"] = id, ["file_name"] = name, ["file_size"] = size };
        return BotHarness.Json(new { message_id = 1, chat = new { id = 1, type = "private" }, document = doc });
    }

    private UserRow User(bool reverse = false)
    {
        var u = _h.Store.Touch(1);
        return u with { Settings = u.Settings with { Reverse = reverse } };
    }

    private void Serve(string name, byte[] bytes)
    {
        _h.Fake.Files["docs/" + name] = bytes;
        _h.Fake.Respond = (m, _) => m == "getFile" ? "{\"ok\":true,\"result\":{\"file_id\":\"F1\",\"file_path\":\"docs/" + name + "\"}}" : FakeTelegram.DefaultResponse;
    }

    private string LastText() => _h.Sent().Last().GetProperty("text").GetString()!;

    [Fact]
    public async Task Docx_converted_and_sent_back()
    {
        Serve("Hisobot.docx", Docx("Shahar o'zi"));
        await Make().Handle(Msg("Hisobot.docx", 100), User(), default);
        var doc = _h.Sent("sendDocument").Single();
        Assert.Equal("file:Hisobot (yangi alifbo).docx", doc.GetProperty("document").GetString());
        Assert.Contains(_h.Fake.Calls, c => c.Method == "sendChatAction");
        using var zip = new ZipArchive(new MemoryStream(_cap.Sent!));
        using var r = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        Assert.Contains("Şahar", r.ReadToEnd());
        Assert.Equal(1, _h.Store.GetStats().Today["file"].Count);
    }

    [Fact]
    public async Task Reverse_txt_named_eski_alifbo()
    {
        Serve("a.txt", Encoding.UTF8.GetBytes("Şahar"));
        await Make().Handle(Msg("a.txt", 5), User(reverse: true), default);
        var doc = _h.Sent("sendDocument").Single();
        Assert.Equal("file:a (eski alifbo).txt", doc.GetProperty("document").GetString());
        Assert.Equal("Shahar", Encoding.UTF8.GetString(_cap.Sent!));
        Assert.Contains(Texts.FileDoneOld, doc.GetProperty("caption").GetString());
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("anim.gif.mp4")]
    [InlineData(null)]
    public async Task Unsupported_name_rejected(string? name)
    {
        await Make().Handle(Msg(name, 10), User(), default);
        Assert.Equal(Texts.FileUnsupported, LastText());
        Assert.DoesNotContain(_h.Fake.Calls, c => c.Method == "getFile");
    }

    [Fact]
    public async Task Over_20mb_rejected_without_download()
    {
        await Make().Handle(Msg("a.docx", 20_000_001), User(), default);
        Assert.Equal(Texts.FileTooBig, LastText());
        Assert.DoesNotContain(_h.Fake.Calls, c => c.Method == "getFile");
    }

    [Fact]
    public async Task Broken_zip_reports_broken()
    {
        Serve("a.docx", Encoding.UTF8.GetBytes("not a zip"));
        await Make().Handle(Msg("a.docx", 9), User(), default);
        Assert.Equal(Texts.FileBroken, LastText());
        Assert.Empty(_h.Sent("sendDocument"));
    }

    [Fact]
    public async Task Zip_bomb_rejected()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            zip.CreateEntry("word/document.xml").Open().Write(new byte[10_000]);
        Serve("b.docx", ms.ToArray());
        await Make(maxUnzipped: 1_000).Handle(Msg("b.docx", 100), User(), default);
        Assert.Equal(Texts.FileBroken, LastText());
    }

    [Fact]
    public async Task No_change_reports_no_change()
    {
        Serve("a.txt", Encoding.UTF8.GetBytes("123"));
        await Make().Handle(Msg("a.txt", 3), User(), default);
        Assert.Equal(Texts.FileNoChange, LastText());
    }

    [Fact]
    public async Task Second_file_while_busy_gets_busy_message()
    {
        var started = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        Serve("a.txt", Encoding.UTF8.GetBytes("Shahar"));
        _h.Fake.Respond = (m, _) =>
        {
            if (m != "getFile") return FakeTelegram.DefaultResponse;
            started.Set(); release.Wait(5000);
            return "{\"ok\":true,\"result\":{\"file_path\":\"docs/a.txt\"}}";
        };
        var handler = Make();
        var first = Task.Run(() => handler.Handle(Msg("a.txt", 6), User(), default));
        Assert.True(started.Wait(5000));
        await handler.Handle(Msg("a.txt", 6), User(), default);
        Assert.Equal(Texts.FileBusy, LastText());
        release.Set();
        await first;
        Assert.Single(_h.Sent("sendDocument"));
        await handler.Handle(Msg("a.txt", 6), User(), default);   // qulf ochilgan
        Assert.Equal(2, _h.Sent("sendDocument").Count);
    }

    [Fact]
    public async Task Workdir_empty_after_each_case()
    {
        Serve("a.docx", Docx("Shahar"));
        await Make().Handle(Msg("a.docx", 10), User(), default);
        Serve("b.docx", Encoding.UTF8.GetBytes("junk"));
        await Make().Handle(Msg("b.docx", 4), User(), default);
        Assert.Empty(Directory.GetFileSystemEntries(_work));
    }

    [Fact]
    public async Task Router_animation_document_is_unsupported()
    {
        await _h.Msg(new()
        {
            ["animation"] = new { file_id = "A" },
            ["document"] = new { file_id = "A", file_name = "x.mp4", file_size = 10 },
        });
        Assert.Equal(Texts.FileUnsupported, LastText());
        Assert.DoesNotContain(_h.Fake.Calls, c => c.Method == "getFile");
    }
}
