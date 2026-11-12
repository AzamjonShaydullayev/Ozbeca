using System.Globalization;
using Ozbeca.Core;

namespace Ozbeca.Bot.Widget;

/// <summary>
/// "Harfni top" topishmog'i: <see cref="Masked"/> — yangi alifbodagi so'z, javob harflari o'rnida "?";
/// <see cref="Options"/> — katta harflar (3 yoki 4); <see cref="Answer"/> — to'g'ri harflar kataklar tartibida (katta);
/// <see cref="IssuedMs"/> — berilgan vaqt (<see cref="Clock.Ms"/>).
/// </summary>
public sealed record Puzzle(string Masked, string Options, string Answer, int Level, long IssuedMs)
{
    /// <summary>Soniya; 0 — taymer yo'q.</summary>
    public int Timer => Level == 2 ? Game.Seconds : 0;
}

/// <summary>
/// "Harfni top" o'yini. Daraja seriyadan: 0 — game_words.txt, 1 katak, 3 variant; 1 — game_words_long.txt, 2 katak,
/// 4 variant (ÖĞŞÇ); 2 — 1 kabi + 10 s taymer (server vaqtni tekshiradi, 3 s zaxira bilan).
/// </summary>
public static class Game
{
    private const string Letters = "ÖĞŞÇ";
    internal const int Seconds = 10;
    private const int GraceMs = 3000;
    /// <summary>oyin.html javob natijasini shuncha ko'rsatadi (setTimeout 1500), keyin yangi topishmoq taymeri boshlanadi.</summary>
    internal const long ResultHoldMs = 1500;

    /// <summary>Yangi alifbodagi so'zlar (kichik harf).</summary>
    internal static readonly IReadOnlyList<string> Words = Load("game_words.txt");
    internal static readonly IReadOnlyList<string> LongWords = Load("game_words_long.txt");

    private static List<string> Load(string name)
    {
        using var s = typeof(Game).Assembly.GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException(name + " resursi yo'q");
        using var r = new StreamReader(s);
        return r.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Transliterator.ToNewAlphabet).ToList();
    }

    public static int LevelFor(int streak) => streak >= 10 ? 2 : streak >= 5 ? 1 : 0;

    public static Puzzle Next(Random rnd, int level, long nowMs)
    {
        var easy = level == 0;
        var list = easy ? Words : LongWords;
        var word = list[rnd.Next(list.Count)];
        var spots = Enumerable.Range(0, word.Length).Where(i => Letters.Contains(char.ToUpperInvariant(word[i])))
            .OrderBy(_ => rnd.Next()).Take(easy ? 1 : 2).Order().ToList();
        var answer = string.Concat(spots.Select(i => char.ToUpperInvariant(word[i])));
        var options = easy
            ? Letters.Where(c => c != answer[0]).OrderBy(_ => rnd.Next()).Take(2).Append(answer[0]).OrderBy(_ => rnd.Next())
            : Letters.OrderBy(_ => rnd.Next());
        var masked = word.ToCharArray();
        foreach (var i in spots) masked[i] = '?';
        return new Puzzle(new string(masked), string.Concat(options), answer, easy ? 0 : level, nowMs);
    }

    /// <summary>Store.GameAnswer uchun: "daraja|javob|variantlar|vaqt|so'z".</summary>
    public static string Pack(Puzzle p) => $"{p.Level}|{p.Answer}|{p.Options}|{p.IssuedMs}|{p.Masked}";

    /// <summary>Yaroqsiz yoki eski format (1-harfli "ŞÇŞÖ?ahar") — null.</summary>
    public static Puzzle? Unpack(string? s)
    {
        var f = s?.Split('|');
        if (f is not { Length: 5 } || f[0] is not ("0" or "1" or "2")
            || !long.TryParse(f[3], NumberStyles.None, CultureInfo.InvariantCulture, out var ms)) return null;
        var level = f[0][0] - '0';
        var answer = f[1].ToUpperInvariant();
        var options = f[2];
        if (options.Length != (level == 0 ? 3 : 4) || options.Distinct().Count() != options.Length || !options.All(Letters.Contains)
            || answer.Length != (level == 0 ? 1 : 2) || !answer.All(options.Contains) || f[4].Count(c => c == '?') != answer.Length)
            return null;
        return new Puzzle(f[4], options, answer, level, ms);
    }

    /// <summary>
    /// Widget so'rovini tekshiradi: "g:i" (1 katak), "g:ij" (2 katak), "g:t" (taymer tugadi, faqat taymerli).
    /// null — so'rov bu topishmoqqa mos emas ("err"); true/false — to'g'ri/xato (kechikkan javob — xato).
    /// </summary>
    public static bool? Check(Puzzle p, string data, long nowMs)
    {
        if (data == "g:t") return p.Timer > 0 ? false : null;
        if (data.Length != 2 + p.Answer.Length || !data.StartsWith("g:", StringComparison.Ordinal)) return null;
        var picks = data[2..];
        if (!picks.All(c => c >= '0' && c - '0' < p.Options.Length)) return null;
        var right = picks.Select((c, i) => char.ToUpperInvariant(p.Options[c - '0']) == char.ToUpperInvariant(p.Answer[i])).All(x => x);
        return right && !Expired(p, nowMs);
    }

    /// <summary>Taymerli topishmoq vaqti (+ zaxira) o'tib ketgan — endi to'g'ri javob bo'lmaydi.</summary>
    public static bool Expired(Puzzle p, long nowMs) => p.Timer > 0 && nowMs - p.IssuedMs > p.Timer * 1000L + GraceMs;
}
