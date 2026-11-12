using Ozbeca.Bot.Text;
using Ozbeca.Core;

namespace Ozbeca.Bot.Tests;

public class TextPipelineTests
{
    private static readonly ConvertOptions Fwd = new(false, false, false);
    private static readonly ConvertOptions Rev = new(true, false, false);

    [Fact]
    public void Bold_entity_follows_shrink()
    {
        var r = TextPipeline.Convert("Shahar choy", [new Entity("bold", 7, 4)], Fwd);
        Assert.Equal("Şahar çoy", r.Text);
        var e = Assert.Single(r.Entities);
        Assert.Equal((6, 3), (e.Offset, e.Length));
        Assert.True(r.Changed);
    }

    [Fact]
    public void Reverse_entity_follows_growth()
    {
        var r = TextPipeline.Convert("Şahar", [new Entity("bold", 0, 5)], Rev);
        Assert.Equal("Shahar", r.Text);
        var e = Assert.Single(r.Entities);
        Assert.Equal((0, 6), (e.Offset, e.Length));
    }

    [Fact]
    public void Unchanged_text_reports_not_changed()
    {
        var r = TextPipeline.Convert("salom", [], Fwd);
        Assert.False(r.Changed);
    }

    [Fact]
    public void Bad_offsets_are_dropped_not_thrown()
    {
        var r = TextPipeline.Convert("salom", [new Entity("bold", -1, 2), new Entity("bold", 3, 99), new Entity("bold", 1, 0), new Entity("bold", 0, 2)], Fwd);
        var e = Assert.Single(r.Entities);
        Assert.Equal((0, 2), (e.Offset, e.Length));
    }

    [Fact]
    public void Custom_emoji_keeps_exact_length()
    {
        var r = TextPipeline.Convert("sh😀", [new Entity("custom_emoji", 2, 2, CustomEmojiId: "1")], Fwd);
        Assert.Equal("ş😀", r.Text);
        var e = Assert.Single(r.Entities);
        Assert.Equal((1, 2), (e.Offset, e.Length));
    }

    [Fact]
    public void Parse_and_ToJson_roundtrip_snake_case()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""[{"type":"text_link","offset":1,"length":2,"url":"https://x.uz"},{"type":"custom_emoji","offset":0,"length":2,"custom_emoji_id":"42"}]""");
        var list = Entity.Parse(doc.RootElement);
        Assert.Equal(2, list.Count);
        Assert.Equal("https://x.uz", list[0].Url);
        Assert.Equal("42", list[1].CustomEmojiId);
        var json = System.Text.Json.JsonSerializer.Serialize(list[1].ToJson(), Ozbeca.Bot.Api.Json.Options);
        Assert.Equal("""{"type":"custom_emoji","offset":0,"length":2,"custom_emoji_id":"42"}""", json);
        Assert.Empty(Entity.Parse(null));
    }

    [Fact]
    public void Split_9000_chars_into_three_parts_under_4000()
    {
        var parts = TextPipeline.Split(new string('a', 9000), [], 4000);
        Assert.Equal(3, parts.Count);
        Assert.All(parts, p => Assert.True(p.Text.Length <= 4000));
        Assert.Equal(9000, parts.Sum(p => p.Text.Length));
    }

    [Fact]
    public void Split_never_breaks_surrogate_pair()
    {
        var parts = TextPipeline.Split(new string('a', 3999) + "😀" + "b", [], 4000);
        Assert.Equal(3999, parts[0].Text.Length);
        Assert.Equal("😀b", parts[1].Text);
    }

    [Fact]
    public void Split_prefers_paragraph_break_and_keeps_separator_in_previous_part()
    {
        var parts = TextPipeline.Split("aaa\n\nbbb\n\nccc", [], 10);
        Assert.Equal(["aaa\n\nbbb\n\n", "ccc"], parts.Select(p => p.Text));
    }

    [Fact]
    public void Split_short_and_empty_text()
    {
        Assert.Equal("abc", Assert.Single(TextPipeline.Split("abc", [], 4000)).Text);
        Assert.Empty(TextPipeline.Split("", [], 4000));
    }

    [Fact]
    public void Split_cuts_entity_across_boundary()
    {
        var text = new string('a', 9000);
        var parts = TextPipeline.Split(text, [new Entity("bold", 0, 9000)], 4000);
        foreach (var p in parts)
        {
            var e = Assert.Single(p.Entities);
            Assert.Equal((0, p.Text.Length), (e.Offset, e.Length));
        }
    }

    [Fact]
    public void Split_shifts_entity_into_later_part()
    {
        var parts = TextPipeline.Split("aaa bbb ccc", [new Entity("italic", 8, 3)], 8);
        Assert.Equal("aaa bbb ", parts[0].Text);
        Assert.Empty(parts[0].Entities);
        var e = Assert.Single(parts[1].Entities);
        Assert.Equal((0, 3), (e.Offset, e.Length));
    }

    [Fact]
    public void Parse_clones_user_so_it_survives_document_disposal()
    {
        List<Entity> list;
        using (var doc = System.Text.Json.JsonDocument.Parse("""[{"type":"text_mention","offset":0,"length":3,"user":{"id":7,"first_name":"A"}}]"""))
            list = Entity.Parse(doc.RootElement);
        var json = System.Text.Json.JsonSerializer.Serialize(list[0].ToJson(), Ozbeca.Bot.Api.Json.Options);
        Assert.Contains("\"user\":{\"id\":7", json);
    }

    [Fact]
    public void Split_rejects_max_below_two() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TextPipeline.Split("abc", [], 1));

    [Fact]
    public void Code_entity_is_not_converted_but_outside_is()
    {
        var r = TextPipeline.Convert("Shahar `shahar` choy", [new Entity("code", 8, 6), new Entity("bold", 15, 4)], Fwd);
        Assert.Equal("Şahar `shahar` çoy", r.Text);
        Assert.Equal((7, 6), (r.Entities[0].Offset, r.Entities[0].Length));
        Assert.Equal((14, 3), (r.Entities[1].Offset, r.Entities[1].Length));
    }

    [Fact]
    public void Pre_and_url_blocks_are_preserved()
    {
        var text = "shahar pre shahar https://shahar.uz";
        var r = TextPipeline.Convert(text, [new Entity("pre", 7, 3), new Entity("url", 18, 17)], Fwd);
        Assert.Equal("şahar pre şahar https://shahar.uz", r.Text);
        Assert.Equal((16, 17), (r.Entities[1].Offset, r.Entities[1].Length));
    }

    [Fact]
    public void Entity_overlapping_protected_range_still_remaps()
    {
        var r = TextPipeline.Convert("shahar shahar", [new Entity("bold", 0, 13), new Entity("code", 7, 6)], Fwd);
        Assert.Equal("şahar shahar", r.Text);
        Assert.Equal((0, 12), (r.Entities[0].Offset, r.Entities[0].Length));
    }

    [Fact]
    public void Split_never_emits_whitespace_only_part()
    {
        var parts = TextPipeline.Split("aaaa" + new string(' ', 10) + "bb", [], 4);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Text)));
        Assert.All(parts, p => Assert.True(p.Text.Length <= 4));
    }
}
