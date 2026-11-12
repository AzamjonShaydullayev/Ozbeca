using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

/// <summary>Tutuqsiz yozilgan so'zlar lug'ati (ozbek → özbek) — faqat bir ma'noli so'zlar.</summary>
public class WordListTests
{
    private static string T(string s) => Transliterator.ToNewAlphabet(s, dictionary: true);

    [Theory]
    [InlineData("ozbek", "özbek")]
    [InlineData("Ozbekiston", "Özbekiston")]
    [InlineData("OZBEKISTON", "ÖZBEKISTON")]
    [InlineData("ozbekistonda", "özbekistonda")]
    [InlineData("kop", "köp")]
    [InlineData("togri", "töğri")]
    [InlineData("to'gri", "töğri")]              // faqat g' tushib qolgan
    [InlineData("tog'ri", "töğri")]              // faqat o' tushib qolgan
    [InlineData("ogil", "öğil")]
    [InlineData("bolgan boladi boldi", "bölgan böladi böldi")]
    [InlineData("garbiy Fargona", "ğarbiy Farğona")]
    [InlineData("boyicha korinadi", "böyiça körinadi")]
    [InlineData("Men Ozbekistonda yashayman, bu togri.", "Men Özbekistonda yaşayman, bu töğri.")]
    [InlineData("'ozbek'", "'özbek'")]            // tirnoq ichida ham
    public void FixesMissingApostrophe(string input, string expected) => Assert.Equal(expected, T(input));

    // Yozishmalardagi qisqargan shakllar (build_wordlist.py: colloquial).
    [Theory]
    [InlineData("bomadi", "bömadi")]
    [InlineData("bomayapti", "bömayapti")]
    [InlineData("bolmayapti", "bölmayapti")]
    [InlineData("torimas", "törimas")]
    [InlineData("togrimas", "töğrimas")]
    [InlineData("bopti", "böpti")]
    [InlineData("togirlash", "töğirlaş")]
    [InlineData("togirlashimiz", "töğirlaşimiz")]
    [InlineData("togirlab", "töğirlab")]
    public void FixesColloquialForms(string input, string expected) => Assert.Equal(expected, T(input));

    // Qisqargan shakli boshqa so'zga to'g'ri keladi — tegilmaydi: bo'ldi → bodi, bo'lsa → bosa, to'g'risi → tori+si.
    [Theory]
    [InlineData("bodi bosa bop tori torisi")]
    public void LeavesAmbiguousColloquial(string input) => Assert.Equal(input, T(input));

    // Kirill korpusidan (bitg uz.tsv): lotin korpusida kam uchragan qo'shimchali shakllar.
    [Theory]
    [InlineData("bolishi kerak", "bölişi kerak")]
    [InlineData("shahar ortasida", "şahar örtasida")]
    [InlineData("qishloq xojaligi", "qişloq xöjaligi")]
    [InlineData("sozlariga", "sözlariga")]
    public void FixesFormsFromCyrillicCorpus(string input, string expected) => Assert.Equal(expected, T(input));

    // Tutuqsiz o'zagi ham haqiqiy so'z: boshliq (bo'shliq), och (o'ch), ot (o't), yog'i/yo'g'i — tegilmaydi.
    [Theory]
    [InlineData("boshliqdan ochmaydi otdim")]
    public void LeavesAmbiguousStems(string input) => Assert.Equal(input.Replace("sh", "ş").Replace("ch", "ç"), T(input));

    // "o'z" olmoshi — "oz" (kam) bilan ikki ma'noli bo'lsa ham qo'shilgan (user qarori); "oz" o'zi tegilmaydi.
    [Theory]
    [InlineData("nima ozi", "nima özi")]
    [InlineData("Ozim qildim", "Özim qildim")]
    [InlineData("ozingizga", "özingizga")]
    [InlineData("ozlarining", "özlarining")]
    [InlineData("oz qoldi", "oz qoldi")]
    public void FixesOzPronoun(string input, string expected) => Assert.Equal(expected, T(input));

