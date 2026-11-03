#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using AeonSake.NintendoTools.FileFormats.Msbt;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.IO.AcnhEnvironment;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace HeavenTool.ModManager.Villagers;

/// <summary>A vanilla villager, as listed in NmlNpcParam with its English name.</summary>
public sealed record VillagerInfo(string Label, string Name, string Species, int Row);

/// <summary>A vanilla item with its English name. <paramref name="Kind"/> is the CRC32 of its ItemKind, such as <c>Tops</c>.</summary>
public sealed record ItemInfo(int Id, string Name, string Label, uint Kind);

/// <summary>
/// The space an item takes, in half tiles, before it's turned. <paramref name="Layer"/> is where an item of that size goes: on the
/// floor, on the back wall, where <paramref name="Z"/> is its height, or under everything as a rug.
/// </summary>
public readonly record struct ItemFootprint(int X, int Z, RoomLayer Layer);

/// <summary>
/// The game files the villager creator reads, loaded from the romfs on first use and cached. Everything here is read-only:
/// callers copy rows and BYML trees before editing them.
/// </summary>
public sealed partial class VillagerGameData
{
    /// <summary>The languages the game ships, as used in message archive names and NookKit's <c>text</c> keys.</summary>
    public static readonly string[] Languages =
        ["USen", "USes", "USfr", "EUen", "EUde", "EUes", "EUfr", "EUit", "EUnl", "EUru", "JPja", "KRko", "CNzh", "TWzh"];

    public const string NameLanguage = "USen";
    public const string StaticParamArchive = "StaticParam";
    // The ItemKind of room markers, which their labels also start with.
    private const string RoomMarkerKind = "NnpcRoomMarker";

    private readonly ConcurrentDictionary<string, BinaryCSV> tables = new();
    private readonly ConcurrentDictionary<(string Archive, string Language), Lazy<SarcFile>> messageArchives = new();
    private readonly Lazy<SarcFile> staticParam;
    private readonly Lazy<AcnhEnvironmentResources> environment;
    private readonly Lazy<IReadOnlyDictionary<int, string>> itemNames;
    private readonly Lazy<IReadOnlyList<VillagerInfo>> villagers;
    private readonly Lazy<IReadOnlyList<NameColor>> nameColors;
    private readonly Lazy<IReadOnlyDictionary<int, ItemRow>> itemParam;
    private readonly Lazy<IReadOnlyList<ItemInfo>> items;
    private readonly Lazy<IReadOnlyDictionary<uint, ItemFootprint>> itemSizes;
    private readonly Lazy<IReadOnlyDictionary<int, string>> roomMarkers;

    public string Romfs { get; }

    private VillagerGameData(string romfs)
    {
        Romfs = romfs;
        staticParam = new(() => ReadSarc(Path.Combine(Romfs, "Pack", StaticParamArchive + ".pack")));
        environment = new(() => AcnhEnvironmentResources.Read(ReadParam, AcnhSunOrbit.Load(Romfs)));
        itemNames = new(LoadItemNames);
        villagers = new(LoadVillagers);
        nameColors = new(() => ReadParam(NameColor.ParamPath) is { } root ? NameColor.ReadAll(root) : []);
        itemParam = new(LoadItemParam);
        items = new(() => [.. itemParam.Value
            .Where(item => ItemNames.ContainsKey(item.Key))
            .Select(item => new ItemInfo(item.Key, ItemNames[item.Key], item.Value.Label, item.Value.Kind))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Id)]);
        itemSizes = new(LoadItemSizes);
        roomMarkers = new(() => itemParam.Value
            .Where(item => item.Value.Kind == RoomMarkerKind.ToCRC32() && item.Value.Label.StartsWith(RoomMarkerKind, StringComparison.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value.Label[RoomMarkerKind.Length..]));
    }

    /// <summary>Opens a romfs folder, after checking it has the files the villager creator needs.</summary>
    public static VillagerGameData Open(string romfs)
    {
        string[] required =
        [
            "Bcsv/NmlNpcParam.bcsv",
            "Bcsv/AmiiboData.bcsv",
            $"Pack/{StaticParamArchive}.pack",
            $"Message/String_{NameLanguage}.sarc.zs",
            $"Message/TalkSys_{NameLanguage}.sarc.zs",
        ];
        var missing = required.Where(file => !File.Exists(Path.Combine(romfs, file))).ToList();
        if (missing.Count > 0)
            throw new FileNotFoundException($"The romfs folder is missing {string.Join(", ", missing)}.");
        return new VillagerGameData(romfs);
    }

    public BinaryCSV NmlNpcParam => Table("NmlNpcParam");
    public BinaryCSV AmiiboData => Table("AmiiboData");

    /// <summary>The vanilla villagers, sorted by name.</summary>
    public IReadOnlyList<VillagerInfo> Villagers => villagers.Value;

    /// <summary>English item names by item ID, from the <c>Item/STR_ItemName_*</c> messages.</summary>
    public IReadOnlyDictionary<int, string> ItemNames => itemNames.Value;

