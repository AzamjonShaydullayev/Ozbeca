using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Ozbeca.Core;

/// <summary>
/// Faylni butunlay yangi alifboga o'giradi. Asl fayl o'zgarmaydi — natija yangi faylga yoziladi.
/// - docx/xlsx/pptx: faqat matn tugunlari (w:t, a:t, x:t) o'zgaradi — shrift, rang, jadval, rasm joyida qoladi.
///   Word so'zni bir necha run'ga bo'lishi mumkin (O | 'zbek): paragraf matni bir butun o'giriladi va xarita bo'yicha
///   run'larga qaytariladi. Maydon kodlari (instrText), formulalar, havolalar (rels) tegilmaydi.
/// - txt/csv/srt/md: kodirovka saqlanadi (ANSI bo'lsa — UTF-8, chunki ğ/ş ANSI'da yo'q).
/// Kod, URL, e-mail, inglizcha so'zlar — CodeGuard.ConvertProse qoidasi bilan o'tkazib yuboriladi.
/// Faylda o'zbek kirill harflari (ў қ ғ ҳ) bo'lsa — kirill so'zlar ham o'giriladi (rus matni bunday harfsiz — tegilmaydi).
/// </summary>
public static class DocumentConverter
{
    public static readonly string[] OfficeExtensions = { ".docx", ".docm", ".xlsx", ".xlsm", ".pptx", ".pptm" };
    public static readonly string[] TextExtensions = { ".txt", ".csv", ".srt", ".md" };

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XName Space = XNamespace.Xml + "space";

    // Matn tugunlari bor qism (<w:t>, <a:t>, <t>), Excel jadval ustunlari yoki ularga havolali formulalar ([...]) —
    // qolganlari (rasm, uslub, oddiy formulalar) ochilmaydi.
    private static readonly Regex HasText = new(@"<(\w+:)?t[ >]|<tableColumn |\[", RegexOptions.Compiled);

    public static bool IsSupported(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return OfficeExtensions.Contains(ext) || TextExtensions.Contains(ext);
    }

    /// <summary>"Hisobot.docx" → "Hisobot (yangi alifbo).docx" (band bo'lsa — "(yangi alifbo 2)").</summary>
    public static string OutputPath(string path)
    {
        string dir = Path.GetDirectoryName(path) ?? "", name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        for (int n = 1; ; n++)
        {
            string p = Path.Combine(dir, $"{name} (yangi alifbo{(n > 1 ? " " + n : "")}){ext}");
            if (!File.Exists(p)) return p;
        }
    }

    /// <summary>input → output. false — eski alifbodagi matn topilmadi (fayl yozilmaydi). Xatoda — exception (o'zbekcha).</summary>
    public static bool Convert(string input, string output)
    {
        string ext = Path.GetExtension(input).ToLowerInvariant();
        if (TextExtensions.Contains(ext)) return ConvertTextFile(input, output);
        if (OfficeExtensions.Contains(ext)) return ConvertPackage(input, output);
        throw new NotSupportedException($"{ext} fayllari o'girilmaydi.");
    }

    /// <summary>Butun matn (txt). Kirill — matnda o'zbek kirill harflari bo'lsa.</summary>
    public static string ConvertText(string text) => CodeGuard.ConvertProse(text, HasUzbekCyrillic(text), null);

    // Word/Excel'da ochiq turgan fayl ham o'qilsin (ular yozish huquqi bilan ushlab turadi).
    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static bool HasUzbekCyrillic(string s) => s.IndexOfAny(UzbekCyrillic) >= 0;
    private static readonly char[] UzbekCyrillic = "ўқғҳЎҚҒҲ".ToCharArray();

    // ---- matn fayllari ----

    private static bool ConvertTextFile(string input, string output)
    {
        byte[] bytes;
        using (var s = OpenShared(input)) { bytes = new byte[s.Length]; int n = 0; while (n < bytes.Length) n += s.Read(bytes, n, bytes.Length - n); }
        Encoding enc;
        int skip = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { enc = new UTF8Encoding(true); skip = 3; }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { enc = new UnicodeEncoding(false, true); skip = 2; }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { enc = new UnicodeEncoding(true, true); skip = 2; }
        else enc = new UTF8Encoding(false);

        string text;
        if (skip > 0) text = enc.GetString(bytes, skip, bytes.Length - skip);
        else
        {
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException)
            {
                text = Encoding.Default.GetString(bytes); // ANSI (1251/1252) — net48'da tizim kod sahifasi
                enc = new UTF8Encoding(true);             // natija UTF-8 BOM bilan: ğ/ş ANSI'da yo'q
            }
        }

