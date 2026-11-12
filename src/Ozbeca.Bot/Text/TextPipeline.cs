using Ozbeca.Core;

namespace Ozbeca.Bot.Text;

public sealed record Converted(string Text, List<Entity> Entities, bool Changed);

public static class TextPipeline
{
    public static Converted Convert(string text, IReadOnlyList<Entity> entities, ConvertOptions o)
    {
        var map = new int[text.Length + 1];
        var result = CodeGuard.ConvertProse(text, o, map);
        var list = new List<Entity>();
        foreach (var e in entities)
        {
            // Telegram ma'lumoti ishonchsiz: noto'g'ri oraliq — tashlanadi
            if (e.Offset < 0 || e.Length <= 0 || (long)e.Offset + e.Length > text.Length) continue;
            int start = map[e.Offset], end = map[e.Offset + e.Length];
            if (end > start) list.Add(e with { Offset = start, Length = end - start });
        }
        return new Converted(result, list.OrderBy(x => x.Offset).ToList(), result != text);
    }

    /// <summary>
    /// Uzun matnni <paramref name="max"/> dan oshmaydigan bo'laklarga bo'ladi. Kesish: oxirgi "\n\n", "\n", ". ", " ",
    /// bo'lmasa max. Ajratgich oldingi bo'lakning oxirida qoladi (hech narsa tushib qolmaydi).
    /// </summary>
    public static List<(string Text, List<Entity> Entities)> Split(string text, IReadOnlyList<Entity> entities, int max)
    {
        var parts = new List<(string, List<Entity>)>();
        int pos = 0;
        do
        {
            int cut = text.Length - pos <= max ? text.Length - pos : CutLength(text, pos, max);
            parts.Add((text.Substring(pos, cut), Clip(entities, pos, pos + cut)));
            pos += cut;
        } while (pos < text.Length);
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
