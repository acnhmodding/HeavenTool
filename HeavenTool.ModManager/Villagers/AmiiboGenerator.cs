#nullable enable

using HeavenTool.IO.FileFormats.BCSV;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeavenTool.ModManager.Villagers;

/// <summary>The IDs that tie an amiibo to a villager: the AmiiboData row and the figure's own ID.</summary>
public sealed record AmiiboIds(uint CharacterId, ushort NumberingId)
{
    /// <summary>The character ID as the figure stores it, for emuiibo's <c>game_character_id</c>.</summary>
    [JsonIgnore]
    public ushort GameCharacterId
    {
        get
        {
            var value = CharacterId >> 4;
            return (ushort)(((value & 0xFFF) << 4) | ((value >> 12) & 0xF));
        }
    }
}

/// <summary>
/// Makes an amiibo for a new villager, so it can be invited by scanning it at Resident Services: an AmiiboData row and an
/// emuiibo virtual amiibo. A port of the wiki's amiibo generator, which picks a slot past the vanilla villagers.
/// </summary>
public static class AmiiboGenerator
{
    public const uint FirstCharacterId = 0xA2000;
    public const ushort LastVanillaNumberingId = 983;
    public const int SlotCount = 223;

    // emuiibo's series and figure type for Animal Crossing villager cards.
    private const int Series = 5;
    private const int FigureType = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, IndentSize = 2 };

    /// <summary>The slots whose character and numbering IDs no row in the table uses yet.</summary>
    public static IEnumerable<AmiiboIds> FreeSlots(BinaryCSV amiiboData)
    {
        var characterColumn = amiiboData.ColumnIndex(AmiiboColumns.CharacterId);
        var numberingColumn = amiiboData.ColumnIndex(AmiiboColumns.NumberingId);
        var characters = amiiboData.Entries.Select(row => (uint)row[characterColumn]).ToHashSet();
        var numberings = amiiboData.Entries.Select(row => (ushort)row[numberingColumn]).ToHashSet();

        return Enumerable.Range(1, SlotCount)
            .Select(slot => new AmiiboIds(FirstCharacterId + (uint)(256 * slot), (ushort)(LastVanillaNumberingId + slot)))
            .Where(ids => !characters.Contains(ids.CharacterId) && !numberings.Contains(ids.NumberingId));
    }

    /// <summary>
    /// A random free slot, or null when none is left. Random, as in the wiki's generator, so two modders' villagers are
    /// unlikely to share one.
    /// </summary>
    public static AmiiboIds? PickFree(BinaryCSV amiiboData, Random? random = null)
    {
        var free = FreeSlots(amiiboData).ToList();
        return free.Count == 0 ? null : free[(random ?? Random.Shared).Next(free.Count)];
    }

    /// <summary>A villager card's AmiiboData row for the given villager, with the values every vanilla villager card has.</summary>
    public static object[] Row(BinaryCSV amiiboData, AmiiboIds ids, string label)
    {
        var row = amiiboData.Fields.Select(field => field.GetFieldDefaultValue()).ToArray();
        row[amiiboData.ColumnIndex(AmiiboColumns.CharacterId)] = ids.CharacterId;
        row[amiiboData.ColumnIndex(AmiiboColumns.NumberingId)] = ids.NumberingId;
        row[amiiboData.ColumnIndex(AmiiboColumns.SpecialNpcCloth)] = (short)-1;
        row[amiiboData.ColumnIndex(AmiiboColumns.NfpType)] = (byte)1;
        row[amiiboData.ColumnIndex(AmiiboColumns.NpcLabel)] = label;
        row[amiiboData.ColumnIndex(AmiiboColumns.SeriesId)] = (byte)Series;
        return row;
    }

    /// <summary>
    /// Writes an emuiibo virtual amiibo to <paramref name="folder"/> (one folder in <c>emuiibo/amiibo</c>): <c>amiibo.json</c>
    /// and the empty <c>amiibo.flag</c> that marks it. Emuiibo makes the Mii and a random UUID itself.
    /// </summary>
    public static void WriteEmuiibo(string folder, string name, AmiiboIds ids, DateOnly date, Random? random = null)
    {
        random ??= Random.Shared;
        var writeDate = new EmuiiboDate(date.Year, date.Month, date.Day);
        // An NFC tag ID is 7 bytes, padded to 10.
        var uuid = new byte[10];
        random.NextBytes(uuid.AsSpan(0, 7));

        var amiibo = new EmuiiboAmiibo(
            name,
            WriteCounter: 0,
            Version: 0,
            MiiCharinfoFile: "mii-charinfo.bin",
            FirstWriteDate: writeDate,
            LastWriteDate: writeDate,
            Id: new EmuiiboId(ids.GameCharacterId, CharacterVariant: 0, FigureType, Series, ids.NumberingId),
            UseRandomUuid: true,
            Uuid: [.. uuid.Select(value => (int)value)]);

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "amiibo.json"), JsonSerializer.Serialize(amiibo, JsonOptions) + Environment.NewLine);
        File.WriteAllBytes(Path.Combine(folder, "amiibo.flag"), []);
    }

    private sealed record EmuiiboAmiibo(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("write_counter")] int WriteCounter,
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("mii_charinfo_file")] string MiiCharinfoFile,
        [property: JsonPropertyName("first_write_date")] EmuiiboDate FirstWriteDate,
        [property: JsonPropertyName("last_write_date")] EmuiiboDate LastWriteDate,
        [property: JsonPropertyName("id")] EmuiiboId Id,
        [property: JsonPropertyName("use_random_uuid")] bool UseRandomUuid,
        [property: JsonPropertyName("uuid")] int[] Uuid);

    private sealed record EmuiiboDate(
        [property: JsonPropertyName("y")] int Year,
        [property: JsonPropertyName("m")] int Month,
        [property: JsonPropertyName("d")] int Day);

    private sealed record EmuiiboId(
        [property: JsonPropertyName("game_character_id")] int GameCharacterId,
        [property: JsonPropertyName("character_variant")] int CharacterVariant,
        [property: JsonPropertyName("figure_type")] int FigureType,
        [property: JsonPropertyName("series")] int Series,
        [property: JsonPropertyName("model_number")] int ModelNumber);
}
