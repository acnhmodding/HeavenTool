#nullable enable

using HeavenTool.ModManager.NookKit;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HeavenTool.ModManager.Villagers;

/// <summary>
/// Everything the villager creator collects for one new villager, saved next to the generated files as
/// <c>&lt;label&gt;.villager.json</c> so it can be reopened and generated again.
/// </summary>
public sealed class VillagerProject
{
    public const string Extension = ".villager.json";
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>The vanilla villager the new one started from. Its messages lend their attributes to the new ones.</summary>
    public string Donor { get; set; } = "";

    public string Label { get; set; } = "";

    /// <summary>Text by language, plus <see cref="MessageEntry.DefaultLanguage"/> for every language left out.</summary>
    public Dictionary<string, string> Name { get; set; } = [];
    public Dictionary<string, string> Catchphrase { get; set; } = [];
    public Dictionary<string, string> Motto { get; set; } = [];

    /// <summary>The villager's NmlNpcParam row, in NookKit's row format.</summary>
    public JsonObject Row { get; set; } = [];

    public HouseExterior? House { get; set; }

    /// <summary>The villager whose room file the new one starts from. Its other values, such as where the villager crafts, are kept.</summary>
    public string RoomFrom { get; set; } = "";

    /// <summary>The room's wallpaper, flooring, music and furniture, or null to keep <see cref="RoomFrom"/>'s as they are.</summary>
    public VillagerRoom? Room { get; set; }

    /// <summary>The villager whose Gmo file is copied. It belongs to the model, so a villager of the same species fits best.</summary>
    public string GmoFrom { get; set; } = "";

    /// <summary>The amiibo IDs, or null to make no amiibo.</summary>
    public AmiiboIds? Amiibo { get; set; }

    /// <summary>A new project that copies a vanilla villager, with the next free label and UniqueID of its species.</summary>
    public static VillagerProject FromDonor(VillagerGameData data, VillagerInfo donor)
    {
        var table = data.NmlNpcParam;
        var label = VillagerLabel.NextFree(VillagerLabel.Species(donor.Label), data.Villagers.Select(villager => villager.Label)) ?? donor.Label;
        var uniqueId = table.ColumnIndex(VillagerColumns.UniqueId);
        var nextId = (ushort)(table.Entries.Max(row => (ushort)row[uniqueId]) + 1);

        var row = table.Entries[donor.Row].CopyRow();
        row[uniqueId] = nextId;
        row[table.ColumnIndex(VillagerColumns.UniqueIdCopy)] = nextId;
        row[table.ColumnIndex(VillagerColumns.Label)] = label;
        row[table.ColumnIndex(VillagerColumns.ResName)] = VillagerLabel.ModelName(label);

        return new VillagerProject
        {
            Donor = donor.Label,
            Label = label,
            Name = new() { [MessageEntry.DefaultLanguage] = "" },
            Catchphrase = new() { [MessageEntry.DefaultLanguage] = DonorText(data, "String", "Npc/STR_NNpcPhrase.msbt", donor.Label) },
            Motto = new() { [MessageEntry.DefaultLanguage] = DonorText(data, "TalkSys", "SYS_Motto.msbt", donor.Label) },
            Row = BcsvPatch.Row(table, row),
            House = data.ReadParam(HouseExterior.PathFor(donor.Label)) is { } house ? HouseExterior.Read(house) : null,
            RoomFrom = donor.Label,
            Room = data.ReadParam(VillagerGenerator.RoomPath(donor.Label)) is { } room ? VillagerRoom.Read(room) : null,
            GmoFrom = donor.Label,
            Amiibo = AmiiboGenerator.PickFree(data.AmiiboData),
        };
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, NookKitJson.Options) + Environment.NewLine);
    }

    public static VillagerProject Load(string path)
    {
        var project = JsonSerializer.Deserialize<VillagerProject>(File.ReadAllText(path), NookKitJson.Options)
            ?? throw new JsonException($"{Path.GetFileName(path)} is empty.");
        if (project.Version > CurrentVersion)
            throw new JsonException($"{Path.GetFileName(path)} was made by a newer Heaven Tool.");
        return project;
    }

    private static string DonorText(VillagerGameData data, string archive, string file, string label) =>
        data.MessageText(archive, VillagerGameData.NameLanguage, file, label) ?? "";
}
