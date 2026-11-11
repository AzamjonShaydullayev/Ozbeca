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
