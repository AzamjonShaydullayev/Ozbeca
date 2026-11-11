using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Ozbeca.Core;

/// <summary>Eski/aralash o'zbek lotin matnini 2026-yilgi yangi alifboga o'giradi (HANDOFF 4-bo'lim).</summary>
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

    // Tegilmaydigan qismlar: URL, email, @username, #hashtag. Email @username'dan oldin turishi shart.
    private static readonly Regex Protected = new(
        @"(?:https?://|www\.)\S+|[\w.+-]+@[\w-]+(?:\.[\w-]+)+|[@#]\w+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Tutuqsiz yozilgan so'zlarni lug'at bo'yicha tuzatish (ozbek → özbek, togri → toğri). Sozlamalardan o'chiriladi.
    /// Faqat bir ma'noli so'zlar — WordList.
    /// </summary>
    public static bool RestoreApostrophes { get; set; } = true;

    public static string ToNewAlphabet(string input) => Convert(input, null, RestoreApostrophes);

    /// <param name="dictionary">false — lug'atsiz (masalan, pauzada: so'z hali tugamagan, "yoq|imli").</param>
    public static string ToNewAlphabet(string input, bool dictionary) => Convert(input, null, dictionary);

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
        if (string.IsNullOrEmpty(input))
        {
            if (map != null) map[0] = 0;
            return input ?? string.Empty;
        }

        string text = input.Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder(text.Length);
        int pos = 0;
        foreach (Match m in Protected.Matches(text))
        {
            ConvertSegment(text, pos, m.Index, sb, map, dictionary);
            for (int k = 0; k < m.Length && map != null; k++) map[m.Index + k] = sb.Length + k;
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        ConvertSegment(text, pos, text.Length, sb, map, dictionary);
        if (map != null) map[text.Length] = sb.Length;
        return sb.ToString();
    }

    private static bool IsA(char c) => Apostrophes.IndexOf(c) >= 0;

    // ʻ (U+02BB) va ʼ (U+02BC) Unicode'da "modifier letter" — ularni harf deb hisoblamaymiz.
    private static bool IsLetter(char c) => char.IsLetter(c) && !IsA(c);

    private static void ConvertSegment(string s, int start, int end, StringBuilder sb, int[]? map, bool dictionary)
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

            // 3. g + A → ğ, so'z oxirida ham — lekin so'z ochuvchi tirnoq bilan boshlangan bo'lsa ('tong'),
            //    oxiridagi A yopuvchi tirnoq deb olinadi (user qarori, 2026-11-10).
            if ((c == 'g' || c == 'G') && IsA(n1) && (IsLetter(n2) || !OpensWithQuote(s, start, i)))
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

    /// <summary>gIdx dagi harf turgan so'z A-belgi bilan ochilganmi (oldida harf bo'lmagan tirnoq)?</summary>
    private static bool OpensWithQuote(string s, int start, int gIdx)
    {
        int k = gIdx - 1;
        while (k >= start && (IsLetter(s[k]) || IsA(s[k]))) k--;
        int first = k + 1; // so'z (harf/tirnoq ketma-ketligi) boshi
        return first < gIdx && IsA(s[first]);
    }
}
