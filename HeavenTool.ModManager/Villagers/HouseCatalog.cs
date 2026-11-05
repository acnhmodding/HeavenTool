#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO.FileFormats.BCSV;
using System.Numerics;

namespace HeavenTool.ModManager.Villagers;

/// <summary>
/// A wall, roof or door a house can use. <see cref="Shape"/> is the house shape it fits, or -1 when it fits any. A door's
/// <see cref="Top"/> is the CRC32 of the shape of its top, "Square" or "Round", and 0 for walls and roofs.
/// </summary>
public sealed record HousePart(int Id, string Name, string ModelName, int Shape, uint Top = 0)
{
    public override string ToString() => Name;
}

/// <summary>The parts a villager house can be built from, with their English names and model names.</summary>
public sealed class HouseCatalog
{
    private const string ModelColumn = "ModelName string32";
    private const string ShapeColumn = "HouseShapeUniqueID u16";
    // The door's top, square or round. Its name isn't known.
    private const uint DoorTopColumn = 0x0aa76edd;

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
    /// The door tops each shape takes, from the vanilla houses: its walls' doorways are square or round, and a door with the
    /// other top doesn't fit them. No table says this, but no vanilla house mixes them.
    /// </summary>
    public required IReadOnlyDictionary<int, IReadOnlySet<uint>> DoorTops { get; init; }

    /// <summary>
    /// The curtain colors as linear RGBA, by <see cref="HouseExterior.Curtain"/>; empty when StructureParams doesn't list them.
    /// The game tints the wall model's <c>mCurtain</c> material with them.
    /// </summary>
    public required IReadOnlyList<Vector4> CurtainColors { get; init; }

    public static HouseCatalog Load(VillagerGameData data)
    {
        var houses = data.StaticParam.Files
            .Where(file => file.Name.StartsWith("Param/NpcHouse/", StringComparison.Ordinal))
            .Select(file => data.ReadParam(file.Name) is { } root ? HouseExterior.Read(root) : null)
            .OfType<HouseExterior>()
            .Where(house => house.Shape >= 0)
            .ToList();
        var doors = Parts(data, "StructureHouseDoorParam", "STR_HouseDoorName.msbt", hasShape: false);
        var doorTops = doors.ToDictionary(door => door.Id, door => door.Top);

        return new HouseCatalog
        {
            Shapes = [.. houses.Select(house => house.Shape).Distinct().Order()],
            Walls = Parts(data, "StructureHouseWallParam", "STR_HouseWallName.msbt", hasShape: true),
            Roofs = Parts(data, "StructureHouseRoofParam", "STR_HouseRoofName.msbt", hasShape: true),
            Doors = doors,
            DoorTops = houses
                .Where(house => doorTops.ContainsKey(house.Door))
                .GroupBy(house => house.Shape)
                .ToDictionary(group => group.Key, group => (IReadOnlySet<uint>)group.Select(house => doorTops[house.Door]).ToHashSet()),
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

    /// <summary>The doors that fit a shape's doorway, or all of them for a shape no vanilla house has.</summary>
    public IEnumerable<HousePart> DoorsFor(int shape) =>
        DoorTops.TryGetValue(shape, out var tops) ? Doors.Where(part => tops.Contains(part.Top)) : Doors;

    /// <summary>The model file of a part, under the romfs.</summary>
    public static string ModelPath(string romfs, HousePart part) => Path.Combine(romfs, "Model", part.ModelName + ".Nin_NX_NVN.zs");

    private static List<HousePart> Parts(VillagerGameData data, string table, string namesFile, bool hasShape)
    {
        BinaryCSV parts = data.Table(table);
        var id = parts.ColumnIndex(VillagerColumns.UniqueId);
        var model = parts.ColumnIndex(ModelColumn);
        var shape = hasShape ? parts.ColumnIndex(ShapeColumn) : -1;
        var top = hasShape ? -1 : parts.ColumnIndex(DoorTopColumn);
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
                return new HousePart(partId, name, modelName, shape >= 0 ? (ushort)row[shape] : -1, top >= 0 ? (uint)row[top] : 0);
            })
            .OrderBy(part => part.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
