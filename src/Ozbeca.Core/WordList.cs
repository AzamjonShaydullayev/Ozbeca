using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ozbeca.Core;

/// <summary>
/// Tutuqsiz yozilgan o'/g' so'zlar lug'ati (ozbek → o'zbek, togri → to'g'ri). Faqat bir ma'noli so'zlar:
/// tutuqsiz shakli boshqa haqiqiy so'z bo'lsa (ot/o't, qol/qo'l, qoshiq/qo'shiq) — ro'yxatda yo'q.
/// words.txt — tools/wordlist/build_wordlist.py bilan yasalgan, exe ichiga resurs sifatida kiradi.
/// </summary>
internal static class WordList
{
    /// <summary>kalit (kichik harf, o/g dan keyingi tutuqlarsiz) → to'g'ri shakl (kichik harf, tutuq ').</summary>
    private static readonly Lazy<Dictionary<string, string>> Map = new(Load);

    public static string? Lookup(string key) => Map.Value.TryGetValue(key, out var form) ? form : null;

    public static string Key(string form)
    {
        var sb = new StringBuilder(form.Length);
        foreach (char c in form)
            if (c != '\'' || sb.Length == 0 || (sb[sb.Length - 1] != 'o' && sb[sb.Length - 1] != 'g')) sb.Append(c);
        return sb.ToString();
    }

    private static Dictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = typeof(WordList).Assembly.GetManifestResourceStream("Ozbeca.words.txt");
        if (stream == null) return map; // resurs yo'q — lug'atsiz ishlaymiz
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
            if (line.Length > 0 && line[0] != '#') map[Key(line)] = line;
        return map;
    }
}