    [Fact]
    public void ColloquialAfterPause()
    {
        // "togirlash" + pauza (→ "togirlaş") + "imiz" + probel — lug'at asl yozilganini tekshiradi.
        var b = new TokenBuffer();
        string screen = "";
        void Type(string s) { foreach (char c in s) { b.Add(c); screen += c; } }
        void Apply(int del, string rep) => screen = screen.Substring(0, screen.Length - del) + rep;

        Type("togirlash");
        Assert.True(b.TryConvertAtPause(out int d, out string r));
        Apply(d, r);
        Type("imiz");
        Assert.True(b.TryComplete(out d, out r));
        Apply(d, r);
        Assert.Equal("töğirlaşimiz", screen);
    }

    // Ikki ma'noli: tutuqsiz shakli ham haqiqiy so'z — tegilmaydi.
    [Theory]
    [InlineData("qol")]       // qol / qo'l
    [InlineData("ot otdi")]   // ot / o't
    [InlineData("bor")]       // bor / bo'r
    [InlineData("oz")]        // oz / o'z
    [InlineData("tort")]      // tort / to'rt
    [InlineData("yoq soz")]   // yoq / yo'q, soz / so'z
    [InlineData("bola olim")] // bola / bo'la, olim / o'lim
    public void LeavesAmbiguousWords(string input) => Assert.Equal(input, T(input));

    [Fact]
    public void SpoonIsNotSong() => Assert.Equal("qoşiq", T("qoshiq")); // qoshiq (qoshiq) ≠ qo'shiq — faqat sh → ş

    [Theory]
    [InlineData("song bug yoga boom")]               // inglizcha so'zlar
    [InlineData("kino maktab dengiz")]
    public void LeavesOtherWords(string input) => Assert.Equal(input, T(input));

    [Fact]
    public void CanBeTurnedOff() => Assert.Equal("ozbek", Transliterator.ToNewAlphabet("ozbek", dictionary: false));

    [Fact]
    public void KeepsUrlsAndMentions() =>
        Assert.Equal("https://ozbek.uz @ozbek özbek", T("https://ozbek.uz @ozbek ozbek"));

    [Fact]
    public void MapStaysOneToOne()
    {
        // Telegram stillari uchun: o → ö bitta belgi bitta belgiga.
        string text = Transliterator.ToNewAlphabet("ozbek kop", out var map);
        Assert.Equal("özbek köp", text);
        Assert.NotNull(map);
        for (int i = 0; i <= 9; i++) Assert.Equal(i, map![i]);
    }

    [Theory]
    [InlineData("ozbek togri bolgan")]
    [InlineData("OZBEKISTON")]
    public void Idempotent(string input) => Assert.Equal(T(input), T(T(input)));

    [Fact]
    public void PauseDoesNotUseDictionary()
    {
        // "yoq" + pauza + "imli" — so'z hali tugamagan, pauzada lug'at ishlamaydi.
        var b = new TokenBuffer();
        foreach (char c in "kop") b.Add(c);
        Assert.False(b.TryConvertAtPause(out _, out _));
        Assert.True(b.TryComplete(out int del, out string rep)); // probelda — ishlaydi
        Assert.Equal(3, del);
        Assert.Equal("köp", rep);
    }

    [Fact]
    public void DictionaryWorksAfterPauseConversion()
    {
        // "organish" + pauza (→ "organiş") + probel — lug'at asl so'zni tekshiradi: "örganiş".
        var b = new TokenBuffer();
        string screen = "";
        void Type(string s) { foreach (char c in s) { b.Add(c); screen += c; } }
        void Apply(int del, string rep) => screen = screen.Substring(0, screen.Length - del) + rep;

        Type("organish");
        Assert.True(b.TryConvertAtPause(out int d, out string r));
        Apply(d, r);
        Assert.Equal("organiş", screen);
        Assert.True(b.TryComplete(out d, out r));
        Apply(d, r);
        Assert.Equal("örganiş", screen);

        // Pauzadan keyin backspace bilan tuzatish: oxirgi harf ikkalasida bir xil — asl shakl saqlanadi.
        screen = "";
        Type("organishx");
        Assert.True(b.TryConvertAtPause(out d, out r));
        Apply(d, r);
        b.Backspace();
        screen = screen.Substring(0, screen.Length - 1);
        Assert.True(b.TryComplete(out d, out r));
        Apply(d, r);
        Assert.Equal("örganiş", screen);
    }
}
