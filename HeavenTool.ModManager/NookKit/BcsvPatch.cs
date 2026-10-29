#nullable enable

using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System.Text.Json.Nodes;

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// Adds rows to one bcsv (<see cref="File"/> is its name without extension, e.g. <c>NmlNpcParam</c>). A row replaces the row
/// that has the same value in the table's unique column, and is added otherwise.
/// </summary>
/// <remarks>
/// Each row lists every column. A column is keyed by the string the game hashes for it (<c>UniqueID u16</c>), or by
/// <c>0x</c> and its hash when that string isn't known. Values are numbers, strings for string columns, a string or a number for
/// hash columns (the string gets hashed), and a hex string for bit fields.
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

    public static string ColumnKey(Field field) =>
        field.GetTranslatedNameOrNull() is { } name && name.ToCRC32() == field.Hash ? name : "0x" + field.Hash.ToString("x8");

    private static JsonNode Value(Field field, object cell) => field.DataType switch
    {
        DataType.String => JsonValue.Create((string)cell),
        DataType.Float32 => NookKitJson.Number((float)cell),
        DataType.CRC32 => HashValue((uint)cell, HashManager.GetHashTranslationOrNull, HashUtility.ToCRC32),
        DataType.MMH3 => HashValue((uint)cell, HashManager.GetMurmurHashTranslationOrNull, HashUtility.ToMurmur),
        DataType.BitField => JsonValue.Create(Convert.ToHexStringLower((byte[])cell)),
        _ => JsonValue.Create(Convert.ToInt64(cell)),
    };

    // A known string is clearer than its hash, but only when it hashes back to the same value.
    private static JsonNode HashValue(uint hash, Func<uint, string?> translate, Func<string, uint> rehash) =>
        translate(hash) is { } text && rehash(text) == hash ? JsonValue.Create(text) : JsonValue.Create(hash);
}
