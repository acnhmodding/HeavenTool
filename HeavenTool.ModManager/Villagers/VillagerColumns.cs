#nullable enable

using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;

namespace HeavenTool.ModManager.Villagers;

/// <summary>The NmlNpcParam columns the villager creator edits, by the string the game hashes for each.</summary>
public static class VillagerColumns
{
    public const string Label = "Label string8";
    public const string UniqueId = "UniqueID u16";
    public const string ResName = "ResName string64";
    public const string Personality = "NpcLooks.hshCstringRef";
    public const string SubPersonality = "NpcTalkType u8";
    public const string Hobby = "Hobby.hshCstringRef";
    public const string BirthMonth = "BirthMonth u8";
    public const string BirthDay = "BirthMDay u8";
    public const string Color1 = "Color1 u8";
    public const string Color2 = "Color2 u8";
    public const string NameColor = "NpcColor u8";
    public const string Tops = "Tops u16";
    public const string RainHat = "RainHat u16";
    public const string RainWear = "RainWare u16";
    public const string Umbrella = "Umbrella u16";
    public const string Photo = "BromideItemID s16";
    public const string Poster = "PosterItemID s16";

    // Columns whose names aren't known, by hash. The wiki guide names the styles; the copy of UniqueID always matches it in vanilla.
    public const uint UniqueIdCopy = 0x504428b5;
    public const uint Style1 = 0x5ef86f1f;
    public const uint Style2 = 0x195815cf;
}

/// <summary>The AmiiboData columns.</summary>
public static class AmiiboColumns
{
    public const string CharacterId = "CharacterId u32";
    public const string NumberingId = "NumberingId u16";
    public const string SpecialNpcCloth = "SpNpcCloth s16";
    public const string NfpType = "NfpType u8";
    public const string NpcLabel = "NpcLabel string8";
    public const string NpcType = "NpcType u8";
    public const string Reaction = "Reaction u8";
    public const string SeriesId = "SeriesId u8";
    public const string Studio = "Studio u8";
}

public static class TableExtensions
{
    public static int ColumnIndex(this BinaryCSV table, string column) => table.ColumnIndex(column.ToCRC32(), column);

    public static int ColumnIndex(this BinaryCSV table, uint hash) => table.ColumnIndex(hash, $"0x{hash:x8}");

    /// <summary>A copy of a row that can be edited without changing the table, including its bit fields.</summary>
    public static object[] CopyRow(this object[] row) => [.. row.Select(cell => cell is byte[] bytes ? bytes.ToArray() : cell)];

    private static int ColumnIndex(this BinaryCSV table, uint hash, string name)
    {
        var index = Array.FindIndex(table.Fields, field => field.Hash == hash);
        return index >= 0 ? index : throw new KeyNotFoundException($"The table has no {name} column.");
    }
}