    /// <summary>The items in ItemParam that have an English name, sorted by name.</summary>
    public IReadOnlyList<ItemInfo> Items => items.Value;

    /// <summary>The colors villager names can be shown in, by NpcColor value; empty when StaticParam.pack doesn't list them.</summary>
    public IReadOnlyList<NameColor> NameColors => nameColors.Value;

    public SarcFile StaticParam => staticParam.Value;
    public AcnhEnvironmentResources Environment => environment.Value;

    /// <summary>
    /// The layout icon archive of an item, as the closet shows it or else as the furniture menus do, or null when the romfs
    /// has neither. Tools and a few other items have their icons elsewhere and get none.
    /// </summary>
    public string? ItemIconPath(int id)
    {
        if (!itemParam.Value.TryGetValue(id, out var item)) return null;
        // Most icons are named by the item's resource; photos and posters by its label, and photos in each frame.
        string[] names =
        [
            $"Layout_ClosetIcon_{item.Resource}", $"Layout_FtrIcon_{item.Resource}",
            $"Layout_FtrIcon_{item.Label}", $"Layout_FtrIcon_{item.Label}_Remake_0_0",
        ];
        return names.Select(name => Path.Combine(Romfs, "Model", name + ".Nin_NX_NVN.zs")).FirstOrDefault(File.Exists);
    }

    /// <summary>The resource name of an item, which names its model, or null when ItemParam has no such item.</summary>
    public string? ItemResource(int id) => itemParam.Value.TryGetValue(id, out var item) ? item.Resource : null;

    /// <summary>
    /// The room markers, by ID: placeholder items that hold a spot in a villager's room for the game to fill, such as with things
    /// the player gives them. Each is named by what its label says goes there, such as <c>FtrFloor2x1</c> or <c>ClothWall</c>.
    /// </summary>
    public IReadOnlyDictionary<int, string> RoomMarkers => roomMarkers.Value;

    /// <summary>The space an item takes, from its ItemSize; one tile when the romfs doesn't say.</summary>
    public ItemFootprint ItemSize(int id) =>
        itemParam.Value.TryGetValue(id, out var item) && itemSizes.Value.TryGetValue(item.Size, out var size) ? size : new(2, 2, RoomLayer.Floor);

    /// <summary>
    /// How high up an item other items stand on it, from its RideHeight. It's lower than the top of the model on furniture
    /// with shelves over its counter.
    /// </summary>
    public float ItemRideHeight(int id) => itemParam.Value.TryGetValue(id, out var item) ? item.RideHeight : 0;

    /// <summary>A table from <c>Bcsv/</c>, by name without extension.</summary>
    public BinaryCSV Table(string name) => tables.GetOrAdd(name, _ =>
    {
        using var stream = File.OpenRead(Path.Combine(Romfs, "Bcsv", name + ".bcsv"));
        return new BinaryCSV(stream);
    });

    /// <summary>A BYML file from StaticParam.pack, or null when the pack has no such file.</summary>
    public IBymlNode? ReadParam(string path)
    {
        var content = StaticParam.Files.FirstOrDefault(file => file.Name == path);
        return content == null ? null : new BymlFileReader().Read(new MemoryStream(content.Data)).RootNode;
    }

    /// <summary>
    /// One msbt from a message archive (<c>Message/&lt;archive&gt;_&lt;language&gt;.sarc.zs</c>), or null when the archive or the
    /// msbt doesn't exist. <paramref name="file"/> is the msbt's path in the archive, e.g. <c>Npc/STR_NNpcName.msbt</c>.
    /// </summary>
    public MsbtFile? Messages(string archive, string language, string file)
    {
        var sarc = messageArchives.GetOrAdd((archive, language), key => new(() =>
        {
            var path = Path.Combine(Romfs, "Message", $"{key.Archive}_{key.Language}.sarc.zs");
            return File.Exists(path) ? ReadSarc(path) : new SarcFile();
        })).Value;
        var content = sarc.Files.FirstOrDefault(entry => entry.Name == file);
        return content == null ? null : new MsbtFileReader().Read(new MemoryStream(content.Data));
    }

    /// <summary>A message's text without its tags, or null when there's no such message.</summary>
    public string? MessageText(string archive, string language, string file, string label) =>
        Messages(archive, language, file)?.Messages.FirstOrDefault(message => message.Label == label) is { } message
            ? PlainText(message.Text)
            : null;

    /// <summary>Removes the tags NintendoTools writes as <c>{{...}}</c>, such as the article tag in front of item names.</summary>
    public static string PlainText(string text) => TagPattern().Replace(text, "");

    [GeneratedRegex(@"\{\{[^}]*\}\}")]
    private static partial Regex TagPattern();

