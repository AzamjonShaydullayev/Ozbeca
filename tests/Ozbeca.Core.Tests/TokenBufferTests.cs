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
                else { B.Add(c); Screen += c; }
            }
            return this;
        }

        public Typist Pause()
        {
            if (B.TryConvertAtPause(out int del, out string rep)) Swap(del, rep);
            return this;
        }

        private void Swap(int del, string rep) => Screen = Screen.Substring(0, Screen.Length - del) + rep;
    }

    [Theory]
    [InlineData("check \b", "check ")]                 // o'girildi (çeck) → Backspace → asliga qaytdi, probel joyida
    [InlineData("ozbek \b", "ozbek ")]                 // lug'at tuzatishi ham qaytadi
    [InlineData("Men check \b", "Men check ")]
    [InlineData("check \b\b", "check")]                // ikkinchi Backspace — odatdagidek probelni o'chiradi
    [InlineData("check \b\b ", "check ")]              // qaytarilgan so'z qayta o'girilmaydi
    [InlineData("check \bqildim ", "check qildim ")]   // keyingi so'zlar odatdagidek
    [InlineData("check \bchoy ", "check çoy ")]
    [InlineData("shahr \b\b\bar ", "şahar ")]          // qaytarib, xatosi tuzatilgan so'z — o'giriladi
    [InlineData("check \b\b\bk ", "check ")]           // o'chirib, o'sha harfni qayta yozdi — hamon rad etilgan so'z
    [InlineData("maktab \b", "maktab")]                // o'girilmagan so'z — oddiy Backspace
    [InlineData("choy n\b\b", "çoy")]                  // orada boshqa tugma bosildi — qaytarish yo'q
    [InlineData("choy \b\b\b ", "ço ")]                // qaytarilgan so'z qisqartirildi — endi boshqa so'z, o'giriladi
    public void BackspaceUndoesConversion(string keys, string expected) => Assert.Equal(expected, new Typist().Type(keys).Screen);

    [Fact]
    public void UndoRestoresWhatWasTypedBeforePause()
    {
        // Sekin yozuvchi: pauzada "organiş", probelda lug'at bilan "örganiş" — Backspace user yozganini qaytaradi.
        var t = new Typist().Type("organish").Pause();
        Assert.Equal("organiş", t.Screen);
        Assert.Equal("örganiş ", t.Type(" ").Screen);
        Assert.Equal("organish ", t.Type("\b").Screen);

        // Pauzada o'girilib, probelda o'zgarmagan so'z ham qaytadi.
        t = new Typist().Type("check").Pause().Type(" ");
        Assert.Equal("çeck ", t.Screen);
        Assert.Equal("check ", t.Type("\b").Screen);
    }

    [Fact]
    public void PauseDoesNotBreakUndo()
    {
        // Probeldan keyin pauza taymeri ishlasa ham qaytarish imkoni qoladi.
        Assert.Equal("check ", new Typist().Type("check ").Pause().Type("\b").Screen);
        // Qaytarilgan so'z pauzada ham qayta o'girilmaydi; o'zgartirilsa — o'giriladi.
        var t = new Typist().Type("check \b\b").Pause();
        Assert.Equal("check", t.Screen);
        Assert.Equal("çecks", t.Type("s").Pause().Screen);
    }

    [Fact]
    public void UndoIsForgottenOnReset()
    {
        var t = new Typist().Type("check ");
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
