#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO.FileFormats.BCSV;
using System.Numerics;

namespace HeavenTool.ModManager.Villagers;

/// <summary>A wall, roof or door a house can use. <see cref="Shape"/> is the house shape it fits, or -1 when it fits any.</summary>
public sealed record HousePart(int Id, string Name, string ModelName, int Shape)
{
    public override string ToString() => Name;
}

/// <summary>The parts a villager house can be built from, with their English names and model names.</summary>
public sealed class HouseCatalog
{
    private const string ModelColumn = "ModelName string32";
    private const string ShapeColumn = "HouseShapeUniqueID u16";

    public const string StructureParamsPath = "Param/Structure/StructureParams.byml";
    // The param names hash to these keys: the Structure object, and its mNpcHouseCurtainColorArray.
    private const string StructureKey = "5f4e8251";
    private const string CurtainColorsKey = "7f0e002d";

    /// <summary>The house shapes vanilla villagers use. Other shapes are player houses and buildings.</summary>
    public required IReadOnlyList<int> Shapes { get; init; }
    public required IReadOnlyList<HousePart> Walls { get; init; }
    public required IReadOnlyList<HousePart> Roofs { get; init; }
    public required IReadOnlyList<HousePart> Doors { get; init; }

    /// <summary>
    /// The curtain colors as linear RGBA, by <see cref="HouseExterior.Curtain"/>; empty when StructureParams doesn't list them.
    /// The game tints the wall model's <c>mCurtain</c> material with them.
    /// </summary>
    public required IReadOnlyList<Vector4> CurtainColors { get; init; }

    public static HouseCatalog Load(VillagerGameData data)
    {
        var shapes = data.StaticParam.Files
            .Where(file => file.Name.StartsWith("Param/NpcHouse/", StringComparison.Ordinal))
            .Select(file => data.ReadParam(file.Name) is { } root ? HouseExterior.Read(root).Shape : -1)
            .Where(shape => shape >= 0)
            .Distinct()
            .Order()
            .ToList();

        return new HouseCatalog
        {
            Shapes = shapes,
            Walls = Parts(data, "StructureHouseWallParam", "STR_HouseWallName.msbt", hasShape: true),
            Roofs = Parts(data, "StructureHouseRoofParam", "STR_HouseRoofName.msbt", hasShape: true),
            Doors = Parts(data, "StructureHouseDoorParam", "STR_HouseDoorName.msbt", hasShape: false),
            CurtainColors = data.ReadParam(StructureParamsPath) is { } structure ? ReadCurtainColors(structure) : [],
        };
    }

    /// <summary>Reads the curtain colors in a StructureParams file's root node, or none when it doesn't hold them.</summary>
    public static IReadOnlyList<Vector4> ReadCurtainColors(IBymlNode root)
    {
        if (root is not BymlDictionary rootDictionary || !rootDictionary.TryGetValue(StructureKey, out var body)
            || body is not BymlDictionary structure || !structure.TryGetValue(CurtainColorsKey, out var list) || list is not BymlArray colors)
            return [];
        return colors.All(color => color is BymlArray { Count: 4 } channels && channels.All(channel => channel is BymlSingleValue))
            ? [.. colors.Select(color => new Vector4([.. ((BymlArray)color).Select(channel => ((BymlSingleValue)channel).Value)]))]
            : [];
    }

    public IEnumerable<HousePart> WallsFor(int shape) => Walls.Where(part => part.Shape == shape);
    public IEnumerable<HousePart> RoofsFor(int shape) => Roofs.Where(part => part.Shape == shape);

    /// <summary>The model file of a part, under the romfs.</summary>
    public static string ModelPath(string romfs, HousePart part) => Path.Combine(romfs, "Model", part.ModelName + ".Nin_NX_NVN.zs");

    private static List<HousePart> Parts(VillagerGameData data, string table, string namesFile, bool hasShape)
    {
        BinaryCSV parts = data.Table(table);
        var id = parts.ColumnIndex(VillagerColumns.UniqueId);
        var model = parts.ColumnIndex(ModelColumn);
        var shape = hasShape ? parts.ColumnIndex(ShapeColumn) : -1;
        // Names are labelled by the part's UniqueID, padded to five digits.
        var names = data.Messages("String", VillagerGameData.NameLanguage, namesFile)?.Messages
            .ToDictionary(message => message.Label, message => VillagerGameData.PlainText(message.Text)) ?? [];

        // A few door rows are placeholders with no model.
        return [.. parts.Entries
            .Where(row => ((string)row[model]).Length > 0)
            .Select(row =>
            {
                var partId = (ushort)row[id];
                var modelName = (string)row[model];
                var name = names.GetValueOrDefault(partId.ToString("00000")) is { Length: > 0 } text ? text : modelName;
                return new HousePart(partId, name, modelName, shape >= 0 ? (ushort)row[shape] : -1);
            })
            .OrderBy(part => part.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
