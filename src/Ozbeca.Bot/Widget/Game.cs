using Ozbeca.Core;

namespace Ozbeca.Bot.Widget;

/// <summary>"Harfni top" topishmog'i: <see cref="Masked"/> — yangi alifbodagi so'z, bitta harfi "?"; <see cref="Options"/> — 3 ta katta harf.</summary>
public sealed record Puzzle(string Masked, string Options, char Answer);

/// <summary>"Harfni top" o'yini: game_words.txt (eski alifbo) → yangi alifbo → bitta Ö/Ğ/Ş/Ç yashiriladi.</summary>
public static class Game
{
    private const string Letters = "ÖĞŞÇ";

    /// <summary>Yangi alifbodagi so'zlar (kichik harf).</summary>
    internal static readonly IReadOnlyList<string> Words = Load();

    private static List<string> Load()
    {
        using var s = typeof(Game).Assembly.GetManifestResourceStream("game_words.txt")
                      ?? throw new InvalidOperationException("game_words.txt resursi yo'q");
        using var r = new StreamReader(s);
        return r.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Transliterator.ToNewAlphabet).ToList();
    }

    public static Puzzle Next(Random rnd)
    {
        var word = Words[rnd.Next(Words.Count)];
        var spots = Enumerable.Range(0, word.Length).Where(i => Letters.Contains(char.ToUpperInvariant(word[i]))).ToList();
        var at = spots[rnd.Next(spots.Count)];
        var answer = char.ToUpperInvariant(word[at]);
        var options = Letters.Where(c => c != answer).OrderBy(_ => rnd.Next()).Take(2).Append(answer).OrderBy(_ => rnd.Next());
        return new Puzzle(word[..at] + "?" + word[(at + 1)..], string.Concat(options), answer);
    }

    /// <summary>Store.GameAnswer uchun: javob + variantlar + so'z ("ŞÇŞÖ?ahar").</summary>
    public static string Pack(Puzzle p) => p.Answer + p.Options + p.Masked;

    public static Puzzle? Unpack(string? s) =>
        s is { Length: >= 5 } && s.Substring(1, 3).Contains(s[0]) && s.Substring(1, 3).All(Letters.Contains) && s[4..].Contains('?')
            ? new Puzzle(s[4..], s.Substring(1, 3), s[0])
            : null;
}
