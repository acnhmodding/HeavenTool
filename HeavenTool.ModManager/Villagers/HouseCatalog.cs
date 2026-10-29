#nullable enable

using HeavenTool.IO.FileFormats.BCSV;

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

    /// <summary>The house shapes vanilla villagers use. Other shapes are player houses and buildings.</summary>
    public required IReadOnlyList<int> Shapes { get; init; }
    public required IReadOnlyList<HousePart> Walls { get; init; }
    public required IReadOnlyList<HousePart> Roofs { get; init; }
    public required IReadOnlyList<HousePart> Doors { get; init; }

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
        };
    }

    public IEnumerable<HousePart> WallsFor(int shape) => Walls.Where(part => part.Shape == shape);
    public IEnumerable<HousePart> RoofsFor(int shape) => Roofs.Where(part => part.Shape == shape);

    private static List<HousePart> Parts(VillagerGameData data, string table, string namesFile, bool hasShape)
    {
        BinaryCSV parts = data.Table(table);
        var id = parts.ColumnIndex(VillagerColumns.UniqueId);
        var model = parts.ColumnIndex(ModelColumn);
        var shape = hasShape ? parts.ColumnIndex(ShapeColumn) : -1;
        // Names are labelled by the part's UniqueID, padded to five digits.
        var names = data.Messages("String", VillagerGameData.NameLanguage, namesFile)?.Messages
            .ToDictionary(message => message.Label, message => VillagerGameData.PlainText(message.Text)) ?? [];

        return [.. parts.Entries
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