    private static SarcFile ReadSarc(string path)
    {
        using var file = File.OpenRead(path);
        if (!path.EndsWith(".zs", StringComparison.OrdinalIgnoreCase))
            return new SarcFileReader().Read(file);

        var decompressed = new MemoryStream();
        FileMerger.ZstdDecompressor.Decompress(file, decompressed);
        decompressed.Position = 0;
        return new SarcFileReader().Read(decompressed);
    }

    private Dictionary<int, string> LoadItemNames()
    {
        var names = new Dictionary<int, string>();
        var files = ReadSarc(Path.Combine(Romfs, "Message", $"String_{NameLanguage}.sarc.zs")).Files;
        IEnumerable<MsbtMessage> Read(string prefix) => files
            .Where(file => file.Name.StartsWith(prefix, StringComparison.Ordinal))
            .SelectMany(file => new MsbtFileReader().Read(new MemoryStream(file.Data)).Messages);
        static int? Id(string label) => int.TryParse(label.AsSpan(label.LastIndexOf('_') + 1), out var id) ? id : null;

        // Labels end in the item ID: "RoomWall_01057".
        foreach (var message in Read("Item/STR_ItemName_"))
            if (Id(message.Label) is { } id) names.TryAdd(id, PlainText(message.Text));

        // Clothing is named by its group, and each of the group's items by its color: "084_Tops_09223" is group 084 in green.
        var groups = Read("Outfit/GroupName/").GroupBy(message => message.Label).ToDictionary(group => group.Key, group => PlainText(group.First().Text));
        foreach (var variants in Read("Outfit/GroupColor/").GroupBy(message => message.Label[..message.Label.IndexOf('_')]))
        {
            if (!groups.TryGetValue(variants.Key, out var group)) continue;
            // A group with one item has no color to tell it apart; its message just says so.
            var single = variants.Count() == 1;
            foreach (var message in variants)
                if (Id(message.Label) is { } id) names.TryAdd(id, single ? group : $"{group} ({PlainText(message.Text)})");
        }
        return names;
    }

    // An item's resource name and label, which name its models and icons, its kind, the CRC32 of its ItemSize label and
    // the height other items stand on it.
    private sealed record ItemRow(string Resource, string Label, uint Kind, uint Size, float RideHeight);

    private Dictionary<int, ItemRow> LoadItemParam()
    {
        if (!File.Exists(Path.Combine(Romfs, "Bcsv", "ItemParam.bcsv"))) return [];
        var table = Table("ItemParam");
        int id = table.ColumnIndex("UniqueID u16"), resource = table.ColumnIndex("ResName string64");
        int label = table.ColumnIndex("Label string50"), kind = table.ColumnIndex("ItemKind.hshCstringRef");
        int size = table.ColumnIndex("ItemSize.hshCstringRef");
        int rideHeight = table.ColumnIndex("RideHeight f32");
        return table.Entries.ToDictionary(row => Convert.ToInt32(row[id]),
            row => new ItemRow((string)row[resource], (string)row[label], Convert.ToUInt32(row[kind]), Convert.ToUInt32(row[size]), Convert.ToSingle(row[rideHeight])));
    }

    // ItemParam names an item's size by the CRC32 of its row's label here, such as 2_0x1_0.
    private Dictionary<uint, ItemFootprint> LoadItemSizes()
    {
        if (!File.Exists(Path.Combine(Romfs, "Bcsv", "ItemSize.bcsv"))) return [];
        var table = Table("ItemSize");
        int label = table.ColumnIndex("Label string32"), x = table.ColumnIndex("HalfUnitSizeX u8"), z = table.ColumnIndex("HalfUnitSizeZ u8");
        static RoomLayer Layer(string name) =>
            name.EndsWith("_Wall", StringComparison.Ordinal) ? RoomLayer.BackWall : name.EndsWith("_Rug", StringComparison.Ordinal) ? RoomLayer.Rug : RoomLayer.Floor;
        return table.Entries.ToDictionary(row => ((string)row[label]).ToCRC32(),
            row => new ItemFootprint(Convert.ToInt32(row[x]), Convert.ToInt32(row[z]), Layer((string)row[label])));
    }

    private IReadOnlyList<VillagerInfo> LoadVillagers()
    {
        var table = NmlNpcParam;
        var label = table.ColumnIndex(VillagerColumns.Label);
        var names = Messages("String", NameLanguage, "Npc/STR_NNpcName.msbt")?.Messages.ToDictionary(message => message.Label, message => PlainText(message.Text)) ?? [];
        var species = Messages("String", NameLanguage, "STR_Race.msbt")?.Messages.ToDictionary(message => message.Label, message => PlainText(message.Text)) ?? [];

        return [.. table.Entries
            .Select((row, index) =>
            {
                var id = (string)row[label];
                var prefix = VillagerLabel.Species(id);
                var speciesName = species.GetValueOrDefault(prefix + "_M") ?? species.GetValueOrDefault(prefix + "_F") ?? prefix;
                return new VillagerInfo(id, names.GetValueOrDefault(id) ?? id, speciesName, index);
            })
            .OrderBy(villager => villager.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
