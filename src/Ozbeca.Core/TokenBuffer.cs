using System.Text;

namespace Ozbeca.Core;

/// <summary>
/// Yozish paytida avtomatik o'girish uchun: oxirgi probeldan keyin yozilgan "token"ni yig'adi.
/// Probel bosilganda butun token Transliterator'dan o'tadi — shuning uchun token ichidagi
/// nuqta, "/", "@", "#" signal emas va URL, email, @username buzilmaydi (Transliterator ularni himoya qiladi).
///
/// Kod rejimi (kod muharrirlari): faqat qo'shtirnoq "…" ichidagi matn o'giriladi. Qo'shtirnoq ichidami — buni
/// faqat o'zi ko'rgan tugmalar bo'yicha biladi: qator boshidan (Enter/Home) beri yozilganlarni kuzatadi.
/// Sichqoncha/strelka bilan qatorning o'rtasiga o'tilsa — bilmaydi va keyingi Enter'gacha hech narsani o'girmaydi
/// (aks holda catch, match, push kabi kalit so'zlar buzilardi).
///
/// Windows'ga bog'liq emas: qaysi belgilar yozilgani/o'chirilgani tashqaridan beriladi.
/// </summary>
public sealed class TokenBuffer
{
    /// <summary>Bundan uzun token o'girilmaydi (masalan, uzun URL yoki parolga o'xshash satr).</summary>
    public const int MaxLength = 64;

    private const int MaxLine = 512;

    private readonly StringBuilder _sb = new();   // joriy so'z
    private readonly StringBuilder _line = new(); // qator boshidan beri yozilgan (kod rejimi uchun)
    private bool _overflow;
    private bool _lineKnown;                      // qator boshidan beri hamma narsani ko'rdikmi
    private bool _inString;
    private bool _codeMode;

    public string Current => _sb.ToString();

    /// <summary>Kod muharriri: faqat qo'shtirnoq ichi o'giriladi. O'zgartirilsa — holat unutiladi.</summary>
    public bool CodeMode
    {
        get => _codeMode;
        set { if (_codeMode != value) { _codeMode = value; Reset(); } }
    }

    /// <summary>Kod rejimida kursor qo'shtirnoq ichida ekani aniq ma'lum.</summary>
    public bool InString => _codeMode && _lineKnown && _inString;

    /// <summary>Kontekst noma'lum bo'ldi (sichqoncha, strelka, boshqa oyna) — yig'ilganini unutamiz.</summary>
    public void Reset()
    {
        ClearWord();
        _line.Clear();
        _lineKnown = false;
        _inString = false;
    }

    /// <summary>Kursor qator boshida (Enter, Home) — kod rejimida qo'shtirnoqdan tashqarida ekanimiz aniq.</summary>
    public void ResetNewLine()
    {
        Reset();
        _lineKnown = true;
    }

    public void Backspace()
    {
        if (_sb.Length > 0) _sb.Length--;
        else _overflow = true; // undan oldingi matnni bilmaymiz — bu so'zni o'girmaymiz

        if (_line.Length == 0) { _lineKnown = false; return; }
        char removed = _line[_line.Length - 1];
        _line.Length--;
        if (_codeMode && removed == '"' && !IsEscaped(_line.Length)) _inString = !_inString;
    }

    /// <summary>Probel/Enter bo'lmagan, ko'rinadigan belgi.</summary>
    public void Add(char c)
    {
        AppendLine(c);
        if (_codeMode && c == '"' && !IsEscaped(_line.Length - 1))
        {
            _inString = !_inString;
            ClearWord(); // qo'shtirnoq — so'z chegarasi: print("choy → so'z "choy"
            return;
        }
        if (_overflow) return;
        if (_sb.Length >= MaxLength) { _overflow = true; return; }
        _sb.Append(c);
    }

    /// <summary>
    /// So'z tugadi (probel). O'zgarish bo'lsa: nechta belgini o'chirib, nimani yozish kerak.
    /// Probelning o'zi qatorga qo'shiladi; so'z bufferi tozalanadi.
    /// </summary>
    public bool TryComplete(out int deleteCount, out string replacement)
    {
        bool ok = TryConvertWord(out deleteCount, out replacement);
        AppendLine(' ');
        return ok;
    }

    /// <summary>
    /// Kod rejimi: qo'shtirnoq ichida yopuvchi " bosildi — undan oldingi so'zni ham o'giramiz.
    /// true bo'lsa chaqiruvchi so'zni almashtirib, keyin " ni o'zi yozadi (qo'shtirnoq bu yerda qayd etiladi).
    /// false bo'lsa — odatdagidek Add('"') chaqirilsin.
    /// </summary>
    public bool TryCompleteBeforeQuote(out int deleteCount, out string replacement)
    {
        deleteCount = 0;
        replacement = "";
        if (!InString || _line.Length > 0 && _line[_line.Length - 1] == '\\') return false;
        if (!TryConvertWord(out deleteCount, out replacement)) return false;
        Add('"');
        return true;
    }

    /// <summary>
    /// Enter bosildi ("Yaxshimisan?" + Enter → "Yaxşimisan?"). Oddiy matnda oxirgi so'z o'giriladi;
    /// kod rejimida Enter — oddiy yangi qator, o'girilmaydi. Har holda yangi qator boshlanadi.
    /// Parol maydoni emasligini chaqiruvchi tekshiradi (parol odatda Enter bilan tugaydi).
    /// </summary>
    public bool TryCompleteAtEnter(out int deleteCount, out string replacement)
    {
        deleteCount = 0;
        replacement = "";
        bool ok = !_codeMode && TryConvertWord(out deleteCount, out replacement);
        ResetNewLine();
        return ok;
    }

    private bool TryConvertWord(out int deleteCount, out string replacement)
    {
        string word = _sb.ToString();
        bool overflow = _overflow;
        ClearWord();
        deleteCount = 0;
        replacement = "";
        if (overflow || word.Length == 0) return false;
        if (_codeMode && !InString) return false;        // kod: faqat qo'shtirnoq ichi
        if (CodeGuard.LooksLikeCode(word)) return false; // kod yozilyapti — tegmaymiz

        string converted = Transliterator.ToNewAlphabet(word);
        if (converted == word) return false;
        deleteCount = word.Length;
        replacement = converted;

        // Qatordagi so'zni ham almashtiramiz — keyingi backspace'lar ekrandagi matn bilan mos kelsin.
        if (_line.Length >= deleteCount)
        {
            _line.Length -= deleteCount;
            foreach (char ch in converted) AppendLine(ch);
        }
        else _lineKnown = false;
        return true;
    }

    private void ClearWord()
    {
        _sb.Clear();
        _overflow = false;
    }

    private void AppendLine(char c)
    {
        if (_line.Length >= MaxLine) { _lineKnown = false; return; }
        _line.Append(c);
    }

    /// <summary>_line[index] oldidan toq sonli \ bo'lsa — escape qilingan.</summary>
    private bool IsEscaped(int index)
    {
        int n = 0;
        for (int i = index - 1; i >= 0 && _line[i] == '\\'; i--) n++;
        return n % 2 == 1;
    }
}
