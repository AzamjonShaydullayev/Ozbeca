using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ozbeca.Core;

/// <summary>
/// Avtomatik o'girishda (yozish paytida va Ctrl+V) kodga tegmaslik uchun: "kodga o'xshash" tokenlar o'zgarmaydi.
/// Hotkey bilan o'girishda ishlatilmaydi — u yerda user o'zi nimani o'girishni tanlaydi.
/// </summary>
public static class CodeGuard
{
    // Gap boshida/oxirida bo'lishi oddiy: (shahar), "choy", shahar., [kvadrat]
    private const string EdgePunctuation = "()[]\"«»“”„,.;:!?…"; // {} — gapda deyarli yo'q, kod belgisi

    // So'z ichida bo'lsa — bu kod/identifikator: user_name, x=5, print(choy), a<b, $var, path\to, a*b, a&b, a|b
    private const string CodeChars = "_=(){}[]<>$\\*&|^~#@/";

    // sh/ch bor inglizcha so'zlar (SCOWL): yozish paytida va Ctrl+V da buzilmasin (check → çeck).
    // O'zbekchada ham shunday yoziladiganlari yo'q (chat, match, chop, chap, bosh) — tools/wordlist/build_english.py.
    private static readonly Lazy<HashSet<string>> EnglishWords = new(LoadEnglish);

