using System;
using System.Collections.Generic;
using System.IO;
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
        int pos = 0;
        if (!ReadI32(data, ref pos, out int count) || count <= 0 || count > textLength) return false;
        for (int i = 0; i < count; i++)
        {
            if (!ReadI32(data, ref pos, out int offset) || !ReadI32(data, ref pos, out int length) ||
                !ReadI32(data, ref pos, out int bytes)) return false;
            string? id = null;
            if (bytes != -1) // 0xFFFFFFFF — null QString
            {
                if (bytes < 0 || bytes > data.Length - pos || bytes % 2 != 0) return false;
                id = Encoding.BigEndianUnicode.GetString(data, pos, bytes);
                pos += bytes;
            }
            if (offset < 0 || length <= 0 || length > textLength - offset) return false; // offset+length int'dan toshmasin
            tags.Add(new Tag(offset, length, id));
        }
        return true;
    }

    public static byte[] Serialize(IReadOnlyList<Tag> tags)
    {
        if (tags.Count == 0) return Array.Empty<byte>();
        var ms = new MemoryStream();
        void I32(int v) => ms.Write(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }, 0, 4);
        I32(tags.Count);
        foreach (var t in tags)
        {
            I32(t.Offset);
            I32(t.Length);
            if (t.Id == null) { I32(-1); continue; }
            var id = Encoding.BigEndianUnicode.GetBytes(t.Id);
            I32(id.Length);
            ms.Write(id, 0, id.Length);
        }
        return ms.ToArray();
    }

    private static bool ReadI32(byte[] d, ref int pos, out int value)
    {
        value = 0;
        if (d.Length - pos < 4) return false;
        value = (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];
        pos += 4;
        return true;
    }
}
