using System.Text;

namespace Ozbeca.Core;

/// <summary>O'zbek kirill matnini 2026-yilgi yangi lotin alifbosiga o'giradi. Kirill bo'lmagan belgilar o'zgarmaydi.</summary>
public static class CyrillicConverter
{
    // Unli harflar: ye va ts qoidalari uchun (aralash matnda lotin unlilari ham).
    private const string Vowels = "аеёиоуыэюяўaeiouö";

    // Yotlangan unlilar: ulardan oldingi qattiq belgi tutuq emas, ajratish belgisi (obyekt).
    private const string Iotated = "еёюяЕЁЮЯ";

    /// <summary>Matnda kirill harfi (U+0400–U+04FF) bormi?</summary>
    public static bool HasCyrillic(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
            if (IsCyrillic(c)) return true;
        return false;
    }

    public static string ToNewLatin(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var sb = new StringBuilder(input.Length + input.Length / 4);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (!IsCyrillic(c)) { sb.Append(c); continue; }

            char lower = char.ToLowerInvariant(c);
            bool upper = c != lower;
            char prev = i > 0 ? input[i - 1] : '\0';
            char next = i + 1 < input.Length ? input[i + 1] : '\0';
            bool wordStart = !char.IsLetter(prev);
            bool afterVowel = Vowels.IndexOf(char.ToLowerInvariant(prev)) >= 0;

            string? multi = null;
            switch (lower)
            {
                case 'ё': multi = "yo"; break;
                case 'ю': multi = "yu"; break;
                case 'я': multi = "ya"; break;
                case 'щ': multi = "şç"; break;
                case 'е':
                    multi = wordStart || afterVowel || prev is 'ъ' or 'Ъ' or 'ь' or 'Ь' ? "ye" : null;
                    if (multi == null) { sb.Append(upper ? 'E' : 'e'); continue; }
                    break;
                case 'ц':
                    if (!wordStart && afterVowel) { multi = "ts"; break; }
                    sb.Append(upper ? 'S' : 's');
                    continue;
                case 'ъ':
                    // Yotlangan unlidan oldin — rus so'zlaridagi ajratish belgisi, tushiriladi; aks holda tutuq.
                    if (Iotated.IndexOf(next) < 0) sb.Append(Transliterator.Tutuq);
                    continue;
                case 'ь':
                    // yumshoq belgi + o → yo (batalyon), qolgan holatda tushiriladi.
                    if (next is 'о' or 'О') sb.Append(char.IsUpper(next) ? 'Y' : 'y');
                    continue;
            }

            if (multi != null)
            {
                if (!upper) sb.Append(multi);
                else if (IsUpperLetter(prev) || IsUpperLetter(next)) sb.Append(multi.ToUpperInvariant());
                else sb.Append(char.ToUpperInvariant(multi[0])).Append(multi, 1, multi.Length - 1);
                continue;
            }

            char latin = Single(lower);
            if (latin == '\0') { sb.Append(c); continue; } // o'zbek alifbosida yo'q kirill harfi — o'zgarmaydi
            // сҳ → sʼh (Исҳоқ → Isʼhoq): tutuqsiz "sh" keyin ş ga aylanib ketardi.
            if (lower == 'ҳ' && sb.Length > 0 && sb[sb.Length - 1] is 's' or 'S') sb.Append(Transliterator.Tutuq);
            sb.Append(upper ? char.ToUpperInvariant(latin) : latin);
        }
        return sb.ToString();
    }

    private static bool IsCyrillic(char c) => c >= (char)0x0400 && c <= (char)0x04FF;

    // Qo'shni harf ham katta bo'lsa — so'z hammasi katta yozilgan (YURT, Yurt emas).
    private static bool IsUpperLetter(char c) => char.IsLetter(c) && char.IsUpper(c);

    private static char Single(char c) => c switch
    {
        'а' => 'a', 'б' => 'b', 'в' => 'v', 'г' => 'g', 'д' => 'd', 'ж' => 'j', 'з' => 'z',
        'и' => 'i', 'й' => 'y', 'к' => 'k', 'л' => 'l', 'м' => 'm', 'н' => 'n', 'о' => 'o',
        'п' => 'p', 'р' => 'r', 'с' => 's', 'т' => 't', 'у' => 'u', 'ф' => 'f', 'х' => 'x',
        'ч' => 'ç', 'ш' => 'ş', 'ы' => 'i', 'э' => 'e', 'ў' => 'ö', 'қ' => 'q', 'ғ' => 'ğ',
        'ҳ' => 'h',
        _ => '\0',
    };
}