        string converted = ConvertText(text);
        if (converted == text) return false;
        using var fs = File.Create(output);
        byte[] bom = enc.GetPreamble(), body = enc.GetBytes(converted);
        fs.Write(bom, 0, bom.Length);
        fs.Write(body, 0, body.Length);
        return true;
    }

    // ---- Office (OOXML: ZIP ichida XML) ----

    private static bool ConvertPackage(string input, string output)
    {
        ZipArchive src;
        var stream = OpenShared(input);
        try { src = new ZipArchive(stream, ZipArchiveMode.Read); }
        catch (InvalidDataException)
        {
            stream.Dispose();
            throw new InvalidDataException("Fayl ochilmadi: parol bilan himoyalangan, buzilgan yoki eski formatda (.doc/.xls/.ppt) bo'lishi mumkin.");
        }

        using (src)
        {
            // ponytail: matnli qismlar xotiraga to'liq o'qiladi — juda katta (100 MB+) Excel varaqlarida sekin.
            var parts = new List<(string Name, string Xml)>();
            foreach (var e in src.Entries)
            {
                if (!IsTextPart(e.FullName)) continue;
                string xml;
                using (var r = new StreamReader(e.Open(), Encoding.UTF8, true)) xml = r.ReadToEnd();
                if (HasText.IsMatch(xml)) parts.Add((e.FullName, xml));
            }
            bool cyrillic = parts.Any(p => HasUzbekCyrillic(p.Xml));

            var renames = new Dictionary<string, string>(); // Excel jadval ustunlari: eski nom → yangi
            var docs = parts.Select(p => (p.Name, Doc: XDocument.Parse(p.Xml, LoadOptions.PreserveWhitespace))).ToList();
            var touched = new HashSet<string>();
            foreach (var (name, doc) in docs)
                if (ConvertXml(doc, cyrillic, renames)) touched.Add(name);
            if (renames.Count > 0)
                foreach (var (name, doc) in docs)
                    if (RenameStructuredRefs(doc, renames)) touched.Add(name);
            if (touched.Count == 0) return false;
            var changed = docs.Where(d => touched.Contains(d.Name)).ToDictionary(d => d.Name, d => Serialize(d.Doc));

            try
            {
                using var fs = File.Create(output);
                using var dst = new ZipArchive(fs, ZipArchiveMode.Create);
                foreach (var e in src.Entries)
                {
                    var ne = dst.CreateEntry(e.FullName, CompressionLevel.Optimal);
                    ne.LastWriteTime = e.LastWriteTime;
                    using var s = ne.Open();
                    if (changed.TryGetValue(e.FullName, out var data)) s.Write(data, 0, data.Length);
                    else using (var old = e.Open()) old.CopyTo(s);
                }
            }
            catch
            {
                try { File.Delete(output); } catch (IOException) { }
                throw;
            }
            return true;
        }
    }

    // Hujjat xususiyatlari, bog'lanishlar va customXml (Word'dagi bog'langan maydonlar manbasi) — tegilmaydi.
    private static bool IsTextPart(string name) =>
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && name.IndexOf("_rels/", StringComparison.Ordinal) < 0 &&
        !name.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("customXml/", StringComparison.OrdinalIgnoreCase) &&
        !name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase);

    private static byte[] Serialize(XDocument doc)
    {
        var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false) })) doc.Save(w);
        return ms.ToArray();
    }

    /// <summary>Hujjat qismidagi barcha paragraflar (+ Excel jadval ustun nomlari). true — nimadir o'zgardi.</summary>
    private static bool ConvertXml(XDocument doc, bool cyrillic, Dictionary<string, string> renames)
    {
        bool changed = false;
        foreach (var p in doc.Descendants().Where(IsParagraph).ToList())
            changed |= ConvertParagraph(p, cyrillic);

        // Jadval ustuni nomi sarlavha katagidagi matnga teng bo'lishi shart, aks holda Excel faylni "tiklaydi".
        foreach (var col in doc.Descendants(X + "tableColumn"))
        {
            var a = col.Attribute("name");
            if (a == null) continue;
            string v = CodeGuard.ConvertProse(a.Value, cyrillic, null);
            if (v == a.Value) continue;
            renames[a.Value] = v;
            a.Value = v;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Formulalardagi jadval ustuni havolalari: =SUM(Jadval1[O''quvchi]) → Jadval1[Öquvçi]. Formulada ' [ ] # oldidan
    /// ' qo'yiladi (O'quvchi → O''quvchi). Formula ichidagi oddiy matn ("o'quvchi") o'zgarmaydi.
    /// </summary>
    private static bool RenameStructuredRefs(XDocument doc, Dictionary<string, string> renames)
    {
        bool changed = false;
        foreach (var f in doc.Descendants().Where(e => e.Name.Namespace == X && e.Name.LocalName is "f" or "formula" or "formula1" or "formula2" or "definedName"))
        {
            string v = f.Value;
            if (v.IndexOf('[') < 0) continue;
            foreach (var r in renames) v = v.Replace("[" + EscapeRef(r.Key) + "]", "[" + EscapeRef(r.Value) + "]");
            if (v != f.Value) { f.Value = v; changed = true; }
        }
        return changed;
    }

    private static string EscapeRef(string s) => Regex.Replace(s, @"['\[\]#]", "'$0");

    // Paragraf: Word w:p, PowerPoint/diagramma/matn qutisi a:p, Excel umumiy satr si, ichki satr is, izoh text.
    private static bool IsParagraph(XElement e) =>
        e.Name == W + "p" || e.Name == A + "p" || e.Name == X + "si" || e.Name == X + "is" || e.Name == X + "text";

    private static bool IsText(XElement e) =>
        e.Name == W + "t" || e.Name == A + "t" || e.Name == X + "t" && e.Parent?.Name != X + "rPh"; // rPh — yapon o'qilishi

    private static bool ConvertParagraph(XElement p, bool cyrillic)
    {
        var texts = new List<XElement>();
        var starts = new List<int>();
        var lens = new List<int>();
        var sb = new StringBuilder();
        char sep = '\0';
        foreach (var e in p.Descendants())
        {
            if (e.Ancestors().First(IsParagraph) != p) continue; // ichki paragraf (Word matn qutisi) — o'zi alohida
            if (IsText(e))
            {
                if (sep != '\0' && sb.Length > 0) sb.Append(sep); // tab/qator ko'chishi — so'zlar yopishib qolmasin
                sep = '\0';
                starts.Add(sb.Length);
                lens.Add(e.Value.Length);
                texts.Add(e);
                sb.Append(e.Value);
            }
            else if (e.Name.Namespace == W || e.Name.Namespace == A)
            {
                if (e.Name.LocalName == "tab") sep = '\t';
                else if (e.Name.LocalName is "br" or "cr") sep = '\n';
            }
        }
        if (texts.Count == 0) return false;

        string old = sb.ToString();
        var map = new int[old.Length + 1];
        string conv = CodeGuard.ConvertProse(old, cyrillic, map);
        if (conv == old) return false;

        int prevEnd = 0;
        for (int k = 0; k < texts.Count; k++)
        {
            int s = k == 0 ? 0 : Math.Min(conv.Length, Math.Max(prevEnd, map[starts[k]]));
            int end = k == texts.Count - 1 ? conv.Length : Math.Min(conv.Length, Math.Max(s, map[starts[k] + lens[k]]));
            prevEnd = end;
            string piece = conv.Substring(s, end - s);
            var t = texts[k];
            if (t.Value == piece) continue;
            t.Value = piece;
            if (t.Name.Namespace != A && piece.Length > 0 && t.Attribute(Space) == null &&
                (char.IsWhiteSpace(piece[0]) || char.IsWhiteSpace(piece[piece.Length - 1])))
                t.SetAttributeValue(Space, "preserve");
        }
        return true;
    }
}
