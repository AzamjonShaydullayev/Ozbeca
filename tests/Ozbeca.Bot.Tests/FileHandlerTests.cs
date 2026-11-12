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
        Assert.Equal((0L, 1), _h.Store.UserTotals(1));
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

    [Fact]
    public async Task Malformed_xml_reports_broken()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            using (var s = zip.CreateEntry("[Content_Types].xml").Open()) s.Write(Encoding.UTF8.GetBytes("<Types/>"));
            using var d = zip.CreateEntry("word/document.xml").Open();
            d.Write(Encoding.UTF8.GetBytes("<w:document><w:t>Shahar"));
        }
        Serve("m.docx", ms.ToArray());
        await Make().Handle(Msg("m.docx", 100), User(), default);
        Assert.Equal(Texts.FileBroken, LastText());
        Assert.Empty(Directory.GetFileSystemEntries(_work));
    }

    [Fact]
    public async Task Very_long_cyrillic_name_works()
    {
        var name = new string('Ш', 120) + ".txt";   // 240 bayt
        Serve("l.txt", Encoding.UTF8.GetBytes("Shahar"));
        await Make().Handle(Msg(name, 6), User(), default);
        var sent = _h.Sent("sendDocument").Single().GetProperty("document").GetString()!;
        Assert.EndsWith(" (yangi alifbo).txt", sent);
        Assert.True(Encoding.UTF8.GetByteCount(sent) < 255);
    }

    [Fact]
    public void SafeName_does_not_split_emoji() =>
        Assert.Equal(new string('a', 79) + ".txt", FileHandler.SafeName(new string('a', 79) + "😀b.txt"));

    [Theory]
    [InlineData("../../x.txt", "x (yangi alifbo).txt")]
    [InlineData("..\\..\\x.txt", "x (yangi alifbo).txt")]
    [InlineData(".txt", "file (yangi alifbo).txt")]
    [InlineData("a<b>c|d.txt", "a_b_c_d (yangi alifbo).txt")]
    public async Task Unsafe_names_stay_inside_workdir(string name, string expected)
    {
        Serve("s.txt", Encoding.UTF8.GetBytes("Shahar"));
        await Make().Handle(Msg(name, 6), User(), default);
        Assert.Equal("file:" + expected, _h.Sent("sendDocument").Single().GetProperty("document").GetString());
        Assert.Empty(Directory.GetFileSystemEntries(_work));
        Assert.False(File.Exists(Path.Combine(_work, "..", "x.txt")));
    }

    [Fact]
    public async Task Gate_timeout_replies_busy_and_cleans_up()
    {
        Serve("g.txt", Encoding.UTF8.GetBytes("Shahar"));
        FileHandler.Gate.Wait();
        try
        {
            var api = new BotApi(_cap, _h.Config.Token);
            var handler = new FileHandler(api, new Sender(api), _h.Store, PremiumEmoji.LoadEmbedded(false), _work)
                { GateTimeout = TimeSpan.FromMilliseconds(200) };
            await handler.Handle(Msg("g.txt", 6), User(), default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Texts.FileBusy, LastText());
            Assert.Empty(_h.Sent("sendDocument"));
            Assert.Empty(Directory.GetFileSystemEntries(_work));
        }
        finally { FileHandler.Gate.Release(); }
        Assert.Equal(1, FileHandler.Gate.CurrentCount);   // olinmagan qulf qo'yib yuborilmadi
    }

    [Fact]
    public async Task Conversion_gate_serialises_conversions()
    {
        Serve("g.txt", Encoding.UTF8.GetBytes("Shahar"));
        FileHandler.Gate.Wait();
        Task t;
        try
        {
            t = Make().Handle(Msg("g.txt", 6), User(), default);
            await Task.Delay(300);
            Assert.False(t.IsCompleted);
            Assert.Empty(_h.Sent("sendDocument"));
        }
        finally { FileHandler.Gate.Release(); }
        await t;
        Assert.Single(_h.Sent("sendDocument"));
    }

    [Fact]
    public async Task Send_retries_once_on_429()
    {
        Serve("r.txt", Encoding.UTF8.GetBytes("Shahar"));
        var n = 0;
        var inner = _h.Fake.Respond!;
        _h.Fake.RespondRaw = (m, b) => m == "sendDocument" && n++ == 0
            ? (429, "{\"ok\":false,\"error_code\":429,\"description\":\"Too Many Requests\",\"parameters\":{\"retry_after\":0}}")
            : (200, inner(m, b));
        await Make().Handle(Msg("r.txt", 6), User(), default);
        Assert.Equal(2, _h.Sent("sendDocument").Count);
    }

    [Fact]
    public void Purge_empties_workdir_without_following_links()
    {
        var outside = Directory.CreateTempSubdirectory("ozbeca-outside").FullName;
        try
        {
            File.WriteAllText(Path.Combine(outside, "keep.txt"), "x");
            Directory.CreateDirectory(Path.Combine(_work, "abc", "sub"));
            File.WriteAllText(Path.Combine(_work, "abc", "sub", "in.docx"), "x");
            File.WriteAllText(Path.Combine(_work, "loose.txt"), "x");
            var link = Path.Combine(_work, "link");
            try { Directory.CreateSymbolicLink(link, outside); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Windows'da symlink huquqi bo'lmasa — junction (huquq talab qilmaydi)
                System.Diagnostics.Process.Start("cmd", $"/c mklink /J \"{link}\" \"{outside}\"")!.WaitForExit();
            }
            Assert.NotNull(new DirectoryInfo(link).LinkTarget);

            FileHandler.PurgeWorkDir(_work);

            Assert.True(Directory.Exists(_work));
            Assert.Empty(Directory.GetFileSystemEntries(_work));
            Assert.True(File.Exists(Path.Combine(outside, "keep.txt")));
        }
        finally { Directory.Delete(outside, true); }
    }

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a.csv")]
    public async Task Cp1251_text_file_decoded(string name)
    {
        // cp1251 da ҳ/қ/ғ yo'q — shu kod sahifasida bor harflar (ў bilan)
        var bytes = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1251)!.GetBytes("Тошкент ўзи, Shahar");
        Serve(name, bytes);
        await Make().Handle(Msg(name, bytes.Length), User(), default);
        Assert.Single(_h.Sent("sendDocument"));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, _cap.Sent![..3]);   // natija UTF-8 BOM bilan
        var text = Encoding.UTF8.GetString(_cap.Sent[3..]);
        Assert.DoesNotContain('�', text);
        Assert.Equal("Toşkent özi, Şahar", text);
    }

    // ---- C1: zararli Office XML (chuqur ichma-ich, element portlashi, DTD) ----

    private const string WNs = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private static byte[] Pkg(params (string Name, string Xml)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (name, xml) in entries)
                using (var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open()) s.Write(Encoding.UTF8.GetBytes(xml));
        return ms.ToArray();
    }

    private static string WordBody(string inner) => $"<w:document {WNs}><w:body>{inner}</w:body></w:document>";

    private async Task AssertBrokenFast(byte[] bytes)
    {
        Serve("x.docx", bytes);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Make().Handle(Msg("x.docx", bytes.Length), User(), default);
        sw.Stop();
        Assert.Equal(Texts.FileBroken, LastText());
        Assert.Empty(_h.Sent("sendDocument"));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"sekin: {sw.Elapsed}");
        Assert.Empty(Directory.GetFileSystemEntries(_work));
    }

    [Fact]
    public async Task Deep_nesting_rejected_fast()
    {
        const int depth = 20_000;
        var xml = WordBody("<w:p>" + string.Concat(Enumerable.Repeat("<w:r>", depth)) + "<w:t>Shahar</w:t>"
            + string.Concat(Enumerable.Repeat("</w:r>", depth)) + "</w:p>");
        await AssertBrokenFast(Pkg(("word/document.xml", xml)));
    }

    [Fact]
    public async Task Element_count_blowup_rejected()
    {
        var xml = WordBody(string.Concat(Enumerable.Repeat("<w:p/>", 1_000_001)) + "<w:p><w:r><w:t>Shahar</w:t></w:r></w:p>");
        await AssertBrokenFast(Pkg(("word/document.xml", xml)));
    }

    [Theory]
    [InlineData("word/document.xml")]
    [InlineData("_rels/.rels")]
    public async Task Doctype_rejected(string part)
    {
        var dtd = "<?xml version=\"1.0\"?><!DOCTYPE r [<!ENTITY e \"Shahar\">]>";
        var doc = WordBody("<w:p><w:r><w:t>" + (part == "word/document.xml" ? "&e;" : "Shahar") + "</w:t></w:r></w:p>");
        var bytes = part == "word/document.xml"
            ? Pkg(("word/document.xml", dtd.Replace("<!DOCTYPE r", "<!DOCTYPE w:document") + doc))
            : Pkg(("_rels/.rels", dtd + "<r/>"), ("word/document.xml", doc));
        await AssertBrokenFast(bytes);
    }

    [Theory]
    [InlineData("a.docx", "word/document.xml", "<w:document " + WNs + "><w:body><w:p><w:r><w:t>Shahar</w:t></w:r></w:p></w:body></w:document>")]
    [InlineData("a.xlsx", "xl/sharedStrings.xml", "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>Shahar</t></si></sst>")]
    [InlineData("a.pptx", "ppt/slides/slide1.xml", "<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>Shahar</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>")]
    public async Task Normal_office_files_still_convert(string name, string part, string xml)
    {
        const string rels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"t\" Target=\"x.xml\"/></Relationships>";
        Serve(name, Pkg(("[Content_Types].xml", "<Types/>"), ("_rels/.rels", rels), (part, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + xml)));
        await Make().Handle(Msg(name, 100), User(), default);
        Assert.Single(_h.Sent("sendDocument"));
        using var zip = new ZipArchive(new MemoryStream(_cap.Sent!));
        using var r = new StreamReader(zip.GetEntry(part)!.Open());
        Assert.Contains("Şahar", r.ReadToEnd());
    }
}
