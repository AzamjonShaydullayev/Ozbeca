using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class TokenBufferTests
{
    private static (bool ok, int del, string rep) Type(string word)
    {
        var b = new TokenBuffer();
        foreach (char c in word) b.Add(c);
        bool ok = b.TryComplete(out int del, out string rep);
        return (ok, del, rep);
    }

    [Theory]
    [InlineData("choy", 4, "çoy")]
    [InlineData("O'zbekiston", 11, "Özbekiston")]
    [InlineData("bog'", 4, "boğ")]
    [InlineData("shahar,", 7, "şahar,")]          // tinish belgisi token ichida qoladi
    [InlineData("Ishoq", 5, "Isʼhoq")]
    public void ConvertsWord(string typed, int del, string rep) => Assert.Equal((true, del, rep), Type(typed));

    [Theory]
    [InlineData("maktab")]
    [InlineData("https://shop.uz/choy")]           // URL buzilmaydi
    [InlineData("info@shahar.uz")]
    [InlineData("@shohruh")]
    [InlineData("#choyxona")]
    [InlineData("'kino'")]
    [InlineData("'tong'")]
    [InlineData("Özbekiston")]                     // allaqachon yangi alifboda
    [InlineData("")]
    public void LeavesAlone(string typed) => Assert.False(Type(typed).ok);

    [Fact]
    public void PauseConvertsAndWordContinues()
    {
        var b = new TokenBuffer();
        string screen = "";
        void Type(string s) { foreach (char c in s) { b.Add(c); screen += c; } }
        void Apply(int del, string rep) => screen = screen.Substring(0, screen.Length - del) + rep;

        Type("ishla");
        Assert.True(b.TryConvertAtPause(out int d, out string r));
        Apply(d, r);
        Assert.Equal("işla", screen);
        Assert.False(b.TryConvertAtPause(out _, out _)); // ikkinchi pauza — hech narsa yuborilmaydi

        Type("sh");                                       // so'z davom etdi: ajralgan "s"+"h" ham ş bo'ladi
        Assert.True(b.TryComplete(out d, out r));
        Apply(d, r);
        Assert.Equal("işlaş", screen);

        var c = new TokenBuffer();
        foreach (char ch in "maktab") c.Add(ch);
        Assert.False(c.TryConvertAtPause(out _, out _));
        Assert.Equal("maktab", c.Current);                // o'zgarmasa — so'z joyida qoladi
    }

    [Fact]
    public void BackspaceEditsWord()
    {
        var b = new TokenBuffer();
        foreach (char c in "choyx") b.Add(c);
        b.Backspace();
        Assert.True(b.TryComplete(out int del, out string rep));
        Assert.Equal((4, "çoy"), (del, rep));
    }

    [Fact]
    public void BackspacePastStartDisablesWord()
    {
        // So'z boshidan oldingi matn o'chirildi — ekrandagi so'zni bilmaymiz, tegmaymiz.
        var b = new TokenBuffer();
        b.Backspace();
        foreach (char c in "choy") b.Add(c);
        Assert.False(b.TryComplete(out _, out _));
    }

    [Fact]
    public void TooLongTokenIsIgnored()
    {
        var b = new TokenBuffer();
        foreach (char c in new string('s', TokenBuffer.MaxLength) + "h") b.Add(c);
        Assert.False(b.TryComplete(out _, out _));
    }

    // ---- Kod rejimi: faqat "…" ichi ----

    /// <summary>Ekranda nima bo'lishini simulyatsiya qiladi. '\b' — Backspace, '\n' — Enter (yangi qator).</summary>
    private static string Simulate(string typed, bool code, bool startAtLineStart = true)
    {
        var b = new TokenBuffer { CodeMode = code };
        if (startAtLineStart) b.ResetNewLine();
        var screen = new System.Text.StringBuilder();
        foreach (char c in typed)
        {
            int del; string rep;
            switch (c)
            {
                case ' ':
                    if (b.TryComplete(out del, out rep)) { screen.Length -= del; screen.Append(rep); }
                    screen.Append(' ');
                    break;
                case '\b':
                    b.Backspace();
                    if (screen.Length > 0) screen.Length--;
                    break;
                case '\n':
                    if (b.TryCompleteAtEnter(out del, out rep)) { screen.Length -= del; screen.Append(rep); }
                    screen.Append('\n');
                    break;
                case '"' when b.TryCompleteBeforeQuote(out del, out rep):
                    screen.Length -= del;
                    screen.Append(rep).Append('"');
                    break;
                default:
                    b.Add(c);
                    screen.Append(c);
                    break;
            }
        }
        return screen.ToString();
    }

    [Theory]
    [InlineData("print(\"Bugun choy ichdik\") ", "print(\"Bugun çoy içdik\") ")]                  // yopuvchi " oldidagi so'z ham
    [InlineData("const shahar = \"choy shirin\"; ", "const shahar = \"çoy şirin\"; ")]                // tashqaridagi shahar — o'zgarmaydi
    [InlineData("catch match push hash chunk ", "catch match push hash chunk ")]                    // kalit so'zlar
    [InlineData("s = \"a \\\"choy\\\" shirin\" ", "s = \"a \\\"choy\\\" şirin\" ")]                  // \" qo'shtirnoqni yopmaydi
    [InlineData("log(\"x\") choy ", "log(\"x\") choy ")]                                            // yopilgandan keyin — tashqari
    [InlineData("\"choy\nshahar ", "\"choy\nshahar ")]                                               // Enter'da o'girilmaydi; yangi qator — tashqari
    [InlineData("\"\bchoy shahar ", "choy shahar ")]                                                 // " backspace bilan o'chirildi
    [InlineData("msg = \"{name} choy_id shirin\" ", "msg = \"{name} choy_id şirin\" ")]              // string ichidagi kodga ham tegilmaydi
    public void CodeModeConvertsOnlyInsideStrings(string typed, string expected) => Assert.Equal(expected, Simulate(typed, code: true));

    [Fact]
    public void CodeModeUnknownPositionConvertsNothing()
    {
        // Sichqoncha bilan qatorning o'rtasiga o'tildi — qo'shtirnoq ichidami, bilmaymiz: hech narsa o'girilmaydi.
        Assert.Equal("x = \"choy shirin\" ", Simulate("x = \"choy shirin\" ", code: true, startAtLineStart: false));
        // Enter'dan keyin esa — ishlaydi.
        Assert.Equal("a \"choy\nb = \"çoy\" ", Simulate("a \"choy\nb = \"choy\" ", code: true, startAtLineStart: false));
    }

    [Theory]
    [InlineData("Yaxshimisan?\n", "Yaxşimisan?\n")]          // probelsiz Enter — oxirgi so'z ham o'giriladi
    [InlineData("salom choy\nshahar\n", "salom çoy\nşahar\n")]
    [InlineData("choy1990\n", "çoy1990\n")]                    // parol maydonini AutoType tekshiradi (UIA)
    [InlineData("https://shop.uz/choy\n", "https://shop.uz/choy\n")]
    [InlineData("maktab\n", "maktab\n")]
    public void EnterConvertsLastWordInProse(string typed, string expected) => Assert.Equal(expected, Simulate(typed, code: false));

    [Fact]
    public void EnterInCodeModeIsJustNewLine() =>
        Assert.Equal("x = \"choy\nshahar ", Simulate("x = \"choy\nshahar ", code: true));

    [Fact]
    public void ProseModeTreatsQuotesAsText() =>
        Assert.Equal("Bu \"çoy\" şirin ", Simulate("Bu \"choy\" shirin ", code: false));

    // ---- Backspace bilan qaytarish ----

    /// <summary>Ekran modeli: probel — so'z tugadi, '\b' — Backspace (AutoType kabi avval TryUndo).</summary>
    private sealed class Typist
    {
        public readonly TokenBuffer B = new();
        public string Screen = "";

        public Typist Type(string keys)
        {
            foreach (char c in keys)
            {
                if (c == ' ')
                {
                    if (B.TryComplete(out int del, out string rep)) Swap(del, rep);
                    Screen += ' ';
                }
                else if (c == '\b')
                {
                    if (B.TryUndo(out int del, out string rep)) Swap(del, rep);
                    else { B.Backspace(); Screen = Screen.Substring(0, Screen.Length - 1); }
                }
                else
                {
                    B.Add(c);
                    Screen += c;
                    if (Eager && B.TryRestoreTyped(out int del, out string rep)) Swap(del, rep);
                }
            }
            return this;
        }

        /// <summary>false — AutoType qaytarishni keyingi harfga qoldirgan holat (Shift bosilgan, dead key).</summary>
        public bool Eager = true;

        public Typist Pause()
        {
            if (B.TryConvertAtPause(out int del, out string rep)) Swap(del, rep);
            return this;
        }

        private void Swap(int del, string rep) => Screen = Screen.Substring(0, Screen.Length - del) + rep;
    }

    [Theory]
    [InlineData("chass \b", "chass ")]                 // o'girildi (çass) → Backspace → asliga qaytdi, probel joyida
    [InlineData("ozbek \b", "ozbek ")]                 // lug'at tuzatishi ham qaytadi
    [InlineData("Men chass \b", "Men chass ")]
    [InlineData("chass \b\b", "chass")]                // ikkinchi Backspace — odatdagidek probelni o'chiradi
    [InlineData("chass \b\b ", "chass ")]              // qaytarilgan so'z qayta o'girilmaydi
    [InlineData("chass \bqildim ", "chass qildim ")]   // keyingi so'zlar odatdagidek
    [InlineData("chass \bchoy ", "chass çoy ")]
    [InlineData("shahr \b\b\bar ", "şahar ")]          // qaytarib, xatosi tuzatilgan so'z — o'giriladi
    [InlineData("chass \b\b\bs ", "chass ")]          // o'chirib, o'sha harfni qayta yozdi — hamon rad etilgan so'z
    [InlineData("maktab \b", "maktab")]                // o'girilmagan so'z — oddiy Backspace
    [InlineData("choy n\b\b", "çoy")]                  // orada boshqa tugma bosildi — qaytarish yo'q
    [InlineData("choy \b\b\b ", "ço ")]                // qaytarilgan so'z qisqartirildi — endi boshqa so'z, o'giriladi
    public void BackspaceUndoesConversion(string keys, string expected) => Assert.Equal(expected, new Typist().Type(keys).Screen);

    // Qaytarilgan so'z shu holicha qoldirildi — ilova sanaydi, ikkinchi safar istisnoga qo'shadi.
    [Theory]
    [InlineData("choy \bv", "choy")]            // qaytardi va keyingi so'zni yozdi
    [InlineData("choy \b\b ", "choy")]          // probelni o'chirib, o'zgartirmasdan tugatdi
    [InlineData("osh \b\bxona ", null)]         // davom ettirdi (oshxona) — rad etish emas
    [InlineData("choy v", null)]                // qaytarmadi
    [InlineData("choy \b\b\b ", null)]          // qisqartirdi — boshqa so'z
    public void RejectedWord(string keys, string? expected)
    {
        var t = new Typist();
        string? got = null;
        foreach (char c in keys) { t.Type(c.ToString()); got ??= t.B.TakeRejected(); }
        Assert.Equal(expected, got);
        Assert.Null(t.B.TakeRejected()); // bir marta
    }

    // Userning o'girilmaydigan so'zlari (statik holat — boshqa testlarda shu so'zlar yo'q).
    [Fact]
    public void UserExceptions()
    {
        try
        {
            Transliterator.SetExceptions(new[] { "Shohjahon", " chorsu ", "", "Chevrolet" });
            Assert.Equal("Shohjahon bilan Chorsu'da çoy", Transliterator.ToNewAlphabet("Shohjahon bilan Chorsu'da choy"));
            Assert.Equal("CHEVROLET şahar", Transliterator.ToNewAlphabet("CHEVROLET shahar"));
            Assert.Equal("Şohjahonov", Transliterator.ToNewAlphabet("Shohjahonov")); // butun so'z emas — o'giriladi
            Assert.Equal("Shohjahon ", new Typist().Type("Shohjahon ").Screen);
            Assert.Equal("Shohjahon ", new Typist().Type("Shoh").Pause().Type("jahon ").Screen); // pauzada o'girilgani qaytadi
        }
        finally { Transliterator.SetExceptions(null); }
        Assert.Equal("Şohjahon", Transliterator.ToNewAlphabet("Shohjahon"));
    }

    // Yozish paytida: tirnoq bir so'zda ochilib, boshqasida yopiladi ('Yaxshi tong' → tonğ bo'lib qolardi).
    [Theory]
    [InlineData("'Yaxshi tong' ", "'Yaxşi tong' ")]
    [InlineData("U 'Yaxshi tong' dedi ", "U 'Yaxşi tong' dedi ")]
    [InlineData("'Yaxshi tong' bog' ", "'Yaxşi tong' boğ ")]     // tirnoq yopilgach g' yana harf
    [InlineData("'tong' bog' ", "'tong' boğ ")]
    [InlineData("'salom' tog' ", "'salom' toğ ")]
    [InlineData("bog' tong' ", "boğ tonğ ")]                     // tirnoq ochilmagan
    public void QuotedPhraseWhileTyping(string keys, string expected) => Assert.Equal(expected, new Typist().Type(keys).Screen);

    [Fact]
    public void QuoteStateSurvivesPauseAndIsForgottenOnReset()
    {
        // Pauza tirnoq holatini o'zgartirmaydi: tong' pauzada ham, probelda ham tirnoq bo'lib qoladi.
        Assert.Equal("'Yaxşi tong' ", new Typist().Type("'Yaxshi tong'").Pause().Type(" ").Screen);
        // Sichqoncha / boshqa oyna — oldingi matnni bilmaymiz: g' yana harf.
        var t = new Typist().Type("'Yaxshi ");
        t.B.Reset();
        Assert.Equal("'Yaxşi tonğ ", t.Type("tong' ").Screen);
    }

    // Pauzada o'girilgan so'z davomi bilan manzil yoki kodga aylansa — yozilganidek qaytadi
    // (user xabari, 2026-11-11: şerzod + pauza + @gmail.com → şerzod@gmail.com bo'lib qolardi).
    [Theory]
    [InlineData("sherzod", "@gmail.com ", "sherzod@gmail.com ")]
    [InlineData("sherzod", "@", "sherzod@")]                       // @ yozilgan zahoti
    [InlineData("shop", ".uz ", "shop.uz ")]
    [InlineData("shop", ".", "şop.")]                              // hali gap oxiri bo'lishi mumkin
    [InlineData("sherzod", "_99 ", "sherzod_99 ")]
    [InlineData("show", "Chart() ", "showChart() ")]
    [InlineData("https", "://shop.uz/choy ", "https://shop.uz/choy ")]
    [InlineData("ishla", "sh ", "işlaş ")]                         // oddiy so'z davomi — avvalgidek
    [InlineData("Shahar", ", ", "Şahar, ")]
    [InlineData("organish", " ", "örganiş ")]
    public void PauseConvertedWordThatBecomesAddressIsRestored(string first, string rest, string expected)
    {
        Assert.Equal(expected, new Typist().Type(first).Pause().Type(rest).Screen);
        // AutoType qaytarishni kechiktirsa ham (Shift bosilgan) — probelda yoki keyingi pauzada to'g'rilanadi.
        var late = new Typist { Eager = false };
        late.Type(first).Pause().Type(rest);
        Assert.Equal(expected, late.Pause().Screen);
    }

    // Inglizcha so'zlar yozish paytida o'girilmaydi; pauzada boshi o'girilib ulgurgan bo'lsa — qaytadi.
    [Theory]
    [InlineData("check ", "check ")]
    [InlineData("Should, ", "Should, ")]
    [InlineData("I should check the school ", "I should check the school ")]
    [InlineData("chat ", "çat ")]                      // o'zbekchada ham shunday yoziladi
    [InlineData("checkni ", "checkni ")]
    public void EnglishWordsAreNotConvertedWhileTyping(string keys, string expected) =>
        Assert.Equal(expected, new Typist().Type(keys).Screen);

    [Fact]
    public void EnglishWordStartedBeforePauseIsRestored()
    {
        Assert.Equal("check", new Typist().Type("check").Pause().Screen);            // to'liq so'z pauzada ham tegilmaydi
        Assert.Equal("çe", new Typist().Type("che").Pause().Screen);                 // hali noma'lum so'z
        Assert.Equal("check ", new Typist().Type("che").Pause().Type("ck ").Screen); // tugagach — yozilganidek
    }

    [Fact]
    public void RestoredAddressStaysAsTyped()
    {
        var t = new Typist().Type("sherzod").Pause().Type("@gmail.com").Pause();
        Assert.Equal("sherzod@gmail.com", t.Screen);
        Assert.Equal("sherzod@gmail.com ", t.Type(" ").Screen);
        Assert.Equal("sherzod@gmail.com", t.Type("\b").Screen); // qaytaradigan narsa yo'q — oddiy Backspace
        // Manzil o'chirilib, oddiy so'zga aylansa — yana o'giriladi.
        Assert.Equal("şerzod ", new Typist().Type("sherzod").Pause().Type("@\b ").Screen);
    }

    [Fact]
    public void UndoRestoresWhatWasTypedBeforePause()
    {
        // Sekin yozuvchi: pauzada "organiş", probelda lug'at bilan "örganiş" — Backspace user yozganini qaytaradi.
        var t = new Typist().Type("organish").Pause();
        Assert.Equal("organiş", t.Screen);
        Assert.Equal("örganiş ", t.Type(" ").Screen);
        Assert.Equal("organish ", t.Type("\b").Screen);

        // Pauzada o'girilib, probelda o'zgarmagan so'z ham qaytadi.
        t = new Typist().Type("chass").Pause().Type(" ");
        Assert.Equal("çass ", t.Screen);
        Assert.Equal("chass ", t.Type("\b").Screen);
    }

    [Fact]
    public void PauseDoesNotBreakUndo()
    {
        // Probeldan keyin pauza taymeri ishlasa ham qaytarish imkoni qoladi.
        Assert.Equal("chass ", new Typist().Type("chass ").Pause().Type("\b").Screen);
        // Qaytarilgan so'z pauzada ham qayta o'girilmaydi; o'zgartirilsa — o'giriladi.
        var t = new Typist().Type("chass \b\b").Pause();
        Assert.Equal("chass", t.Screen);
        Assert.Equal("çasss", t.Type("s").Pause().Screen);
    }

    [Fact]
    public void UndoIsForgottenOnReset()
    {
        var t = new Typist().Type("chass ");
        t.B.Reset(); // sichqoncha, boshqa oyna
        Assert.False(t.B.TryUndo(out _, out _));
    }

    [Fact]
    public void UndoWorksInsideCodeString()
    {
        var t = new Typist();
        t.B.CodeMode = true;
        t.B.ResetNewLine();
        Assert.Equal("x = \"çoy ", t.Type("x = \"choy ").Screen);
        Assert.Equal("x = \"choy ", t.Type("\b").Screen);
        Assert.True(t.B.InString);
    }

    [Fact]
    public void ResetForgetsAndCompleteClears()
    {
        var b = new TokenBuffer();
        foreach (char c in "choy") b.Add(c);
        b.Reset();
        foreach (char c in "non") b.Add(c);
        Assert.False(b.TryComplete(out _, out _));
        Assert.Equal("", b.Current);
    }
}
