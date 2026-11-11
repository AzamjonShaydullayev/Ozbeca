using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class TransliteratorTests
{
    private static string T(string s) => Transliterator.ToNewAlphabet(s);

    // Majburiy holatlar
    [Theory]
    [InlineData("O'zbekiston", "Özbekiston")]
    [InlineData("o‘zbek", "özbek")]
    [InlineData("o`rta", "örta")]
    [InlineData("oʻgʻil", "öğil")]
    [InlineData("O'G'IL", "ÖĞIL")]
    [InlineData("G'ALABA", "ĞALABA")]
    [InlineData("bog'", "boğ")]
    [InlineData("tog'da", "toğda")]
    [InlineData("shahar", "şahar")]
    [InlineData("Shahar", "Şahar")]
    [InlineData("SHAHAR", "ŞAHAR")]
    [InlineData("choy", "çoy")]
    [InlineData("CHOY", "ÇOY")]
    [InlineData("Chorshanba", "Çorşanba")]
    [InlineData("qo'shiq", "qöşiq")]
    [InlineData("g'isht", "ğişt")]
    [InlineData("ma'no", "maʼno")]
    [InlineData("Is'hoq", "Isʼhoq")]
    [InlineData("tong", "tong")]
    [InlineData("Bu 'kino' edi", "Bu 'kino' edi")]
    [InlineData("'salom'", "'salom'")]
    [InlineData("Özbekiston", "Özbekiston")]
    [InlineData("O'zbekiston shaharlari Çirçiq", "Özbekiston şaharlari Çirçiq")]
    [InlineData("https://shop.uz/choy", "https://shop.uz/choy")]
    [InlineData("info@shahar.uz", "info@shahar.uz")]
    [InlineData("@shohruh #choyxona", "@shohruh #choyxona")]
    [InlineData("maktab", "maktab")]
    public void Required(string input, string expected) => Assert.Equal(expected, T(input));

    // Tirnoq ichidagi ibora: oxiridagi g' — ğ emas, g + yopuvchi tirnoq (user xabari, 2026-11-11: 'Yaxshi tong' → tonğ edi).
    [Theory]
    [InlineData("'Yaxshi tong'", "'Yaxşi tong'")]
    [InlineData("U 'Yaxshi tong' dedi", "U 'Yaxşi tong' dedi")]
    [InlineData("‘Yaxshi tong’", "‘Yaxşi tong’")]
    [InlineData("'Yaxshi tong!'", "'Yaxşi tong!'")]
    [InlineData("'Yaxshi tong', dedi u", "'Yaxşi tong', dedi u")]
    [InlineData("('Yaxshi tong')", "('Yaxşi tong')")]
    [InlineData("'tong'", "'tong'")]                                        // bitta so'z — avvalgidek
    [InlineData("'Tog' baland'", "'Toğ baland'")]                           // keyinroq aniq yopuvchi bor — g' harf
    [InlineData("'Bog'da tong'", "'Boğda tong'")]
    [InlineData("'tog' va bog'", "'toğ va bog'")]                           // ikki nomzod — oxirgisi tirnoq
    [InlineData("U 'salom' dedi, bog' katta", "U 'salom' dedi, boğ katta")] // tirnoq yopilgan — keyingi g' harf
    [InlineData("'Yaxshi tong' va tog'", "'Yaxşi tong' va toğ")]
    [InlineData("bog' va tog'", "boğ va toğ")]                              // tirnoq ochilmagan
    [InlineData("'Yaxshi\ntog'", "'Yaxşi\ntoğ")]                            // tirnoq qatordan qatorga o'tmaydi
    [InlineData("'Salom!' bog'", "'Salom!' boğ")]
    public void QuotedPhraseEndingWithG(string input, string expected)
    {
        Assert.Equal(expected, T(input));
        Assert.Equal(expected, T(T(input))); // idempotent
    }

    [Fact]
    public void QuoteStateCarriesAcrossPieces()
    {
        // So'zma-so'z o'girishda (yozish paytida, Ctrl+V) tirnoq holati bo'lakdan bo'lakka o'tadi.
        bool open = false;
        Assert.Equal("'Yaxşi", Transliterator.ToNewAlphabet("'Yaxshi", true, ref open));
        Assert.True(open);
        Assert.Equal("tong'", Transliterator.ToNewAlphabet("tong'", true, ref open));
        Assert.False(open);
        Assert.Equal("tonğ", Transliterator.ToNewAlphabet("tong'", true, ref open)); // endi tirnoq yo'q — harf
    }

    // https'siz havolalar ham buzilmasin (Telegram'da t.me/... juda ko'p uchraydi).
    [Theory]
    [InlineData("Kanal: t.me/shahar_yangiliklari", "Kanal: t.me/shahar_yangiliklari")]
    [InlineData("shahar.uz saytida choy", "shahar.uz saytida çoy")]
    [InlineData("kun.uz/news/shahar ni o'qi", "kun.uz/news/shahar ni öqi")]
    [InlineData("Instagram.com/choyxona, youtu.be/shch", "Instagram.com/choyxona, youtu.be/shch")]
    [InlineData("(chat.shahar.uz)", "(chat.shahar.uz)")]
    [InlineData("shahar.uz.", "shahar.uz.")]
    // Havola emas: nuqtadan keyin probelsiz yozilgan gap, fayl nomi, kasr son.
    [InlineData("choy ichdim.Shahar katta", "çoy içdim.Şahar katta")]
    [InlineData("shahar.docx", "şahar.docx")]
    [InlineData("3.5 choy", "3.5 çoy")]
    [InlineData("ish.uzoq", "iş.uzoq")]
    public void BareLinksAreProtected(string input, string expected)
    {
        Assert.Equal(expected, T(input));
        Assert.Equal(expected, T(T(input))); // idempotent
    }

    [Fact]
    public void MultilineKeepsCrLf()
    {
        const string input = "Bog'da\r\nshaftoli\r\n\r\n  qo'shni\tchol\n";
        Assert.Equal("Boğda\r\nşaftoli\r\n\r\n  qöşni\tçol\n", T(input));
    }

    // Qo'shimcha holatlar
    [Theory]
    [InlineData("sH", "ş")]
    [InlineData("cH", "ç")]
    [InlineData("o’g’il", "öğil")]
    [InlineData("o´zbek", "özbek")]
    [InlineData("Özbekiston", "Özbekiston")]                 // NFC
    [InlineData("mo'jiza", "möjiza")]          // 1995 imlo qoidalari: oʻ dan keyin tutuq qo'yilmaydi
    [InlineData("as'hob", "asʼhob")]      // 1995 qoidalari 17-band: Isʼhoq, asʼhob
    [InlineData("Bu 'tong' edi", "Bu 'tong' edi")]  // tirnoq ichidagi so'z oxiridagi g' — yopuvchi tirnoq
    [InlineData("‘tong’", "‘tong’")]
    [InlineData("\"Bog'\" bog'", "\"Boğ\" boğ")]    // qo'shtirnoq A to'plamida emas — ğ
    [InlineData("'bog'", "'bog'")]                   // ma'lum cheklov: tirnoq bilan boshlansa bog' ham saqlanadi
    [InlineData("tog' va bog'", "toğ va boğ")]
    [InlineData("'o'zbek'", "'özbek'")]
    [InlineData("o' harfi", "ö harfi")]                  // yakka o' — harfning o'zi
    [InlineData("O‘ va G‘", "Ö va Ğ")]
    [InlineData("o'", "ö")]
    [InlineData("(o')", "(ö)")]
    [InlineData("'o' harfi", "'o' harfi")]               // tirnoq ichidagi o — o'zgarmaydi
    [InlineData("kino' ", "kino' ")]                     // so'z oxiridagi o' — yopuvchi tirnoq
    [InlineData("sana' ", "sana' ")]                                // harflar orasida emas
    [InlineData("Salom, www.shahar.uz/choy ga kir", "Salom, www.shahar.uz/choy ga kir")]
    [InlineData("Choy: http://x.uz/sh?q=o'g'il, keyin choy", "Çoy: http://x.uz/sh?q=o'g'il, keyin çoy")]
    [InlineData("chat@shahar.uz #shaxmat", "chat@shahar.uz #shaxmat")]
    [InlineData("Shoh@ yo'q", "Şoh@ yöq")]
    [InlineData("o′zbek", "özbek")]                    // ′ U+2032
    [InlineData("G＇ALABA", "ĞALABA")]                  // ＇ U+FF07
    [InlineData("ma′no", "maʼno")]
    [InlineData("șahar ȘAHAR", "şahar ŞAHAR")]    // vergulli ș → ş
    [InlineData("Ishoq", "Isʼhoq")]                     // tutuqsiz — istisno ro'yxati
    [InlineData("ishoq ISHOQ Ishoqov", "isʼhoq ISʼHOQ Isʼhoqov")]
    [InlineData("ashob Ashoblar", "asʼhob Asʼhoblar")]
    [InlineData("Is'hoq", "Isʼhoq")]                    // tutuqli ham o'sha natija
    [InlineData("kishoq", "kişoq")]                          // so'z boshida bo'lmasa — oddiy sh
    [InlineData("", "")]
    [InlineData("123 - 456!", "123 - 456!")]
    public void Extra(string input, string expected) => Assert.Equal(expected, T(input));

    // 100+ so'z: eski → yangi
    public static TheoryData<string, string> Words => new()
    {
        { "o'g'il", "öğil" }, { "qo'l", "qöl" }, { "ko'z", "köz" }, { "yo'l", "yöl" }, { "to'rt", "tört" },
        { "bo'lim", "bölim" }, { "o'qituvchi", "öqituvçi" }, { "o'quvchi", "öquvçi" }, { "ko'cha", "köça" },
        { "go'sht", "göşt" }, { "do'st", "döst" }, { "to'y", "töy" }, { "so'z", "söz" }, { "o'rik", "örik" },
        { "o'n", "ön" }, { "ko'p", "köp" }, { "yo'q", "yöq" }, { "bo'sh", "böş" }, { "qo'shni", "qöşni" },
        { "o'zbekcha", "özbekça" }, { "to'g'ri", "töğri" }, { "o'g'ri", "öğri" }, { "bo'g'iz", "böğiz" },
        { "g'oz", "ğoz" }, { "g'alaba", "ğalaba" }, { "g'arb", "ğarb" }, { "g'isht", "ğişt" }, { "g'ildirak", "ğildirak" },
        { "tog'", "toğ" }, { "bog'", "boğ" }, { "sog'", "soğ" }, { "yog'", "yoğ" }, { "bag'ir", "bağir" },
        { "tog'lar", "toğlar" }, { "bog'bon", "boğbon" }, { "sog'lik", "soğlik" }, { "yog'och", "yoğoç" },
        { "chiroq", "çiroq" }, { "choynak", "çoynak" }, { "chaqaloq", "çaqaloq" }, { "chumoli", "çumoli" },
        { "chiroyli", "çiroyli" }, { "kuch", "kuç" }, { "qilich", "qiliç" }, { "bolacha", "bolaça" }, { "ich", "iç" },
        { "chol", "çol" }, { "chap", "çap" }, { "uch", "uç" }, { "sichqon", "siçqon" }, { "qarshi", "qarşi" },
        { "shamol", "şamol" }, { "shaftoli", "şaftoli" }, { "shirin", "şirin" }, { "osh", "oş" }, { "ish", "iş" },
        { "qush", "quş" }, { "tosh", "toş" }, { "yosh", "yoş" }, { "bosh", "boş" }, { "kishi", "kişi" },
        { "Toshkent", "Toşkent" }, { "Samarqand", "Samarqand" }, { "Buxoro", "Buxoro" }, { "Shahrisabz", "Şahrisabz" },
        { "Chirchiq", "Çirçiq" }, { "Farg'ona", "Farğona" }, { "Qo'qon", "Qöqon" }, { "Xorazm", "Xorazm" },
        { "Andijon", "Andijon" }, { "Namangan", "Namangan" }, { "Jizzax", "Jizzax" }, { "Qashqadaryo", "Qaşqadaryo" },
        { "Surxondaryo", "Surxondaryo" }, { "Navoiy", "Navoiy" }, { "Guliston", "Guliston" }, { "Nukus", "Nukus" },
        { "ta'lim", "taʼlim" }, { "san'at", "sanʼat" }, { "e'lon", "eʼlon" }, { "ma'lumot", "maʼlumot" },
        { "e'tibor", "eʼtibor" }, { "jur'at", "jurʼat" }, { "ra'no", "raʼno" }, { "sur'at", "surʼat" },
        { "qal'a", "qalʼa" }, { "she'r", "şeʼr" }, { "Ma'rifat", "Maʼrifat" }, { "ish'ol", "işʼol" },
        { "tong", "tong" }, { "ming", "ming" }, { "keng", "keng" }, { "singil", "singil" }, { "dengiz", "dengiz" },
        { "rang", "rang" }, { "o'ng", "öng" }, { "ko'ngil", "köngil" }, { "bilan", "bilan" }, { "kitob", "kitob" },
        { "daftar", "daftar" }, { "maktab", "maktab" }, { "olma", "olma" }, { "non", "non" }, { "suv", "suv" },
        { "Shoshilinch", "Şoşilinç" }, { "chorshanba", "çorşanba" }, { "shanba", "şanba" }, { "chorva", "çorva" },
        { "o'shanda", "öşanda" }, { "g'ishtchi", "ğiştçi" }, { "cho'l", "çöl" }, { "sho'rva", "şörva" },
        { "cho'chqa", "çöçqa" }, { "sho'x", "şöx" }, { "qo'shiqchi", "qöşiqçi" }, { "o'quvchilar", "öquvçilar" },
        { "MAKTAB", "MAKTAB" }, { "TOSHKENT", "TOŞKENT" }, { "O'ZBEKISTON", "ÖZBEKISTON" }, { "CHO'L", "ÇÖL" },
    };

    [Theory]
    [MemberData(nameof(Words))]
    public void WordList(string oldForm, string newForm)
    {
        Assert.Equal(newForm, T(oldForm));
        Assert.Equal(newForm, T(newForm)); // yangi shakl o'zgarmaydi
    }

    [Fact]
    public void WordListHasHundredPlus() => Assert.True(Words.Count() >= 100);

    [Theory]
    [MemberData(nameof(Words))]
    public void WordsInSentenceWithEveryApostropheVariant(string oldForm, string newForm)
    {
        foreach (char a in "'`’‘ʻʼ´′＇")
        {
            string input = $"Bu {oldForm.Replace('\'', a)}, xolos.\r\n";
            Assert.Equal($"Bu {newForm}, xolos.\r\n", T(input));
        }
    }

    // Tasodifiy aralash matnlar: idempotentlik, qator oxirlari va himoyalangan qismlar saqlanishi
    [Fact]
    public void RandomMixedText()
    {
        string[] pieces =
        {
            "o'", "O'", "g'", "G'", "sh", "SH", "Sh", "ch", "Ch", "ng", "s'h", "ö", "Ğ", "ş", "Ç", "ʼ", "'", "`", "’",
            "‘", "ʻ", "´", "a", "o", "g", "s", "c", "h", "q", "x", "e", "i", " ", " ", "\r\n", "\n", "\t",
            ".", ",", "!", "-", "1", "kino", "bog'", "'tong'", "ma'no",
        };
        string[] protectedTokens = { "https://shop.uz/choy", "www.o'g'il.uz", "info@shahar.uz", "@shohruh", "#choyxona" };

        var rnd = new Random(20261021);
        for (int iter = 0; iter < 5000; iter++)
        {
            var parts = new List<string>();
            int len = rnd.Next(1, 30);
            for (int k = 0; k < len; k++)
            {
                if (rnd.Next(10) == 0)
                    parts.AddRange(new[] { " ", protectedTokens[rnd.Next(protectedTokens.Length)], " " });
                else
                    parts.Add(pieces[rnd.Next(pieces.Length)]);
            }
            string input = string.Concat(parts);
            string once = T(input);

            Assert.Equal(once, T(once));
            Assert.Equal(once, Transliterator.ToNewAlphabet(input, out var map));
            Assert.Equal(once.Length, map![input.Length]);
            for (int k = 1; k <= input.Length; k++) Assert.True(map[k] >= map[k - 1], "xarita kamaymasligi kerak");
            Assert.Equal(Count(input, "\r\n"), Count(once, "\r\n"));
            Assert.Equal(Count(input, "\n"), Count(once, "\n"));
            foreach (var tok in protectedTokens)
                if (input.Contains(" " + tok + " "))
                    Assert.Contains(" " + tok + " ", once);
        }
    }

    // ---- Pozitsiyalar xaritasi (stil oraliqlarini ko'chirish uchun) ----

    [Fact]
    public void MapDigraph()
    {
        string o = Transliterator.ToNewAlphabet("a sh b", out var map);
        Assert.Equal("a ş b", o);
        Assert.Equal(new[] { 0, 1, 2, 2, 3, 4, 5 }, map);
    }

    [Fact]
    public void MapStemInsertsTutuq()
    {
        string o = Transliterator.ToNewAlphabet("Ishoq", out var map);
        Assert.Equal("Isʼhoq", o);
        Assert.Equal(new[] { 0, 1, 3, 4, 5, 6 }, map);
    }

    [Theory]
    [InlineData("Salom o'zbek do'stlar", 6, 12, "özbek")]          // "o'zbek" qalin bo'lsa
    [InlineData("Choyxonada shirin choy", 11, 17, "şirin")]
    [InlineData("https://shop.uz va shahar", 19, 25, "şahar")]     // himoyalangan qismdan keyin
    [InlineData("Bog'da g'isht", 7, 13, "ğişt")]
    public void MapMovesStyleRange(string input, int from, int to, string expected)
    {
        string o = Transliterator.ToNewAlphabet(input, out var map);
        Assert.Equal(expected, o[map![from]..map[to]]);
    }

    [Fact]
    public void MapNullWhenNotNfc()
    {
        Assert.Equal("Özbek", Transliterator.ToNewAlphabet("Özbek", out var map));
        Assert.Null(map);
    }

    private static int Count(string s, string sub)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; }
        return n;
    }
}
