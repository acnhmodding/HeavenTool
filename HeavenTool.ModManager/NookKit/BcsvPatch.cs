#nullable enable

using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// Adds rows to one bcsv (<see cref="File"/> is its name without extension, e.g. <c>NmlNpcParam</c>). A row replaces the row
/// that has the same value in the table's unique column, and is added otherwise.
/// </summary>
/// <remarks>
/// Each row lists every column. A column is keyed by the string the game hashes for it (<c>UniqueID u16</c>), or by
/// <c>0x</c> and its hash when that string isn't known. Values are numbers, strings for string columns, a hex string for bit
/// fields, and for hash columns a number or the hashed string tagged as <c>{"crc32": ...}</c> or <c>{"mmh3": ...}</c>, since
/// NookKit can't tell which hash a column holds from its name.
/// </remarks>
public sealed record BcsvPatch(string File, IReadOnlyList<JsonObject> Rows)
{
    /// <summary>One row of a table as NookKit JSON, with its columns in the table's order.</summary>
    public static JsonObject Row(BinaryCSV table, object[] entry)
    {
        var row = new JsonObject();
        for (var i = 0; i < table.Fields.Length; i++)
            row[ColumnKey(table.Fields[i])] = Value(table.Fields[i], entry[i]);
        return row;
    }

    /// <summary>
    /// Reads a row written by <see cref="Row"/> back into the table's cell types. Columns the row leaves out get the table's default.
    /// </summary>
    public static object[] ReadRow(BinaryCSV table, JsonObject row)
    {
        var entry = new object[table.Fields.Length];
        for (var i = 0; i < table.Fields.Length; i++)
        {
            var field = table.Fields[i];
            entry[i] = row[ColumnKey(field)] is { } value ? ReadValue(field, value) : field.GetFieldDefaultValue();
        }
        return entry;
    }

    public static string ColumnKey(Field field) =>
        field.GetTranslatedNameOrNull() is { } name && name.ToCRC32() == field.Hash ? name : "0x" + field.Hash.ToString("x8");

    private static JsonNode Value(Field field, object cell) => field.DataType switch
    {
        DataType.String => JsonValue.Create((string)cell),
        DataType.Float32 => NookKitJson.Number((float)cell),
        DataType.CRC32 => HashValue((uint)cell, HashManager.GetHashTranslationOrNull, HashUtility.ToCRC32, Crc32Tag),
        DataType.MMH3 => HashValue((uint)cell, HashManager.GetMurmurHashTranslationOrNull, HashUtility.ToMurmur, Mmh3Tag),
        DataType.BitField => JsonValue.Create(Convert.ToHexStringLower((byte[])cell)),
        _ => JsonValue.Create(Convert.ToInt64(cell)),
    };

    private const string Crc32Tag = "crc32", Mmh3Tag = "mmh3";

    // A known string is clearer than its hash, but only when it hashes back to the same value.
    private static JsonNode HashValue(uint hash, Func<uint, string?> translate, Func<string, uint> rehash, string tag) =>
        translate(hash) is { } text && rehash(text) == hash ? new JsonObject { [tag] = text } : JsonValue.Create(hash);

    // A plain string is still read, from rows saved before hashes were tagged.
    private static uint ReadHash(JsonNode node, string tag, Func<string, uint> hash) => node switch
    {
        JsonObject tagged when tagged.Count == 1 && tagged[tag] is JsonValue text => hash(text.GetValue<string>()),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => hash(value.GetValue<string>()),
        JsonValue value => checked((uint)Integer(value)),
        _ => throw new JsonException($"Expected a number, a string or {{\"{tag}\": ...}}, not {node.ToJsonString()}."),
    };

    private static object ReadValue(Field field, JsonNode node) => field.DataType switch
    {
        DataType.CRC32 => ReadHash(node, Crc32Tag, HashUtility.ToCRC32),
        DataType.MMH3 => ReadHash(node, Mmh3Tag, HashUtility.ToMurmur),
        _ => ReadPlainValue(field, node as JsonValue ?? throw new JsonException($"Expected a value, not {node.ToJsonString()}.")),
    };

    private static object ReadPlainValue(Field field, JsonValue value) => field.DataType switch
    {
        DataType.String => value.GetValue<string>(),
        DataType.Float32 => float.Parse(value.ToJsonString(), CultureInfo.InvariantCulture),
        DataType.BitField => Convert.FromHexString(value.GetValue<string>()),
        DataType.S8 => checked((sbyte)Integer(value)),
        DataType.U8 => checked((byte)Integer(value)),
        DataType.Int16 => checked((short)Integer(value)),
        DataType.UInt16 => checked((ushort)Integer(value)),
        DataType.Int32 => checked((int)Integer(value)),
        DataType.UInt32 => checked((uint)Integer(value)),
        _ => throw new NotSupportedException($"Unexpected column type {field.DataType}."),
    };

    // Read from the number's text, which works the same for parsed values and for values built in memory with any integer type.
    private static long Integer(JsonValue value) => long.Parse(value.ToJsonString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
}
