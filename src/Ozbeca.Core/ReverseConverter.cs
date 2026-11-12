using System.Text;

namespace Ozbeca.Core;

/// <summary>2026-yilgi yangi lotin matnini eski (1995) lotin alifbosiga qaytaradi: ö → o', ş → sh, ʼ → '.</summary>
public static class ReverseConverter
{
    public static string ToOldLatin(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var sb = new StringBuilder(input.Length + input.Length / 8);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            switch (c)
            {
                case 'ö': sb.Append("o'"); break;
                case 'Ö': sb.Append("O'"); break;
                case 'ğ': sb.Append("g'"); break;
                case 'Ğ': sb.Append("G'"); break;
                case 'ş' or 'ș': sb.Append("sh"); break;
                case 'ç': sb.Append("ch"); break;
                case 'Ş' or 'Ș': sb.Append(AllCaps(input, i) ? "SH" : "Sh"); break;
                case 'Ç': sb.Append(AllCaps(input, i) ? "CH" : "Ch"); break;
                case Transliterator.Tutuq: sb.Append('\''); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // Qo'shni harf ham katta bo'lsa — so'z hammasi katta yozilgan (ŞAHAR → SHAHAR).
    private static bool AllCaps(string s, int i) =>
        i > 0 && char.IsLetter(s[i - 1]) && char.IsUpper(s[i - 1]) ||
        i + 1 < s.Length && char.IsLetter(s[i + 1]) && char.IsUpper(s[i + 1]);
}
