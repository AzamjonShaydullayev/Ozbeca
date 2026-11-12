using Ozbeca.Core;

namespace Ozbeca.Bot.Text;

public sealed record Converted(string Text, List<Entity> Entities, bool Changed);

public static class TextPipeline
{
    // Shu turdagi entity ostidagi matn o'zgarmaydi (kod, havola, e-mail, ...).
    private static readonly HashSet<string> Protected =
        ["code", "pre", "url", "email", "mention", "hashtag", "cashtag", "bot_command", "phone_number"];

    public static Converted Convert(string text, IReadOnlyList<Entity> entities, ConvertOptions o)
    {
        var valid = entities.Where(e => e.Offset >= 0 && e.Length > 0 && (long)e.Offset + e.Length <= text.Length).ToList();

        // himoyalangan oraliqlar birlashtiriladi
        var guard = new bool[text.Length];
        foreach (var e in valid.Where(e => Protected.Contains(e.Type)))
            Array.Fill(guard, true, e.Offset, e.Length);

        // faqat himoyasiz bo'laklar o'giriladi; butun matn bo'yicha bitta monoton xarita yig'iladi
        var map = new int[text.Length + 1];
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length;)
        {
            int j = i;
            while (j < text.Length && guard[j] == guard[i]) j++;
            var seg = text[i..j];
            if (guard[i])
            {
                for (int k = 0; k < seg.Length; k++) map[i + k] = sb.Length + k;
                sb.Append(seg);
            }
            else
            {
                var m = new int[seg.Length + 1];
                var conv = CodeGuard.ConvertProse(seg, o, m);
                for (int k = 0; k < seg.Length; k++) map[i + k] = sb.Length + m[k];
                sb.Append(conv);
            }
            i = j;
        }
        map[text.Length] = sb.Length;
        var result = sb.ToString();

        var list = new List<Entity>();
        foreach (var e in valid)
        {
            int start = map[e.Offset], end = map[e.Offset + e.Length];
            if (end > start) list.Add(e with { Offset = start, Length = end - start });
        }
        return new Converted(result, list.OrderBy(x => x.Offset).ToList(), result != text);
    }

    /// <summary>
    /// Uzun matnni <paramref name="max"/> dan oshmaydigan bo'laklarga bo'ladi. Kesish: oxirgi "\n\n", "\n", ". ", " ",
    /// bo'lmasa max. Ajratgich oldingi bo'lakning oxirida qoladi.
    /// Bo'sh yoki faqat bo'shliqdan iborat bo'lak chiqarilmaydi (Telegram rad etadi): sig'sa oldingisiga qo'shiladi, aks holda tashlanadi.
    /// Bo'sh matn — bo'sh ro'yxat.
    /// </summary>
    public static List<(string Text, List<Entity> Entities)> Split(string text, IReadOnlyList<Entity> entities, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 2);
        var parts = new List<(string Text, List<Entity> Entities)>();
        int pos = 0;
        while (pos < text.Length)
        {
            int cut = text.Length - pos <= max ? text.Length - pos : CutLength(text, pos, max);
            var chunk = text.Substring(pos, cut);
            var ents = Clip(entities, pos, pos + cut);
            if (!string.IsNullOrWhiteSpace(chunk)) parts.Add((chunk, ents));
            else if (parts.Count > 0 && parts[^1].Text.Length + chunk.Length <= max)
            {
                var (pt, pe) = parts[^1];
                pe.AddRange(ents.Select(e => e with { Offset = e.Offset + pt.Length }));
                parts[^1] = (pt + chunk, pe);
            }
            pos += cut;
        }
        return parts;
    }

    private static int CutLength(string text, int pos, int max)
    {
        var window = text.AsSpan(pos, max);
        foreach (var sep in new[] { "\n\n", "\n", ". ", " " })
        {
            int i = window.LastIndexOf(sep.AsSpan(), StringComparison.Ordinal);
            if (i >= 0) return i + sep.Length;
        }
        // surrogat juftligini o'rtasidan kesmaslik
        return char.IsHighSurrogate(text[pos + max - 1]) && max > 1 ? max - 1 : max;
    }

    private static List<Entity> Clip(IReadOnlyList<Entity> entities, int from, int to)
    {
        var list = new List<Entity>();
        foreach (var e in entities)
        {
            if (e.Offset < 0 || e.Length <= 0) continue;
            int s = Math.Max(e.Offset, from), en = (int)Math.Min((long)e.Offset + e.Length, to);
            if (en > s) list.Add(e with { Offset = s - from, Length = en - s });
        }
        return list;
    }
}
