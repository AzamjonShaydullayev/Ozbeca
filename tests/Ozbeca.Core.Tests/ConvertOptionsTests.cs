using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class ConvertOptionsTests
{
    [Fact] public void Reverse_map_tracks_growth()
    {
        var map = new int[11];
        Assert.Equal("Shahar o'g'il", ReverseConverter.ToOldLatin("Şahar öğil", map));
        Assert.Equal(0, map[0]); Assert.Equal(2, map[1]); Assert.Equal(13, map[10]);
    }

    [Fact] public void Prose_reverse_skips_code_and_converts_words() =>
        Assert.Equal("www.şop.uz Shahar", CodeGuard.ConvertProse("www.şop.uz Şahar", new ConvertOptions(true, false, true), null));

    [Fact] public void Prose_reverse_cyrillic_only_when_enabled()
    {
        Assert.Equal("Shahar", CodeGuard.ConvertProse("Шаҳар", new ConvertOptions(true, true, true), null));
        Assert.Equal("Шаҳар", CodeGuard.ConvertProse("Шаҳар", new ConvertOptions(true, false, true), null));
    }

    [Fact] public void Prose_forward_cyrillic_without_uzbek_letters() =>
        Assert.Equal("salom dunyo", CodeGuard.ConvertProse("салом дунё", new ConvertOptions(false, true, true), null));

    [Fact] public void Dictionary_option_ignores_global_flag()
    {
        bool old = Transliterator.RestoreApostrophes;
        try
        {
            Transliterator.RestoreApostrophes = false;
            Assert.Equal("özbek", CodeGuard.ConvertProse("ozbek", new ConvertOptions(false, false, true), null));
            Assert.Equal("ozbek", CodeGuard.ConvertProse("ozbek", new ConvertOptions(false, false, false), null));
        }
        finally { Transliterator.RestoreApostrophes = old; }
    }

    [Fact] public void Prose_map_is_full_length_in_reverse()
    {
        string s = "Şahar çoy"; var map = new int[s.Length + 1];
        string r = CodeGuard.ConvertProse(s, new ConvertOptions(true, false, true), map);
        Assert.Equal(r.Length, map[s.Length]); Assert.Equal(r.IndexOf("choy"), map[6]);
    }

    [Fact] public void Prose_map_composes_for_reverse_cyrillic()
    {
        string s = "Шаҳар чой"; var map = new int[s.Length + 1];
        string r = CodeGuard.ConvertProse(s, new ConvertOptions(true, true, true), map);
        Assert.Equal("Shahar choy", r);
        Assert.Equal(r.Length, map[s.Length]); Assert.Equal(r.IndexOf("choy"), map[6]);
    }
}
