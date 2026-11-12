using System.Reflection;
using Ozbeca.Bot.Text;
using Ozbeca.Bot.Ui;

namespace Ozbeca.Bot.Tests;

public class UiTests
{
    private static readonly Dictionary<string, string> OldSpelling = new()
    {
        ["Greeting"] = "Salom! Matn yuboring — yangi alifboga o'giraman.",
        ["Help"] = "Matn yuboring — yangi alifboga o'giraman. Faylni (Word, Excel, PowerPoint yoki matn) yuborsangiz, formatini saqlab o'giraman.\nTeskari tugma yangi alifbodan eski alifboga qaytaradi. Boshqa chatda @ozbecauzbot deb yozsangiz ham ishlayman.",
        ["NewHeader"] = "Yangi alifboda:",
        ["OldHeader"] = "Eski alifboda:",
        ["NoChange"] = "Bu matnda o'giradigan narsa yo'q.",
        ["SendText"] = "Matn yuboring.",
        ["FileBusy"] = "Avval oldingi faylni tugatay, biroz kuting.",
        ["FileTooBig"] = "Fayl juda katta. 20 MB gacha fayl yuboring.",
        ["FileUnsupported"] = "Bu fayl turini o'girolmayman. Word, Excel, PowerPoint yoki matn fayl yuboring.",
        ["FileBroken"] = "Faylni o'qiy olmadim. Fayl buzilgan bo'lishi mumkin.",
        ["FileNoChange"] = "Faylda o'giradigan narsa yo'q.",
        ["FileDone"] = "Tayyor! Fayl yangi alifboda.",
        ["CardCaption"] = "O'zbecha — yangi alifbo",
        ["CopyButton"] = "Nusxa",
        ["ReverseButton"] = "Teskari",
        ["SettingsTitle"] = "Sozlamalar. Tugmani bosib o'zgartiring.",
        ["SetOldToNew"] = "Eski → Yangi",
        ["SetNewToOld"] = "Yangi → Eski",
        ["SetCyrillic"] = "Kirillni ham o'girish",
        ["SetWords"] = "Tutuqsiz so'zlar",
        ["SetFile"] = "Katta matnni fayl qilib berish",
        ["InlineNewTitle"] = "Yangi alifbo",
        ["InlineOldTitle"] = "Eski alifbo",
        ["InlineHint"] = "Matn yozing",
        ["InlineLongButton"] = "Uzun matn — botda o'girish",
        ["StatTitle"] = "Statistika",
        ["BroadcastAsk"] = "Xabarni yuboring. Barcha foydalanuvchilarga tarqataman.",
        ["BroadcastDone"] = "Tarqatish tugadi.",
        ["BroadcastStopped"] = "Tarqatish to'xtatildi.",
        ["NotAdmin"] = "Bu buyruq faqat admin uchun.",
        ["Flood"] = "Juda tez yozyapsiz. Biroz kuting.",
    };

    [Fact]
    public void Texts_match_correct_old_spelling_converted()
    {
        var fields = typeof(Texts).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToList();
        foreach (var f in fields)
        {
            Assert.True(OldSpelling.TryGetValue(f.Name, out var old), $"{f.Name}: eski imlo jadvalida yo'q");
            Assert.Equal(Ozbeca.Core.Transliterator.ToNewAlphabet(old!), (string)f.GetRawConstantValue()!);
        }
        Assert.Equal(fields.Count, OldSpelling.Count);
    }

    [Fact]
    public void All_texts_are_in_new_alphabet()
    {
        var fields = typeof(Texts).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToList();
        Assert.True(fields.Count >= 25);
        foreach (var f in fields)
        {
            var s = (string)f.GetRawConstantValue()!;
            Assert.True(Ozbeca.Core.Transliterator.ToNewAlphabet(s) == s, $"{f.Name}: ToNewAlphabet o'zgartirdi");
            Assert.True(Ozbeca.Core.CodeGuard.ConvertProse(s) == s, $"{f.Name}: ConvertProse o'zgartirdi");
        }
    }

