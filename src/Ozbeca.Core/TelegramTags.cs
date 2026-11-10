using System.Buffers.Binary;
using System.Text;

namespace Ozbeca.Core;

/// <summary>
/// Telegram Desktop clipboard formati: "application/x-td-field-text" (UTF-8 matn) va
/// "application/x-td-field-tags" (QDataStream, big-endian: qint32 soni; har biri qint32 offset, qint32 length,
/// QString id = quint32 baytlar soni + UTF-16BE). Manba: desktop-app/lib_ui, ui/text/text_entity.cpp (SerializeTags).
/// Stillar (qalin, chiziqli, premium emoji) shu ro'yxatda — o'girishda ularning o'rni xarita bo'yicha suriladi.
/// </summary>
public static class TelegramTags
{
    public const string TextFormat = "application/x-td-field-text";
    public const string TagsFormat = "application/x-td-field-tags";

    public readonly record struct Tag(int Offset, int Length, string? Id);

    /// <summary>Telegram matni va stillarini yangi alifboga o'giradi. Format buzuq bo'lsa false.</summary>
    public static bool TryConvert(byte[] textUtf8, byte[] tags, out string newText, out byte[] newTags)
    {
        newText = "";
        newTags = Array.Empty<byte>();
        string text = Encoding.UTF8.GetString(textUtf8);
        if (!TryParse(tags, text.Length, out var list)) return false;

        newText = Transliterator.ToNewAlphabet(text, out var map);
        if (map == null) return false; // NFC emas — o'rinlarni ishonchli surib bo'lmaydi

        var moved = new List<Tag>(list.Count);
        foreach (var t in list)
        {
            int start = map[t.Offset], end = map[t.Offset + t.Length];
            if (end > start) moved.Add(t with { Offset = start, Length = end - start });
        }
        newTags = Serialize(moved);
        return true;
    }

    public static bool TryParse(byte[] data, int textLength, out List<Tag> tags)
    {
        tags = new List<Tag>();
        var s = data.AsSpan();
        if (s.Length < 4) return false;
        int count = BinaryPrimitives.ReadInt32BigEndian(s);
        s = s[4..];
        if (count <= 0 || count > textLength) return false;
        for (int i = 0; i < count; i++)
        {
            if (s.Length < 12) return false;
            int offset = BinaryPrimitives.ReadInt32BigEndian(s);
            int length = BinaryPrimitives.ReadInt32BigEndian(s[4..]);
            uint bytes = BinaryPrimitives.ReadUInt32BigEndian(s[8..]);
            s = s[12..];
            string? id = null;
            if (bytes != 0xFFFFFFFF) // null QString
            {
                if (bytes > s.Length || bytes % 2 != 0) return false;
                id = Encoding.BigEndianUnicode.GetString(s[..(int)bytes]);
                s = s[(int)bytes..];
            }
            if (offset < 0 || length <= 0 || offset + length > textLength) return false;
            tags.Add(new Tag(offset, length, id));
        }
        return true;
    }

    public static byte[] Serialize(IReadOnlyList<Tag> tags)
    {
        if (tags.Count == 0) return Array.Empty<byte>();
        var ms = new MemoryStream();
        var b4 = new byte[4];
        void I32(int v) { BinaryPrimitives.WriteInt32BigEndian(b4, v); ms.Write(b4); }
        I32(tags.Count);
        foreach (var t in tags)
        {
            I32(t.Offset);
            I32(t.Length);
            if (t.Id == null) { BinaryPrimitives.WriteUInt32BigEndian(b4, 0xFFFFFFFF); ms.Write(b4); continue; }
            var id = Encoding.BigEndianUnicode.GetBytes(t.Id);
            I32(id.Length);
            ms.Write(id);
        }
        return ms.ToArray();
    }
}