    // Inglizcha qisqartma: that's, don't, we'll, they're, I've, Obama's. O'zbekcha ta'm, so'm, o't, ra'd, ko's — emas.
    private static readonly Regex Contraction = new(
        @"^(?:[A-Za-z]+n['’]t|[A-Za-z]+['’](?:ll|re|ve)|[A-Za-z]*[A-FH-NP-Za-fh-np-z]['’]s|(?:I|you|he|she|we|they|who|it|that|there)['’]d|I['’]m)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Token (chetidagi tinish belgilarisiz) inglizcha so'zmi: ro'yxatda bor, inglizcha qisqartma yoki o'zbek lotinida
    /// yo'q harf bor — w yoki h siz c (Chicago, Washington, check, Facebook).
    /// </summary>
    public static bool IsEnglishWord(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        int start = 0, end = token.Length;
        while (start < end && EdgePunctuation.IndexOf(token[start]) >= 0) start++;
        while (end > start && EdgePunctuation.IndexOf(token[end - 1]) >= 0) end--;
        if (end <= start) return false;
        string word = token.Substring(start, end - start);
        if (IrishName.IsMatch(word)) return true;
        // Chiziqchali: push-up, high-tech, T-shirt — bir qismi inglizcha, sh/ch li qolgan qismlari ham inglizcha
        // (ship-shiydon — o'zbekcha).
        bool english = false;
        foreach (string part in word.Split('-'))
        {
            if (part.Length == 0) continue;
            if (IsEnglishPart(part)) english = true;
            else if (part.IndexOf("sh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     part.IndexOf("ch", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        }
        return english;
    }

    // O'Brien, O’Malley — o'zbekchada O' dan keyin kichik harf (O'zbekiston) yoki hammasi katta (O'ZBEK).
    private static readonly Regex IrishName = new(@"^O['’][A-Z][a-z]+$", RegexOptions.CultureInvariant);

    private static bool IsEnglishPart(string word)
    {
        if (EnglishWords.Value.Contains(word) || Contraction.IsMatch(word)) return true;
        for (int i = 0; i < word.Length; i++)
        {
            char c = char.ToLowerInvariant(word[i]);
            if (c == 'w' || (c == 'c' && (i + 1 == word.Length || char.ToLowerInvariant(word[i + 1]) != 'h'))) return true;
        }
        return false;
    }

    private static HashSet<string> LoadEnglish()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var stream = typeof(CodeGuard).Assembly.GetManifestResourceStream("Ozbeca.english.txt");
        if (stream == null) return set;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
            if (line.Length > 0 && line[0] != '#') set.Add(line);
        return set;
    }

    /// <summary>Avtomatik o'girishda tegilmaydigan token: kod yoki inglizcha so'z.</summary>
    public static bool ShouldSkip(string token) => LooksLikeCode(token) || IsEnglishWord(token);

    /// <summary>Token kodga o'xshaydimi (identifikator, ifoda, domen, camelCase).</summary>
    public static bool LooksLikeCode(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        // Funksiya chaqiruvi va operatorlar: choy(), std::cout, a->b, x => y
        if (token.Contains("()") || token.Contains("::") || token.Contains("->") || token.Contains("=>")) return true;
        int start = 0, end = token.Length;
        while (start < end && EdgePunctuation.IndexOf(token[start]) >= 0) start++;
        while (end > start && EdgePunctuation.IndexOf(token[end - 1]) >= 0) end--;
        if (start >= end) return false;

        for (int i = start; i < end; i++)
        {
            char c = token[i];
            if (CodeChars.IndexOf(c) >= 0) return true;

            // Harflar orasidagi nuqta yoki ikki nuqta: shop.check, shahar.uz, std::cout, a:b
            if ((c == '.' || c == ':') && i > start && i < end - 1 && char.IsLetterOrDigit(token[i - 1]) && char.IsLetterOrDigit(token[i + 1]))
                return true;

            // camelCase: kichik harfdan keyin katta harf (showChart, getChars). "O'zbekiston", "SHAHAR" — emas.
            if (char.IsUpper(c) && i > start && char.IsLower(token[i - 1])) return true;
        }
        return false;
    }

    /// <summary>
    /// Matnni (masalan, Ctrl+V bilan qo'yilgan) so'zma-so'z o'giradi: bo'shliqlar aynan saqlanadi,
    /// kodga o'xshash tokenlar o'zgarmaydi.
    /// </summary>
    public static string ConvertProse(string text) => ConvertProse(text, false, null);

    /// <param name="cyrillic">Kirill so'zlar ham o'girilsin (fayl o'girish: faylda o'zbek kirill harflari bo'lsa).</param>
    /// <param name="map">null yoki text.Length + 1: map[i] — kirishdagi i-belgi chiqishda qayerdan boshlanadi.</param>
    public static string ConvertProse(string text, bool cyrillic, int[]? map)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (map != null) map[0] = 0;
            return text ?? string.Empty;
        }
        var sb = new StringBuilder(text.Length);
        bool quoteOpen = false; // 'Yaxshi tong' — tirnoq bir so'zda ochilib, boshqasida yopiladi
        // Muallif tutuq umuman qo'ymaydi (ozbek, bolgan) — ikki ma'noli so'zlar ham: oz → o'z, yoq → yo'q.
        bool ambiguous = Transliterator.RestoreApostrophes && Transliterator.ApostropheStyle(text) == true;
        int i = 0;
        while (i < text.Length)
        {
            int j = i;
            if (char.IsWhiteSpace(text[i]))
            {
                while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                if (text.IndexOf('\n', i, j - i) >= 0) quoteOpen = false; // tirnoq qatordan qatorga o'tmaydi
                if (map != null) Fill(map, i, j - i, sb.Length, j - i, null);
                sb.Append(text, i, j - i);
            }
            else
            {
                while (j < text.Length && !char.IsWhiteSpace(text[j])) j++;
                string token = text.Substring(i, j - i);
                int[]? tm = null;
                string result;
                if (ShouldSkip(token)) result = token;
                else if (cyrillic && CyrillicConverter.HasCyrillic(token))
                    result = CyrillicConverter.ToNewLatin(token, tm = map != null ? new int[token.Length + 1] : null);
                else if (map != null) result = Transliterator.ToNewAlphabet(token, Transliterator.RestoreApostrophes, ref quoteOpen, out tm, ambiguous);
                else result = Transliterator.ToNewAlphabet(token, Transliterator.RestoreApostrophes, ref quoteOpen, ambiguous);
                if (map != null) Fill(map, i, j - i, sb.Length, result.Length, tm);
                sb.Append(result);
            }
            i = j;
        }
        if (map != null) map[text.Length] = sb.Length;
        return sb.ToString();
    }

    // Token xaritasi umumiy xaritaga; xarita yo'q (o'zgarmagan yoki NFC emas) — uzunlik bo'yicha proporsional.
    private static void Fill(int[] map, int at, int len, int outAt, int outLen, int[]? tm)
    {
        for (int k = 0; k < len; k++) map[at + k] = outAt + (tm != null ? tm[k] : k * outLen / len);
    }
}
