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
    [InlineData("Please check the school chart", "Please check the school chart")]       // inglizcha so'zlar — tegilmaydi
    [InlineData("Check, should; (which)", "Check, should; (which)")]
    [InlineData("chat va match", "çat va matç")]                                         // o'zbekchada ham bor — o'giriladi
    [InlineData("checkni shoshma", "checkni şoşma")]                                     // h siz c — o'zbekcha emas
    [InlineData("That's why she'd say Chicago, Washington", "That's why she'd say Chicago, Washington")]
    [InlineData("Ta'm, so'm, o't, ra'd va ko's", "Taʼm, söm, öt, raʼd va kös")]
    [InlineData("U 'Yaxshi tong' dedi, bog' katta", "U 'Yaxşi tong' dedi, boğ katta")]  // tirnoq so'zdan so'zga o'tadi
    [InlineData("'Yaxshi\r\ntog' baland", "'Yaxşi\r\ntoğ baland")]                      // lekin qatordan qatorga emas
    [InlineData("", "")]
    public void ConvertProse(string input, string expected) => Assert.Equal(expected, CodeGuard.ConvertProse(input));

    [Theory]
    [InlineData("check", true)]
    [InlineData("Should,", true)]
    [InlineData("SCHOOL", true)]
    [InlineData("chat", false)]      // o'zbekcha çat
    [InlineData("match", false)]     // matç
    [InlineData("finish", false)]    // finiş
    [InlineData("shahar", false)]
    [InlineData("checkni", true)]    // h siz c — o'zbek lotinida yo'q
    [InlineData("schoolchildren", true)]
    [InlineData("doesn't", true)]
    [InlineData("they’re", true)]
    [InlineData("Obama's", true)]
    [InlineData("I'm", true)]
    [InlineData("so'm", false)]
    [InlineData("ta'm", false)]
    [InlineData("o't", false)]
    [InlineData("ra'd", false)]
    [InlineData("ko's", false)]
    [InlineData("chop", false)]      // o'zbekcha çop
    [InlineData("bosh", false)]
    [InlineData("Toshkent", false)]
    [InlineData("much", true)]
    [InlineData("high-tech", true)]
    [InlineData("T-shirt", true)]
    [InlineData("qo'llab-quvvatlash", false)]
    [InlineData("ship-shiydon", false)]
    [InlineData("O'Brien", true)]
    [InlineData("O'zbekiston", false)]
    [InlineData("O'ZBEKISTON", false)]
    [InlineData("", false)]
    public void EnglishWords(string token, bool expected)
    {
        Assert.Equal(expected, CodeGuard.IsEnglishWord(token));
        Assert.False(CodeGuard.LooksLikeCode(token), token); // inglizcha so'z kod emas — Telegram'da "kod bor" deb o'tkazib yuborilmasin
    }

    [Fact]
    public void TokenBufferSkipsCode()
    {
        var b = new TokenBuffer();
        foreach (char c in "shahar_choy") b.Add(c);
        Assert.False(b.TryComplete(out _, out _));
    }
}
