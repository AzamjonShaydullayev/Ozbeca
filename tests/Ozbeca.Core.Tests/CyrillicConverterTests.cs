using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class CyrillicConverterTests
{
    private static string C(string s) => CyrillicConverter.ToNewLatin(s);

    // Asosiy harflar
    [Theory]
    [InlineData("Ўзбекистон", "Özbekiston")]
    [InlineData("шаҳар", "şahar")]
    [InlineData("Шаҳар", "Şahar")]
    [InlineData("ШАҲАР", "ŞAHAR")]
    [InlineData("ғалла", "ğalla")]
    [InlineData("Ғалаба", "Ğalaba")]
    [InlineData("Қўқон", "Qöqon")]
    [InlineData("ҚЎҚОН", "QÖQON")]
    [InlineData("Ўқитувчи", "Öqituvçi")]
    [InlineData("Чорсу", "Çorsu")]
    [InlineData("эшик", "eşik")]
    [InlineData("жўжа", "jöja")]
    [InlineData("хабар", "xabar")]
    [InlineData("ҳаво", "havo")]
    [InlineData("бой", "boy")]
    [InlineData("тил", "til")]
    [InlineData("мактаб", "maktab")]
    public void Letters(string input, string expected) => Assert.Equal(expected, C(input));

    // Qattiq belgi → tutuq, yumshoq belgi tushiriladi
    [Theory]
    [InlineData("маъно", "maʼno")]
    [InlineData("таъсир", "taʼsir")]
    [InlineData("шеър", "şeʼr")]
    [InlineData("мўъжиза", "möʼjiza")]
    [InlineData("объект", "obyekt")]
    [InlineData("съезд", "syezd")]
    [InlineData("пьеса", "pyesa")]
    [InlineData("батальон", "batalyon")]
    [InlineData("медаль", "medal")]
    [InlineData("Исҳоқ", "Isʼhoq")]
    [InlineData("асҳоб", "asʼhob")]
    public void SignsAndApostrophe(string input, string expected) => Assert.Equal(expected, C(input));

    // ye/e: so'z boshida va unlidan keyin "ye", undoshdan keyin "e"
    [Theory]
    [InlineData("ер", "yer")]
    [InlineData("Ер", "Yer")]
    [InlineData("ЕР", "YER")]
    [InlineData("поезд", "poyezd")]
    [InlineData("келди", "keldi")]
    [InlineData("Европа", "Yevropa")]
    [InlineData("шоир ер", "şoir yer")]
    public void LetterYe(string input, string expected) => Assert.Equal(expected, C(input));

    // s/ts: so'z boshida/undoshdan keyin "s", unlidan keyin "ts"
    [Theory]
    [InlineData("цирк", "sirk")]
    [InlineData("Цирк", "Sirk")]
    [InlineData("ЦИРК", "SIRK")]
    [InlineData("лицей", "litsey")]
    [InlineData("ЛИЦЕЙ", "LITSEY")]
    [InlineData("акция", "aksiya")]
    [InlineData("милиция", "militsiya")]
    public void LetterTs(string input, string expected) => Assert.Equal(expected, C(input));

    // Ko'p harfli chiqish: katta harf so'z hammasi katta bo'lsa — hammasi katta, aks holda faqat birinchisi
    [Theory]
    [InlineData("Юлдуз", "Yulduz")]
    [InlineData("ЮЛДУЗ", "YULDUZ")]
    [InlineData("юлдуз", "yulduz")]
    [InlineData("Ёшлар", "Yoşlar")]
    [InlineData("ЁШЛАР", "YOŞLAR")]
    [InlineData("Янги", "Yangi")]
    [InlineData("ЯНГИ", "YANGI")]
    [InlineData("Я", "Ya")]
    [InlineData("борщ", "borşç")]
    [InlineData("БОРЩ", "BORŞÇ")]
    [InlineData("дунё", "dunyo")]
    [InlineData("1-сентябр", "1-sentyabr")]
    public void MultiLetterCase(string input, string expected) => Assert.Equal(expected, C(input));

    // Aralash matn, tinish belgilari, raqamlar
    [Theory]
    [InlineData("Salom дўст", "Salom döst")]
    [InlineData("Тошкент шаҳри — пойтахт.", "Toşkent şahri — poytaxt.")]
    [InlineData("1-сентябр, 2026!", "1-sentyabr, 2026!")]
    [InlineData("«Ёшлик» (2026)", "«Yoşlik» (2026)")]
    [InlineData("Hello, world 123", "Hello, world 123")]
    [InlineData("Özbekiston", "Özbekiston")]
    [InlineData("", "")]
    public void MixedAndPunctuation(string input, string expected) => Assert.Equal(expected, C(input));

    [Fact]
    public void NullIsEmpty() => Assert.Equal(string.Empty, C(null!));

    // Hotkey'dagi tartib: avval eski lotin qismlari, keyin kirill (ConvertEngine).
    [Theory]
    [InlineData("Salom дўст, choy ichdik", "Salom döst, çoy içdik")]
    [InlineData("Исҳоқ va Ishoq", "Isʼhoq va Isʼhoq")]
    [InlineData("Тошкент shahri", "Toşkent şahri")]
    public void MixedWithOldLatin(string input, string expected) =>
        Assert.Equal(expected, C(Transliterator.ToNewAlphabet(input)));

    [Theory]
    [InlineData("салом", true)]
    [InlineData("Salom дўст", true)]
    [InlineData("Ў", true)]
    [InlineData("Salom", false)]
    [InlineData("Özbekiston şahar", false)]
    [InlineData("", false)]
    public void HasCyrillic(string input, bool expected) => Assert.Equal(expected, CyrillicConverter.HasCyrillic(input));

    [Fact]
    public void OutputHasNoCyrillic() =>
        Assert.False(CyrillicConverter.HasCyrillic(C("Ўзбекистон Республикаси пойтахти — Тошкент шаҳри, ҳаёт гўзал, маъно, щ, ц, ё, ю, я, ъ, ь")));
}
