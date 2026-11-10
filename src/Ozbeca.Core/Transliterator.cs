using System.Text;
using System.Text.RegularExpressions;

namespace Ozbeca.Core;

/// <summary>Eski/aralash o'zbek lotin matnini 2026-yilgi yangi alifboga o'giradi (HANDOFF 4-bo'lim).</summary>
public static partial class Transliterator
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
    [GeneratedRegex(@"(?:https?://|www\.)\S+|[\w.+-]+@[\w-]+(?:\.[\w-]+)+|[@#]\w+", RegexOptions.IgnoreCase)]
    private static partial Regex ProtectedRegex();

    public static string ToNewAlphabet(string input) => Convert(input, null);

    /// <summary>
    /// O'giradi va pozitsiyalar xaritasini qaytaradi: map[i] — kirishdagi i-belgi chiqishda qayerdan boshlanadi,
    /// map[input.Length] — chiqish uzunligi. Stil (qalin, emoji) oraliqlarini yangi matnga ko'chirish uchun.
    /// Kirish NFC bo'lmasa (ajralgan belgilar) xarita null — chaqiruvchi formatsiz yo'lga o'tadi.
    /// </summary>
    public static string ToNewAlphabet(string input, out int[]? map)
    {
        input ??= string.Empty;
        if (!input.IsNormalized(NormalizationForm.FormC)) { map = null; return Convert(input, null); }
        map = new int[input.Length + 1];
        return Convert(input, map);
    }

    private static string Convert(string input, int[]? map)
    {
        if (string.IsNullOrEmpty(input))
        {
            if (map != null) map[0] = 0;
            return input ?? string.Empty;
        }

        string text = input.Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder(text.Length);
        int pos = 0;
        foreach (Match m in ProtectedRegex().Matches(text))
        {
            ConvertSegment(text, pos, m.Index, sb, map);
            for (int k = 0; k < m.Length && map != null; k++) map[m.Index + k] = sb.Length + k;
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        ConvertSegment(text, pos, text.Length, sb, map);
        if (map != null) map[text.Length] = sb.Length;
        return sb.ToString();
    }

    private static bool IsA(char c) => Apostrophes.IndexOf(c) >= 0;

    // ʻ (U+02BB) va ʼ (U+02BC) Unicode'da "modifier letter" — ularni harf deb hisoblamaymiz.
    private static bool IsLetter(char c) => char.IsLetter(c) && !IsA(c);

    private static void ConvertSegment(string s, int start, int end, StringBuilder sb, int[]? map)
    {
        int segOut = sb.Length;
        char At(int k) => k >= start && k < end ? s[k] : '\0';

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

            // 2. o + A → ö, faqat A dan keyin harf kelsa.
            if ((c == 'o' || c == 'O') && IsA(n1) && IsLetter(n2))
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
            if (IsA(c) && sb.Length > segOut && IsLetter(sb[^1]) && IsLetter(n1))
            {
                sb.Append(Tutuq);
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }
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
