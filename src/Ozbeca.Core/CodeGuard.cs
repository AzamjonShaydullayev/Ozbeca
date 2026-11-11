using System;
using System.Collections.Generic;
using System.Text;

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

    // Ko'p uchraydigan inglizcha so'zlar: yozish paytida va Ctrl+V da buzilmasin (check → çeck).
    // O'zbekchada ham xuddi shunday yoziladiganlari ataylab yo'q: chat, match, finish, shop, much, chop, chin, char.
    // Ro'yxat g'oyasi — XakimovNosirbek (PR #1).
    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "she", "shall", "should", "shape", "share", "shared", "sharp", "shell", "shift", "ship", "shipping", "shopping",
        "short", "shortcut", "shot", "show", "showed", "showing", "shown", "shut", "chain", "chance", "change", "changed", "changes",
        "changing", "channel", "chapter", "character", "charge", "chart", "charts", "cheap", "check", "checked",
        "checking", "checkout", "checks", "chief", "child", "children", "choice", "choose", "chrome", "church", "cache", "schedule",
        "scheme", "school", "which", "such", "each", "reach", "teach", "touch", "watch", "catch", "march", "search",
        "searching", "speech", "branch", "lunch", "launch", "tech", "technical", "technology", "machine", "merchant", "purchase",
        "architecture", "english", "finished", "publish", "published", "push", "pushed", "crash",
        "refresh", "wish", "dish", "wash", "cash", "hash", "trash", "brush", "dash", "slash", "splash", "mesh", "fashion",
    };

    /// <summary>Token (chetidagi tinish belgilarisiz) ko'p uchraydigan inglizcha so'zmi.</summary>
    public static bool IsEnglishWord(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        int start = 0, end = token.Length;
        while (start < end && EdgePunctuation.IndexOf(token[start]) >= 0) start++;
        while (end > start && EdgePunctuation.IndexOf(token[end - 1]) >= 0) end--;
        return end > start && EnglishWords.Contains(token.Substring(start, end - start));
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
    public static string ConvertProse(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        var sb = new StringBuilder(text.Length);
        bool quoteOpen = false; // 'Yaxshi tong' — tirnoq bir so'zda ochilib, boshqasida yopiladi
        int i = 0;
        while (i < text.Length)
        {
            int j = i;
            if (char.IsWhiteSpace(text[i]))
            {
                while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                if (text.IndexOf('\n', i, j - i) >= 0) quoteOpen = false; // tirnoq qatordan qatorga o'tmaydi
                sb.Append(text, i, j - i);
            }
            else
            {
                while (j < text.Length && !char.IsWhiteSpace(text[j])) j++;
                string token = text.Substring(i, j - i);
                sb.Append(ShouldSkip(token) ? token
                    : Transliterator.ToNewAlphabet(token, Transliterator.RestoreApostrophes, ref quoteOpen));
            }
            i = j;
        }
        return sb.ToString();
    }
}
