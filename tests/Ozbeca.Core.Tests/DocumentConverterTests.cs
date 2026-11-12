using System.IO.Compression;
using System.Text;
using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

/// <summary>Fayl o'girish: docx/xlsx/pptx (formatlash saqlanadi) va matn fayllari.</summary>
public class DocumentConverterTests : IDisposable
{
    private const string Wns = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Ans = "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"";
    private const string Xns = "xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"";
    private const string Decl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";

    private readonly string _dir = Directory.CreateTempSubdirectory("ozbeca-doc").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private string Zip(string name, params (string Entry, byte[] Data)[] entries)
    {
        string path = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entry, data) in entries)
            using (var s = zip.CreateEntry(entry).Open()) s.Write(data);
        return path;
    }

    private static (string, byte[]) Xml(string entry, string body) => (entry, Encoding.UTF8.GetBytes(Decl + body));

    private static string Read(string zipPath, string entry)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        using var r = new StreamReader(zip.GetEntry(entry)!.Open());
        return r.ReadToEnd();
    }

    private string Convert(string input)
    {
        string output = DocumentConverter.OutputPath(input);
        Assert.True(DocumentConverter.Convert(input, output));
        return output;
    }

    [Fact]
    public void Docx_SplitRunsKeepFormatting()
    {
        // Word "O'zbekiston" ni ikki run'ga bo'lgan: O | 'zbekiston (qalin). Tab — so'zlar yopishmasin.
        string doc = $"<w:document {Wns}><w:body><w:p>" +
                     "<w:r><w:t>O</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>'zbekiston</w:t></w:r>" +
                     "<w:r><w:t xml:space=\"preserve\"> shahar</w:t></w:r><w:r><w:tab/><w:t>choy</w:t></w:r>" +
                     "<w:r><w:instrText> HYPERLINK \"https://shop.uz\" </w:instrText></w:r>" +
                     "</w:p></w:body></w:document>";
        byte[] image = { 1, 2, 3, 4, 5 };
        string input = Zip("a.docx", Xml("[Content_Types].xml", "<Types/>"), Xml("word/document.xml", doc), ("word/media/image1.png", image));

        string output = Convert(input);
        string xml = Read(output, "word/document.xml");
        Assert.Contains("<w:t></w:t>", xml);                                  // "O" → bo'sh, Ö keyingi run'da
        Assert.Contains("<w:b /></w:rPr><w:t>Özbekiston</w:t>", xml);         // qalin run saqlandi
        Assert.Contains("<w:t xml:space=\"preserve\"> şahar</w:t>", xml);
        Assert.Contains("<w:tab /><w:t>çoy</w:t>", xml);
        Assert.Contains("HYPERLINK \"https://shop.uz\"", xml);               // maydon kodi tegilmadi
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>", xml);
        using var zip = ZipFile.OpenRead(output);
        Assert.Equal(new[] { "[Content_Types].xml", "word/document.xml", "word/media/image1.png" }, zip.Entries.Select(e => e.FullName));
        using var ms = new MemoryStream();
        zip.GetEntry("word/media/image1.png")!.Open().CopyTo(ms);
        Assert.Equal(image, ms.ToArray());
        Assert.EndsWith("a (yangi alifbo).docx", output);
    }

    [Fact]
    public void Docx_TextBoxParagraphSeparate_UrlAndEnglishKept()
    {
        string doc = $"<w:document {Wns}><w:body><w:p><w:r><w:t xml:space=\"preserve\">Bog' </w:t></w:r>" +
                     "<w:r><w:txbxContent><w:p><w:r><w:t>to'g'ri</w:t></w:r></w:p></w:txbxContent></w:r>" +
                     "<w:r><w:t>www.shop.uz check</w:t></w:r></w:p></w:body></w:document>";
        string output = Convert(Zip("b.docx", Xml("word/document.xml", doc)));
        string xml = Read(output, "word/document.xml");
        Assert.Contains(">Boğ </w:t>", xml);
        Assert.Contains(">töğri</w:t>", xml);
        Assert.Contains(">www.shop.uz check</w:t>", xml);
    }

    [Fact]
    public void Xlsx_SharedStringsAndTableColumnMatch()
    {
        string sst = $"<sst {Xns} count=\"2\" uniqueCount=\"2\"><si><t>O'quvchi</t></si>" +
                     "<si><r><t>Ish</t></r><r><rPr><b/></rPr><t>xona</t></r></si></sst>";
        string table = $"<table {Xns} id=\"1\" name=\"T\" ref=\"A1:A2\"><tableColumns count=\"1\"><tableColumn id=\"1\" name=\"O'quvchi\"/></tableColumns></table>";
        string sheet = $"<worksheet {Xns}><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><f>SUM(1,2)</f><v>3</v></c><c r=\"C1\"><f>COUNTA(T[O''quvchi])&amp;\"o'quvchi\"</f></c></row></sheetData></worksheet>";
        string output = Convert(Zip("c.xlsx", Xml("xl/sharedStrings.xml", sst), Xml("xl/tables/table1.xml", table), Xml("xl/worksheets/sheet1.xml", sheet)));

        string s = Read(output, "xl/sharedStrings.xml");
        Assert.Contains("<t>Öquvçi</t>", s);
        Assert.Contains("<t>Iş</t>", s);     // Ish | xona — run'lar bo'yicha: Iş + xona
        Assert.Contains("<t>xona</t>", s);
        Assert.Contains("name=\"Öquvçi\"", Read(output, "xl/tables/table1.xml"));
        string sh = Read(output, "xl/worksheets/sheet1.xml");
        Assert.Contains("<f>SUM(1,2)</f>", sh);
        Assert.Contains("<f>COUNTA(T[Öquvçi])&amp;\"o'quvchi\"</f>", sh); // ustun havolasi yangilandi, formula matni — yo'q
    }

    [Fact]
    public void Pptx_DrawingText()
    {
        string slide = $"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" {Ans}><p:cSld><p:spTree><p:sp><p:txBody>" +
                       "<a:p><a:r><a:t>Ma'ruza: G'</a:t></a:r><a:r><a:t>ulom</a:t></a:r><a:br/><a:r><a:t>shahar</a:t></a:r></a:p>" +
                       "</p:txBody></p:sp></p:spTree></p:cSld></p:sld>";
        string output = Convert(Zip("d.pptx", Xml("ppt/slides/slide1.xml", slide)));
        string xml = Read(output, "ppt/slides/slide1.xml");
        Assert.Contains("<a:t>Maʼruza: Ğ</a:t>", xml); // G' → Ğ birinchi run'da qoladi (G o'sha yerda edi)
        Assert.Contains("<a:t>ulom</a:t>", xml);
        Assert.Contains("<a:br /><a:r><a:t>şahar</a:t>", xml);
    }

    [Fact]
    public void Docx_UzbekCyrillic()
    {
        string doc = $"<w:document {Wns}><w:body><w:p><w:r><w:t>Ўзбекистон ва o'qish</w:t></w:r></w:p></w:body></w:document>";
        string xml = Read(Convert(Zip("e.docx", Xml("word/document.xml", doc))), "word/document.xml");
        Assert.Contains("<w:t>Özbekiston va öqiş</w:t>", xml);
    }

    [Fact]
    public void RussianCyrillicNotTouched() =>
        Assert.Equal("Привет, shahar", DocumentConverter.ConvertText("Привет, shahar").Replace("şahar", "shahar"));

    [Fact]
    public void NothingToConvert_NoFile()
    {
        string doc = $"<w:document {Wns}><w:body><w:p><w:r><w:t>Salom dunyo</w:t></w:r></w:p></w:body></w:document>";
        string input = Zip("f.docx", Xml("word/document.xml", doc));
        string output = DocumentConverter.OutputPath(input);
        Assert.False(DocumentConverter.Convert(input, output));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void NotZip_FriendlyError()
    {
        string input = Path.Combine(_dir, "g.docx");
        File.WriteAllBytes(input, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 1, 2, 3 }); // eski .doc / parolli fayl (CFB)
        var ex = Assert.Throws<InvalidDataException>(() => DocumentConverter.Convert(input, input + ".out"));
        Assert.Contains("parol", ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TextFile_KeepsUtf8Bom(bool bom)
    {
        string input = Path.Combine(_dir, "h.txt");
        File.WriteAllText(input, "O'zbekiston, shahar\r\nchoy", new UTF8Encoding(bom));
        byte[] outBytes = File.ReadAllBytes(Convert(input));
        Assert.Equal(bom, outBytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal("Özbekiston, şahar\r\nçoy", new UTF8Encoding(false).GetString(outBytes, bom ? 3 : 0, outBytes.Length - (bom ? 3 : 0)));
    }

    [Fact]
    public void TextFile_Utf16()
    {
        string input = Path.Combine(_dir, "i.srt");
        File.WriteAllText(input, "1\n00:00:01,000 --> 00:00:02,000\nG'alaba\n", Encoding.Unicode);
        Assert.Equal("1\n00:00:01,000 --> 00:00:02,000\nĞalaba\n", File.ReadAllText(Convert(input), Encoding.Unicode));
    }

    [Fact]
    public void OutputPath_Unique()
    {
        string input = Path.Combine(_dir, "j.txt");
        File.WriteAllText(Path.Combine(_dir, "j (yangi alifbo).txt"), "");
        Assert.Equal(Path.Combine(_dir, "j (yangi alifbo 2).txt"), DocumentConverter.OutputPath(input));
    }

    [Fact]
    public void ConvertProseMap_Monotonic()
    {
        string s = "O'zbek shahar www.x.uz Ўзбек";
        var map = new int[s.Length + 1];
        string r = CodeGuard.ConvertProse(s, true, map);
        Assert.Equal(r.Length, map[s.Length]);
        for (int i = 1; i <= s.Length; i++) Assert.True(map[i] >= map[i - 1]);
    }
}
