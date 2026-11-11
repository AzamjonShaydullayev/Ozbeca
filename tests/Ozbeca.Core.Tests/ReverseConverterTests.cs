using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class ReverseConverterTests
{
    private static string R(string s) => ReverseConverter.ToOldLatin(s);

    [Theory]
    [InlineData("Özbekiston", "O'zbekiston")]
    [InlineData("özbek", "o'zbek")]
    [InlineData("ÖĞIL", "O'G'IL")]
    [InlineData("ĞALABA", "G'ALABA")]
    [InlineData("boğ", "bog'")]
    [InlineData("şahar", "shahar")]
    [InlineData("Şahar", "Shahar")]
    [InlineData("ŞAHAR", "SHAHAR")]
    [InlineData("çoy", "choy")]
    [InlineData("Çoy", "Choy")]
    [InlineData("ÇOY", "CHOY")]
    [InlineData("TOŞKENT", "TOSHKENT")]
    [InlineData("Ş", "Sh")]
    [InlineData("Ç va Ş", "Ch va Sh")]
    [InlineData("maʼno", "ma'no")]
    [InlineData("Isʼhoq", "Is'hoq")]
    [InlineData("qöşiq", "qo'shiq")]
    [InlineData("Çorşanba", "Chorshanba")]
    public void Letters(string input, string expected) => Assert.Equal(expected, R(input));

    // Vergulli ș/Ș (U+0219/U+0218) ham ş/Ş kabi
    [Theory]
    [InlineData("ișçi", "ishchi")]
    [InlineData("Șahar", "Shahar")]
    [InlineData("ȘAHAR", "SHAHAR")]
    public void CommaBelowS(string input, string expected) => Assert.Equal(expected, R(input));

    [Theory]
    [InlineData("Özbekiston şaharlari Çirçiq", "O'zbekiston shaharlari Chirchiq")]
    [InlineData("Salom, dunyo! 2026", "Salom, dunyo! 2026")]
    [InlineData("Bu 'kino' edi", "Bu 'kino' edi")]
    [InlineData("", "")]
    public void Sentences(string input, string expected) => Assert.Equal(expected, R(input));

    [Fact]
    public void NullIsEmpty() => Assert.Equal(string.Empty, R(null!));

    [Fact]
    public void OutputIsAscii() =>
        Assert.All(R("Özbekiston şaharlari Çirçiq, ĞALABA, maʼno, ŞAHAR"), c => Assert.True(c < 128, $"ASCII emas: {c}"));

    // Eski lotin → yangi → eski: tutuqli, ikki ma'noli bo'lmagan matnda o'zgarmasligi kerak.
    [Theory]
    [InlineData("O'zbekiston shaharlari")]
    [InlineData("Bog'da choy ichdik")]
    [InlineData("ma'no va ta'sir")]
    [InlineData("Toshkent - poytaxt.")]
    [InlineData("G'ALABA KUNI")]
    [InlineData("SHAHAR")]
    [InlineData("Chorshanba kuni qo'shiq aytdik")]
    [InlineData("O'g'il bola, g'isht, to'g'ri")]
    [InlineData("Is'hoq")]
    public void RoundTrip(string oldLatin) =>
        Assert.Equal(oldLatin, R(Transliterator.ToNewAlphabet(oldLatin)));
}
