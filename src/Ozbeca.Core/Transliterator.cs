using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Ozbeca.Core;

/// <summary>Eski/aralash o'zbek lotin matnini 2026-yilgi yangi alifboga o'giradi.</summary>
public static class Transliterator
{
    /// <summary>Rasmiy tutuq belgisi U+02BC — O'RQ-1184 (21.10.2026) 1-modda jadvalidan tasdiqlangan (lex.uz/docs/8548754).</summary>
    public const char Tutuq = 'ʼ';

    /// <summary>
    /// Tutuq/harf belgisi deb tanilaladigan variantlar (A to'plami). Tutuq o'zi ham kiradi — idempotentlik uchun.
    /// ' ` ’ ‘ ʻ ʼ ´ ′ ＇
    /// </summary>
    private const string Apostrophes = "'`’‘ʻʼ´′＇";

    /// <summary>
    /// Tutuqsiz yozilgan, lekin s va h alohida tovush bo'lgan so'z o'zaklari (1995 imlo qoidalari, 17-band: Isʼhoq, asʼhob).
    /// So'z boshida kelsa s va h orasiga tutuq qo'yiladi; qo'shimchalar saqlanadi (Ishoqov → Isʼhoqov).
    /// </summary>
    private static readonly string[] SHStems = { "ishoq", "ashob" };

