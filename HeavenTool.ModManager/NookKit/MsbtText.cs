#nullable enable

using System.Buffers.Binary;
using System.Text;

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// Message text in NookKit's tagged form, read straight from an msbt's UTF-16, since NintendoTools names tags its own way. A port of
/// <c>decode</c> in NookKit's <c>nookkit-msbt/src/tags.rs</c>: the system tags get names, any other tag is written as
/// <c>{{Tag group="" type="" data=""}}</c>, and a literal <c>{{</c> is written twice.
/// </summary>
public static class MsbtText
{
    private const char Open = '\u000E', Close = '\u000F';
    private const ushort SystemGroup = 0, Ruby = 0, Font = 1, Size = 2, Color = 3, PageBreak = 4;

    /// <summary>
    /// Every message of a little-endian UTF-16 msbt, by label, in NookKit's tagged form. Null when the msbt has another encoding or
    /// no labels. A message whose text NookKit can't read back is left out.
    /// </summary>
    public static Dictionary<string, string>? Read(byte[] msbt)
    {
        var data = msbt.AsSpan();
        // "MsgStdBn", then FF FE for little endian, and 1 for UTF-16 at 0x0C.
        if (data.Length < 0x20 || !data[..8].SequenceEqual("MsgStdBn"u8) || data[8] != 0xFF || data[9] != 0xFE || data[0x0C] != 1) return null;

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(data[0x0E..]);
        ReadOnlySpan<byte> labels = default, texts = default;
        var offset = 0x20;
        for (var i = 0; i < sectionCount && offset + 16 <= data.Length; i++)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);
            var body = data.Slice(offset + 16, size);
            if (data.Slice(offset, 4).SequenceEqual("LBL1"u8)) labels = body;
            else if (data.Slice(offset, 4).SequenceEqual("TXT2"u8)) texts = body;
            offset += 16 + size;
            offset = (offset + 15) & ~15;
        }
        if (labels.IsEmpty || texts.IsEmpty) return null;

        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(texts);
        var result = new Dictionary<string, string>();
        var buckets = (int)BinaryPrimitives.ReadUInt32LittleEndian(labels);
        for (var bucket = 0; bucket < buckets; bucket++)
        {
            var entries = (int)BinaryPrimitives.ReadUInt32LittleEndian(labels[(4 + bucket * 8)..]);
            var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(labels[(8 + bucket * 8)..]);
            for (var entry = 0; entry < entries; entry++)
            {
                int length = labels[at];
                var label = Encoding.ASCII.GetString(labels.Slice(at + 1, length));
                var index = (int)BinaryPrimitives.ReadUInt32LittleEndian(labels[(at + 1 + length)..]);
                at += 1 + length + 4;
                if (index >= count) continue;

                var start = (int)BinaryPrimitives.ReadUInt32LittleEndian(texts[(4 + index * 4)..]);
                var end = index + 1 < count ? (int)BinaryPrimitives.ReadUInt32LittleEndian(texts[(4 + (index + 1) * 4)..]) : texts.Length;
                var units = new char[(end - start) / 2];
                for (var i = 0; i < units.Length; i++) units[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian(texts[(start + i * 2)..]);
                if (Decode(units) is { } text) result.TryAdd(label, text);
            }
        }
        return result;
    }

    /// <summary>MsgStd UTF-16 as tagged text, up to its NUL. Null when a tag runs past the end or the text isn't valid UTF-16.</summary>
    public static string? Decode(ReadOnlySpan<char> units)
    {
        var output = new StringBuilder(units.Length);
        var plain = new StringBuilder();
        var i = 0;
        while (i < units.Length)
        {
            switch (units[i])
            {
                case Open:
                {
                    if (!Flush(output, plain) || i + 3 >= units.Length) return null;
                    var (group, kind, size) = (units[i + 1], units[i + 2], (int)units[i + 3]);
                    if (size % 2 != 0 || i + 4 + size / 2 > units.Length) return null;
                    var parameters = new byte[size];
                    for (var p = 0; p < size / 2; p++) BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(p * 2), units[i + 4 + p]);
                    DecodeTag(output, group, kind, parameters);
                    i += 4 + size / 2;
                    break;
                }
                case Close:
                    if (!Flush(output, plain) || i + 2 >= units.Length) return null;
                    output.Append($"{{{{/Tag group=\"{(ushort)units[i + 1]}\" type=\"{(ushort)units[i + 2]}\"}}}}");
                    i += 3;
                    break;
                case '\0':
                    i = units.Length;
                    break;
                default:
                    plain.Append(units[i]);
                    i++;
                    break;
            }
        }
        return Flush(output, plain) ? output.ToString() : null;
    }

    private static void DecodeTag(StringBuilder output, ushort group, ushort kind, byte[] parameters)
    {
        var named = (group, kind) switch
        {
            (SystemGroup, Ruby) => RubyTag(parameters),
            (SystemGroup, Font) => U16(parameters) is { } face ? $"{{{{Font face=\"{face}\"" : null,
            (SystemGroup, Size) => U16(parameters) is { } percent ? $"{{{{Size percent=\"{percent}\"" : null,
            (SystemGroup, Color) => U16(parameters) is { } index ? $"{{{{Color index=\"{index}\"" : null,
            (SystemGroup, PageBreak) when parameters.Length == 0 => "{{PageBreak",
            _ => null,
        };
        if (named != null) output.Append(named);
        else
        {
            output.Append($"{{{{Tag group=\"{group}\" type=\"{kind}\"");
            if (parameters.Length > 0) output.Append($" data=\"{Convert.ToHexStringLower(parameters)}\"");
        }
        output.Append("}}");
    }

    private static ushort? U16(byte[] parameters) => parameters.Length == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(parameters) : null;

    // Only a reading that reads back the same gets a name: one holding a NUL or "}}" stays a raw tag.
    private static string? RubyTag(byte[] parameters)
    {
        if (parameters.Length < 4) return null;
        var span = BinaryPrimitives.ReadUInt16LittleEndian(parameters);
        var length = BinaryPrimitives.ReadUInt16LittleEndian(parameters.AsSpan(2));
        if (parameters.Length - 4 != length || length % 2 != 0) return null;
        var units = new char[length / 2];
        for (var i = 0; i < units.Length; i++) units[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian(parameters.AsSpan(4 + i * 2));
        if (!IsValid(units)) return null;
        var value = new string(units);
        if (value.Contains('\0') || value.Contains("}}")) return null;
        return $"{{{{Ruby byteSpan=\"{span}\" value=\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static bool Flush(StringBuilder output, StringBuilder plain)
    {
        var text = plain.ToString();
        plain.Clear();
        if (!IsValid(text)) return false;
        output.Append(MessageText.Escape(text));
        return true;
    }

    private static bool IsValid(ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            else if (char.IsSurrogate(text[i])) return false;
        }
        return true;
    }
}
