using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class CodeGuardTests
{
    [Theory]
    [InlineData("user_name")]
    [InlineData("x=5")]
    [InlineData("print(choy)")]
    [InlineData("choy()")]
    [InlineData("shop.check")]
    [InlineData("check")]
    [InlineData("Should,")]
    [InlineData("shahar.uz")]
    [InlineData("std::cout")]
    [InlineData("showChart")]
    [InlineData("getChars();")]
    [InlineData("$shahar")]
    [InlineData("a<b")]
    [InlineData("C:\\choy")]
    [InlineData("list[i]")]
    [InlineData("{choy}")]          // ichki qavs ham
    [InlineData("a&&b")]
    [InlineData("va/yoki")]
    public void Code(string token) => Assert.True(CodeGuard.LooksLikeCode(token), token);

    [Theory]
    [InlineData("choy")]
    [InlineData("O'zbekiston")]
    [InlineData("SHAHAR")]
    [InlineData("(shahar)")]
    [InlineData("[kvadrat")]
    [InlineData("choy,")]
    [InlineData("shahar.")]
    [InlineData("bog':")]
    [InlineData("\"choy\"")]
    [InlineData("Salom!")]
    [InlineData("2026-yil")]
    [InlineData("10%")]
    [InlineData("o`rta")]
    [InlineData("Is'hoq")]
    [InlineData("«shahar»")]
    public void Prose(string token) => Assert.False(CodeGuard.LooksLikeCode(token), token);

    [Theory]
    [InlineData("Bugun choy ichdik", "Bugun çoy içdik")]
    [InlineData("choy  shahar\r\n\tqo'shiq", "çoy  şahar\r\n\tqöşiq")]                 // bo'shliqlar aynan
    [InlineData("var shahar_choy = getChoy(); // shirin choy", "var shahar_choy = getChoy(); // şirin çoy")]
    [InlineData("if (x) { choy(); }", "if (x) { choy(); }")]
    [InlineData("shahar.uz saytida choy", "shahar.uz saytida çoy")]
    [InlineData("", "")]
    public void ConvertProse(string input, string expected) => Assert.Equal(expected, CodeGuard.ConvertProse(input));

    [Fact]
    public void TokenBufferSkipsCode()
    {
        var b = new TokenBuffer();
        foreach (char c in "shahar_choy") b.Add(c);
        Assert.False(b.TryComplete(out _, out _));
    }
}