    // Tegilmaydigan qismlar: URL, email, https'siz havola (t.me/shahar, kun.uz/choy), @username, #hashtag.
    // Email @username'dan va domendan oldin turishi shart. Domen — faqat tanish zona bilan: "tugadi.Keyin" havola emas.
    private static readonly Regex Protected = new(
        @"(?:https?://|www\.)\S+|[\w.+-]+@[\w-]+(?:\.[\w-]+)+" +
        @"|(?<![\w.@-])(?:[\w-]+\.)+(?:uz|com|org|net|ru|me|io|info|biz|edu|gov|tv|app|dev|ai|co|kz|tj|kg|tm|tr|uk|us|de|be|ly)(?![\w-])(?:[/?#]\S*)?" +
        @"|[@#]\w+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Tutuqsiz yozilgan so'zlarni lug'at bo'yicha tuzatish (ozbek → özbek, togri → toğri). Sozlamalardan o'chiriladi.
    /// Faqat bir ma'noli so'zlar — WordList.
    /// </summary>
    public static bool RestoreApostrophes { get; set; } = true;

    public static string ToNewAlphabet(string input) => Convert(input, null, RestoreApostrophes);

    /// <param name="dictionary">false — lug'atsiz (masalan, pauzada: so'z hali tugamagan, "yoq|imli").</param>
    public static string ToNewAlphabet(string input, bool dictionary) => Convert(input, null, dictionary);

    /// <summary>
    /// Matn bo'laklab (so'zma-so'z) o'girilganda: quoteOpen — bu bo'lakdan oldin ochilgan, hali yopilmagan
    /// tirnoq bor ('Yaxshi | tong'). Chiqishda — bo'lak oxiridagi holat, keyingi bo'lakka beriladi.
    /// </summary>
    public static string ToNewAlphabet(string input, bool dictionary, ref bool quoteOpen) =>
        Convert(input, null, dictionary, ref quoteOpen);

    /// <summary>
    /// O'giradi va pozitsiyalar xaritasini qaytaradi: map[i] — kirishdagi i-belgi chiqishda qayerdan boshlanadi,
    /// map[input.Length] — chiqish uzunligi. Stil (qalin, emoji) oraliqlarini yangi matnga ko'chirish uchun.
    /// Kirish NFC bo'lmasa (ajralgan belgilar) xarita null — chaqiruvchi formatsiz yo'lga o'tadi.
    /// </summary>
    public static string ToNewAlphabet(string input, out int[]? map)
    {
        input ??= string.Empty;
        if (!input.IsNormalized(NormalizationForm.FormC)) { map = null; return Convert(input, null, RestoreApostrophes); }
        map = new int[input.Length + 1];
        return Convert(input, map, RestoreApostrophes);
    }

    private static string Convert(string input, int[]? map, bool dictionary)
    {
        bool quoteOpen = false;
        return Convert(input, map, dictionary, ref quoteOpen);
    }

    private static string Convert(string input, int[]? map, bool dictionary, ref bool quoteOpen)
    {
        if (string.IsNullOrEmpty(input))
        {
            if (map != null) map[0] = 0;
            return input ?? string.Empty;
        }

        string text = input.Normalize(NormalizationForm.FormC);
        HashSet<int>? closing = ClosingQuotes(text, ref quoteOpen);
        var sb = new StringBuilder(text.Length);
        int pos = 0;
        foreach (Match m in Protected.Matches(text))
        {
            ConvertSegment(text, pos, m.Index, sb, map, dictionary, closing);
            for (int k = 0; k < m.Length && map != null; k++) map[m.Index + k] = sb.Length + k;
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        ConvertSegment(text, pos, text.Length, sb, map, dictionary, closing);
        if (map != null) map[text.Length] = sb.Length;
        return sb.ToString();
    }

    /// <summary>
    /// So'z oxiridagi g' ikki ma'noli: ğ harfi (bog') yoki g + yopuvchi tirnoq ('Yaxshi tong'). Tirnoq deb olinadi,
    /// agar undan oldin shu qatorda tirnoq ochilgan (oldida harf yo'q, keyin harf: 'Yaxshi) va yopilmagan bo'lsa:
    /// - "ng'" — darhol yopuvchi (o'zbekchada ngʻ bilan tugaydigan so'z yo'q: tong, rang, ming);
    /// - boshqa g' (tog', bog') — keyinroq aniq yopuvchi tirnoq kelmasagina ('Tog' baland' — g' harf, tirnoq oxirida);
    ///   bir nechta bo'lsa — oxirgisi tirnoq.
    /// Qaytadi: yopuvchi tirnoq deb topilgan g' dagi tirnoq indekslari. Harfga qo'shilib ketadigan tutuqlar (o' → ö,
    /// g' → ğ) tirnoq hisoblanmaydi va o'rnida harf turgandek ko'riladi — natija ustidan qayta o'tilsa ham shu chiqadi.
    /// </summary>
    private static HashSet<int>? ClosingQuotes(string s, ref bool open)
    {
        HashSet<int>? closing = null;
        int candidate = -1;    // ochiq tirnoq ichidagi oxirgi ikki ma'noli g'
        bool prevGone = false; // oldingi belgi — harfga qo'shilib ketgan tutuq

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool gone = prevGone;
            prevGone = false;
            if (c == '\n') { Resolve(ref open, ref candidate, ref closing); open = false; continue; } // tirnoq qatordan qatorga o'tmaydi
            if (!IsA(c)) continue;
            char prev = i > 0 ? s[i - 1] : '\0', next = i + 1 < s.Length ? s[i + 1] : '\0';
            bool letterAfter = IsLetter(next), wordBefore = gone || IsLetter(prev) || char.IsDigit(prev);

            if (prev is 'o' or 'O')
            {
                char p = i > 1 ? s[i - 2] : '\0';
                if (letterAfter || !IsLetter(p) && !IsA(p)) { prevGone = true; continue; } // 2-qoida: ö
            }
            if (prev is 'g' or 'G')
            {
                if (!letterAfter && open && !IsA(next))
                {
                    if (i > 1 && s[i - 2] is 'n' or 'N')
                    {
                        (closing ??= new HashSet<int>()).Add(i); // tong' — yopuvchi; oldingi g' lar harf
                        open = false;
                        candidate = -1;
                    }
                    else candidate = i;
                }
                else prevGone = true; // 3-qoida: ğ
                continue;
            }

            if (open && prev is '.' or ',' or '!' or '?' or '…')
            {
                open = false; // 'Yaxshi tong!' — tinish belgisidan keyingi tirnoq yopuvchi; oldingi g' lar harf
                candidate = -1;
            }
            else if (!wordBefore && !IsA(prev) && letterAfter)
            {
                Resolve(ref open, ref candidate, ref closing); // 'tog' 'salom' — oldingisi shu yerda yopilgan
                open = true;
            }
            else if (open && wordBefore && !letterAfter && !IsA(next))
            {
                open = false; // aniq yopuvchi ('kino') — oldingi g' lar harf
                candidate = -1;
            }
        }
        Resolve(ref open, ref candidate, ref closing);
        return closing;
    }

    /// <summary>Ochiq tirnoq yopilmay qoldi: oxirgi ikki ma'noli g' — o'sha yopuvchi tirnoq.</summary>
    private static void Resolve(ref bool open, ref int candidate, ref HashSet<int>? closing)
    {
        if (open && candidate >= 0)
        {
            (closing ??= new HashSet<int>()).Add(candidate);
            open = false;
        }
        candidate = -1;
    }

    private static bool IsA(char c) => Apostrophes.IndexOf(c) >= 0;

    // ʻ (U+02BB) va ʼ (U+02BC) Unicode'da "modifier letter" — ularni harf deb hisoblamaymiz.
    private static bool IsLetter(char c) => char.IsLetter(c) && !IsA(c);

    private static void ConvertSegment(string s, int start, int end, StringBuilder sb, int[]? map, bool dictionary, HashSet<int>? closing)
    {
        int segOut = sb.Length;
        char At(int k) => k >= start && k < end ? s[k] : '\0';
        HashSet<int>? missing = dictionary ? MissingApostrophes(s, start, end) : null;

        int i = start;
        while (i < end)
        {
            // Xarita: bir necha belgi bitta belgiga aylansa (sh → ş), hammasi o'sha belgining boshiga.
            if (map != null) map[i] = sb.Length;
            if (map != null && i + 1 < end) map[i + 1] = sb.Length;

            char c = Confusable(s[i]);
            char n1 = Confusable(At(i + 1)), n2 = At(i + 2);

            // Tutuqsiz Ishoq/ashob — sh qoidasidan oldin, faqat so'z boshida.
            if ((c == 'i' || c == 'I' || c == 'a' || c == 'A') && (i == start || !IsLetter(s[i - 1])) && MatchStem(s, i, end) is int len)
            {
                for (int k = 0; k < len && map != null; k++) map[i + k] = sb.Length + (k < 2 ? k : k + 1);
                sb.Append(s, i, 2).Append(Tutuq).Append(s, i + 2, len - 2);
                i += len;
                continue;
            }

            // 1a. Lug'at: tutuqsiz yozilgan o/g (ozbek, togri) — bitta belgi bitta belgiga, xarita o'zgarmaydi.
            if (missing != null && missing.Contains(i))
            {
                sb.Append(c switch { 'o' => 'ö', 'O' => 'Ö', 'g' => 'ğ', _ => 'Ğ' });
                i++;
                continue;
            }

            // 2. o + A → ö, A dan keyin harf kelsa — yoki yakka o' (oldida harf ham, tirnoq ham yo'q: "o' harfi").
            //    So'z oxiridagi o' ('kino') yopuvchi tirnoq bo'lib qoladi.
            char p = At(i - 1);
            if ((c == 'o' || c == 'O') && IsA(n1) && (IsLetter(n2) || !IsLetter(p) && !IsA(p)))
            {
                sb.Append(c == 'o' ? 'ö' : 'Ö');
                i += 2;
                continue;
            }

            // 3. g + A → ğ, so'z oxirida ham — lekin undan oldin tirnoq ochilgan bo'lsa ('tong', 'Yaxshi tong'),
            //    oxiridagi A yopuvchi tirnoq deb olinadi (user qarori, 2026-11-10; ibora uchun — ClosingQuotes).
            if ((c == 'g' || c == 'G') && IsA(n1) && (IsLetter(n2) || closing == null || !closing.Contains(i + 1)))
            {
                sb.Append(c == 'g' ? 'ğ' : 'Ğ');
                i += 2;
                continue;
            }

            // 5. sh → ş, ch → ç. Bosh harf birinchi harf bo'yicha.
            if ((c is 's' or 'S' or 'c' or 'C') && (n1 == 'h' || n1 == 'H'))
            {
                sb.Append(c switch { 's' => 'ş', 'S' => 'Ş', 'c' => 'ç', _ => 'Ç' });
                i += 2;
                continue;
            }

            // 4. Harf + A + harf → rasmiy tutuq. Chap tomon chiqishdan olinadi (ö/ğ dan keyin ham ishlaydi).
            if (IsA(c) && sb.Length > segOut && IsLetter(sb[sb.Length - 1]) && IsLetter(n1))
            {
                sb.Append(Tutuq);
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }
    }

    /// <summary>
    /// Segmentdagi so'zlarni lug'atdan qidiradi va tutuqi tushib qolgan o/g indekslarini qaytaradi
    /// (Ozbekiston → {0}, to'gri → {2}). So'z = harf va tutuqlar ketma-ketligi; chetdagi tirnoqlar olinmaydi.
    /// </summary>
    private static HashSet<int>? MissingApostrophes(string s, int start, int end)
    {
        HashSet<int>? result = null;
        int i = start;
        while (i < end)
        {
            if (!IsLetter(s[i]) && !IsA(s[i])) { i++; continue; }
            int a = i;
            while (i < end && (IsLetter(s[i]) || IsA(s[i]))) i++;
            int b = i;
            while (a < b && IsA(s[a])) a++;                                              // ochuvchi tirnoq
            while (b > a && IsA(s[b - 1]) && !(b - 2 >= a && (s[b - 2] == 'g' || s[b - 2] == 'G'))) b--; // yopuvchi (bog' emas)
            if (b - a >= 3) MarkWord(s, a, b, ref result);
        }
        return result;
    }

    private static void MarkWord(string s, int a, int b, ref HashSet<int>? result)
    {
        var key = new StringBuilder(b - a);
        for (int k = a; k < b; k++)
        {
            char c = char.ToLowerInvariant(s[k]);
            if (IsA(c))
            {
                if (key.Length > 0 && (key[key.Length - 1] == 'o' || key[key.Length - 1] == 'g')) continue;
                key.Append('\'');
            }
            else if (c >= 'a' && c <= 'z') key.Append(c);
            else return; // boshqa harf (ö, ş, kirill) — lug'at so'zi emas
        }
        string? form = WordList.Lookup(key.ToString());
        if (form == null) return;

        // Yozilgan so'zni to'g'ri shakl bilan yonma-yon yurib, tutuqi tushib qolgan o/g larni topamiz.
        List<int>? marks = null;
        int j = 0;
        for (int k = a; k < b; k++)
        {
            char c = char.ToLowerInvariant(s[k]);
            if (IsA(c))
            {
                if (j < form.Length && form[j] == '\'') { j++; continue; }
                return;
            }
            if (j >= form.Length || form[j] != c) return;
            j++;
            if ((c == 'o' || c == 'g') && j < form.Length && form[j] == '\'' && !(k + 1 < b && IsA(s[k + 1])))
            {
                (marks ??= new List<int>()).Add(k);
                j++;
            }
        }
        if (j != form.Length || marks == null) return;
        result ??= new HashSet<int>();
        result.UnionWith(marks);
    }

    /// <summary>Ko'rinishi bir xil boshqa belgilar: ș/Ș (U+0219/U+0218, vergulli) → ş/Ş.</summary>
    private static char Confusable(char c) => c switch { 'ș' => 'ş', 'Ș' => 'Ş', _ => c };

    private static int? MatchStem(string s, int i, int end)
    {
        foreach (var stem in SHStems)
            if (i + stem.Length <= end && string.Compare(s, i, stem, 0, stem.Length, StringComparison.OrdinalIgnoreCase) == 0)
                return stem.Length;
        return null;
    }
}
