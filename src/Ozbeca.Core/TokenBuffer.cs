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

    private readonly StringBuilder _sb = new();   // joriy so'z — ekranda qanday turgan bo'lsa
    // Pauzada o'girilgan so'zning asl (user yozgan) ko'rinishi. Lug'at faqat eski harflarni taniydi:
    // pauzada "organish" → "organiş" bo'lsa, probelda lug'at "organish"ni tekshirishi kerak (→ "örganiş").
    private StringBuilder? _raw;
    private readonly StringBuilder _line = new(); // qator boshidan beri yozilgan (kod rejimi uchun)
    private bool _overflow;
    // Qaytarish (TryUndo): oxirgi probelda so'z o'girilgan bo'lsa — ekrandagi va user yozgan ko'rinishi.
    // Faqat keyingi tugmagacha yashaydi: darhol Backspace bosilsa so'z asliga qaytadi.
    private string? _undoFrom, _undoTo;
    private string? _undone;                      // hozirgina qaytarilgan so'z — probeli o'chirilsa bufferga qaytadi
    private string? _keep;                        // user o'girishni rad etgan so'z: o'zgarmaguncha qayta o'girilmaydi
    private bool _quoteOpen;                      // oldingi so'zlarda tirnoq ochilgan va yopilmagan ('Yaxshi tong')
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
        _quoteOpen = false;
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
        string? undone = _undone;
        ForgetUndo();
        if (_raw != null)
        {
            // Asl va ekrandagi oxirgi harf bir xil bo'lsa — ikkalasidan o'chadi; aks holda (ş ↔ sh) asl shaklni unutamiz.
            if (_sb.Length > 0 && _raw.Length > 0 && _raw[_raw.Length - 1] == _sb[_sb.Length - 1]) _raw.Length--;
            else _raw = null;
        }
        if (_sb.Length > 0) _sb.Length--;
        else if (undone != null && !_overflow)
        {
            // Qaytarilgan so'zdan keyingi probel o'chirildi — so'z yana qo'lda: xatosi tuzatilsa odatdagidek
            // o'giriladi, o'zgarmasa — tegilmaydi (user uni shu holicha xohladi).
            _sb.Append(undone);
            _keep = undone;
        }
        else _overflow = true; // undan oldingi matnni bilmaymiz — bu so'zni o'girmaymiz

        if (_line.Length == 0) { _lineKnown = false; return; }
        char removed = _line[_line.Length - 1];
        _line.Length--;
        if (_codeMode && removed == '"' && !IsEscaped(_line.Length)) _inString = !_inString;
    }

    /// <summary>Probel/Enter bo'lmagan, ko'rinadigan belgi.</summary>
    public void Add(char c)
    {
        ForgetUndo();
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
        _raw?.Append(c);
    }

    /// <summary>
    /// So'z tugadi (probel). O'zgarish bo'lsa: nechta belgini o'chirib, nimani yozish kerak.
    /// Probelning o'zi qatorga qo'shiladi; so'z bufferi tozalanadi.
    /// </summary>
    public bool TryComplete(out int deleteCount, out string replacement)
    {
        string screen = _sb.ToString();
        string typed = _raw?.ToString() ?? screen;
        bool overflow = _overflow;
        bool ok = TryConvertWord(out deleteCount, out replacement);
        AppendLine(' ');
        // Ekrandagi so'z user yozganidan farq qiladi (hozir yoki oldinroq pauzada o'girilgan) — qaytarish mumkin.
        string final = ok ? replacement : screen;
        if (!overflow && final != typed) { _undoFrom = final; _undoTo = typed; }
        return ok;
    }

    /// <summary>
    /// Backspace bosildi. Undan oldingi tugma so'zni o'girgan probel bo'lsa — o'girish qaytariladi:
    /// chaqiruvchi deleteCount ta belgini o'chirib, replacement ni (asl so'z + probel) yozadi va Backspace'ni yutadi.
    /// Qaytarilgan so'z o'sha ko'rinishida qayta o'girilmaydi. false — odatdagidek Backspace() chaqirilsin.
    /// </summary>
    public bool TryUndo(out int deleteCount, out string replacement)
    {
        deleteCount = 0;
        replacement = "";
        if (_undoFrom == null || _undoTo == null) return false;
        string from = _undoFrom, to = _undoTo;
        ClearWord();
        deleteCount = from.Length + 1; // so'z + probel
        replacement = to + " ";
        ReplaceLineEnd(deleteCount, replacement);
        _undone = to;
        return true;
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

    /// <summary>
    /// Yozish to'xtadi (pauza): so'z tugamagan bo'lishi mumkin — o'giramiz, lekin u bufferda (o'girilgan holda)
    /// qoladi: davomi yozilsa ("işla" + "sh") probelda butun so'z yana tekshiriladi. Idempotent — ikkinchi pauza
    /// hech narsa yubormaydi.
    /// </summary>
    public bool TryConvertAtPause(out int deleteCount, out string replacement)
    {
        deleteCount = 0;
        replacement = "";
        if (_sb.Length == 0) return false; // so'z yo'q (probeldan keyin) — holatga, jumladan qaytarish imkoniga tegmaymiz
        string screen = _sb.ToString();
        string? raw = _raw?.ToString();
        string? keep = _keep;
        bool overflow = _overflow;
        // Lug'atsiz: so'z hali tugamagan bo'lishi mumkin ("yoq" + "imli" — yo'q emas). Lug'at probelda ishlaydi.
        bool ok = TryConvertWord(out deleteCount, out replacement, dictionary: false);
        _sb.Append(ok ? replacement : screen);
        if (ok || raw != null) _raw = new StringBuilder(raw ?? screen);
        _overflow = overflow;
        _keep = keep;
        return ok;
    }

    private bool TryConvertWord(out int deleteCount, out string replacement, bool dictionary = true)
    {
        string screen = _sb.ToString();
        string word = _raw?.ToString() ?? screen; // lug'at asl yozilgan so'zni tekshiradi
        bool overflow = _overflow;
        bool kept = _keep != null && word == _keep; // user shu so'zning o'girilishini hozirgina qaytargan
        ClearWord();
        deleteCount = 0;
        replacement = "";
        if (overflow || kept || word.Length == 0) return false;
        if (_codeMode && !InString) return false;        // kod: faqat qo'shtirnoq ichi
        if (CodeGuard.LooksLikeCode(word)) return false; // kod yozilyapti — tegmaymiz

        // Oldingi so'zlarda ochilgan tirnoq: 'Yaxshi + tong' — oxirgi ' harf (ğ) emas, yopuvchi tirnoq.
        // Pauzada so'z hali tugamagan — tirnoq holatini o'zgartirmaymiz.
        bool quote = _quoteOpen;
        string converted = Transliterator.ToNewAlphabet(word, dictionary && Transliterator.RestoreApostrophes, ref quote);
        if (dictionary) _quoteOpen = quote;
        if (converted == screen) return false;
        deleteCount = screen.Length;
        replacement = converted;
        ReplaceLineEnd(deleteCount, converted);
        return true;
    }

    /// <summary>Qator oxiridagi matnni ham almashtiramiz — keyingi backspace'lar ekrandagi matn bilan mos kelsin.</summary>
    private void ReplaceLineEnd(int deleteCount, string text)
    {
        if (_line.Length >= deleteCount)
        {
            _line.Length -= deleteCount;
            foreach (char ch in text) AppendLine(ch);
        }
        else _lineKnown = false;
    }

    private void ClearWord()
    {
        _sb.Clear();
        _raw = null;
        _overflow = false;
        _keep = null;
        ForgetUndo();
    }

    private void ForgetUndo() => _undoFrom = _undoTo = _undone = null;

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
