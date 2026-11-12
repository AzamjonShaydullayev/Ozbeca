using System.Text;
using Ozbeca.Bot.Text;

namespace Ozbeca.Bot.Ui;

/// <summary>Matn + entity'lar yig'uvchi. Offset'lar UTF-16 birliklarida (C# string uzunligi).</summary>
public sealed class MessageBuilder(PremiumEmoji emoji, long userId)
{
    private readonly StringBuilder _sb = new();
    private readonly List<Entity> _ents = new();

    public MessageBuilder Emoji(string role)
    {
        var plain = emoji.Plain(role);
        if (plain.Length == 0) return this;
        var id = emoji.Id(role, userId, Clock.Today());
        if (id != null) _ents.Add(new Entity("custom_emoji", _sb.Length, plain.Length, CustomEmojiId: id));
        _sb.Append(plain);
        return this;
    }

    public MessageBuilder Text(string s) { _sb.Append(s); return this; }

    public MessageBuilder Bold(string s)
    {
        _ents.Add(new Entity("bold", _sb.Length, s.Length));
        _sb.Append(s);
        return this;
    }

    public MessageBuilder NewLine() => Text("\n");

    public MessageBuilder Quote(string text, IReadOnlyList<Entity> inner, bool expandable)
    {
        var shift = _sb.Length;
        _ents.Add(new Entity(expandable ? "expandable_blockquote" : "blockquote", shift, text.Length));
        _ents.AddRange(inner.Select(e => e with { Offset = e.Offset + shift }));
        _sb.Append(text);
        return this;
    }

    public (string Text, List<Entity> Entities) Build() => (_sb.ToString(), _ents.ToList());
}
