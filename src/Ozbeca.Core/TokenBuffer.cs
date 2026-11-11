using System.Text;

namespace Ozbeca.Core;

/// <summary>
/// Yozish paytida avtomatik o'girish uchun: oxirgi probeldan keyin yozilgan "token"ni yig'adi.
/// Probel/Enter bosilganda butun token Transliterator'dan o'tadi — shuning uchun token ichidagi
/// nuqta, "/", "@", "#" signal emas va URL, email, @username buzilmaydi (Transliterator ularni himoya qiladi).
/// Windows'ga bog'liq emas: qaysi belgilar yozilgani/o'chirilgani tashqaridan beriladi.
/// </summary>
public sealed class TokenBuffer
{
    /// <summary>Bundan uzun token o'girilmaydi (masalan, uzun URL yoki parolga o'xshash satr).</summary>
    public const int MaxLength = 64;

    private readonly StringBuilder _sb = new();
    private bool _overflow;

    public string Current => _sb.ToString();

    /// <summary>Kontekst noma'lum bo'ldi (sichqoncha, strelka, boshqa oyna) — yig'ilganini unutamiz.</summary>
    public void Reset()
    {
        _sb.Clear();
        _overflow = false;
    }

    public void Backspace()
    {
        if (_sb.Length > 0) _sb.Length--;
        else _overflow = true; // undan oldingi matnni bilmaymiz — bu so'zni o'girmaymiz
    }

    /// <summary>Probel/Enter bo'lmagan, ko'rinadigan belgi.</summary>
    public void Add(char c)
    {
        if (_overflow) return;
        if (_sb.Length >= MaxLength) { _overflow = true; return; }
        _sb.Append(c);
    }

    /// <summary>
    /// So'z tugadi (probel yoki Enter). O'zgarish bo'lsa: nechta belgini o'chirib, nimani yozish kerak.
    /// Har holda buffer tozalanadi.
    /// </summary>
    public bool TryComplete(out int deleteCount, out string replacement)
    {
        string word = _sb.ToString();
        bool overflow = _overflow;
        Reset();
        deleteCount = 0;
        replacement = "";
        if (overflow || word.Length == 0) return false;

        string converted = Transliterator.ToNewAlphabet(word);
        if (converted == word) return false;
        deleteCount = word.Length;
        replacement = converted;
        return true;
    }
}
