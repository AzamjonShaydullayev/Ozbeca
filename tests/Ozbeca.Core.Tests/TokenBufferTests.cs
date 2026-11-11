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
                    b.ResetNewLine();
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

    [Fact]
    public void ProseModeTreatsQuotesAsText() =>
        Assert.Equal("Bu \"çoy\" şirin ", Simulate("Bu \"choy\" shirin ", code: false));

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