    [Fact]
    public void Emoji_stable_within_day_changes_next_day()
    {
        var e = PremiumEmoji.LoadEmbedded(true);
        var d = new DateOnly(2026, 11, 1);
        Assert.Equal(e.Id("greeting", 7, d), e.Id("greeting", 7, d));
        var ids = Enumerable.Range(0, 30).Select(i => e.Id("greeting", 7, d.AddDays(i))).Distinct().ToList();
        Assert.True(ids.Count >= 2);
        Assert.DoesNotContain(null, ids);
    }

    [Fact]
    public void Emoji_disabled_returns_null_id_and_plain_text()
    {
        var e = PremiumEmoji.LoadEmbedded(false);
        Assert.Null(e.Id("greeting", 7, new DateOnly(2026, 11, 1)));
        Assert.Equal("👋", e.Plain("greeting"));
    }

    [Fact]
    public void Emoji_unknown_role_falls_back_to_animated_char()
    {
        var e = PremiumEmoji.LoadEmbedded(true);
        Assert.NotNull(e.Id("😀", 1, new DateOnly(2026, 11, 1)));
        Assert.Equal("😀", e.Plain("😀"));
        Assert.Null(e.Id("no_such_role", 1, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Builder_quote_shifts_inner_entities()
    {
        var b = new MessageBuilder(PremiumEmoji.LoadEmbedded(true), 5);
        b.Emoji("success").Text(" Yangi alifboda:").NewLine()
         .Quote("Şahar", new[] { new Entity("bold", 0, 5) }, false);
        var (text, ents) = b.Build();
        var header = text.IndexOf('\n') + 1;
        Assert.Equal("Şahar", text[header..]);
        var ce = Assert.Single(ents, x => x.Type == "custom_emoji");
        Assert.Equal(0, ce.Offset);
        Assert.Equal(text.IndexOf(' '), ce.Length);
        var bq = Assert.Single(ents, x => x.Type == "blockquote");
        Assert.Equal(header, bq.Offset);
        Assert.Equal(5, bq.Length);
        Assert.Equal(header, Assert.Single(ents, x => x.Type == "bold").Offset);
    }

    [Fact]
    public void Builder_expandable_and_bold()
    {
        var b = new MessageBuilder(PremiumEmoji.LoadEmbedded(false), 5);
        b.Bold("A").Quote("xyz", Array.Empty<Entity>(), true);
        var (_, ents) = b.Build();
        Assert.Contains(ents, x => x.Type == "bold" && x.Offset == 0 && x.Length == 1);
        Assert.Contains(ents, x => x.Type == "expandable_blockquote" && x.Offset == 1 && x.Length == 3);
        Assert.DoesNotContain(ents, x => x.Type == "custom_emoji");
    }

    [Fact]
    public void Buttons_reject_oversized_data()
    {
        Buttons.Callback("T", new string('a', 64));
        Assert.Throws<ArgumentException>(() => Buttons.Callback("T", new string('a', 65)));
        Assert.Throws<ArgumentException>(() => Buttons.Callback("T", new string('ö', 33)));   // 66 bayt
        Buttons.Copy("N", new string('a', 256));
        Assert.Throws<ArgumentException>(() => Buttons.Copy("N", new string('a', 257)));
    }

    [Fact]
    public void Buttons_shapes()
    {
        var cb = (Dictionary<string, object?>)Buttons.Callback("T", "d", "primary", "123");
        Assert.Equal("d", cb["callback_data"]);
        Assert.Equal("primary", cb["style"]);
        Assert.Equal("123", cb["icon_custom_emoji_id"]);
        var cp = (Dictionary<string, object?>)Buttons.Copy("Nusxa", "abc");
        Assert.False(cp.ContainsKey("style"));
        var m = (Dictionary<string, object?>)Buttons.Markup(new object[] { cb, cp });
        Assert.Single((object[][])m["inline_keyboard"]!);
    }
}
