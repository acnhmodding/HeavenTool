#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using AeonSake.NintendoTools.FileFormats.Msbt;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.FileFormats.BCSV;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace HeavenTool.ModManager.Villagers;

/// <summary>A vanilla villager, as listed in NmlNpcParam with its English name.</summary>
public sealed record VillagerInfo(string Label, string Name, string Species, int Row);

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

    private readonly ConcurrentDictionary<string, BinaryCSV> tables = new();
    private readonly ConcurrentDictionary<(string Archive, string Language), Lazy<SarcFile>> messageArchives = new();
    private readonly Lazy<SarcFile> staticParam;
    private readonly Lazy<IReadOnlyDictionary<int, string>> itemNames;
    private readonly Lazy<IReadOnlyList<VillagerInfo>> villagers;
    private readonly Lazy<IReadOnlyList<NameColor>> nameColors;

    public string Romfs { get; }

    private VillagerGameData(string romfs)
    {
        Romfs = romfs;
        staticParam = new(() => ReadSarc(Path.Combine(Romfs, "Pack", StaticParamArchive + ".pack")));
        itemNames = new(LoadItemNames);
        villagers = new(LoadVillagers);
        nameColors = new(() => ReadParam(NameColor.ParamPath) is { } root ? NameColor.ReadAll(root) : []);
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

    /// <summary>The colors villager names can be shown in, by NpcColor value; empty when StaticParam.pack doesn't list them.</summary>
    public IReadOnlyList<NameColor> NameColors => nameColors.Value;

    public SarcFile StaticParam => staticParam.Value;

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

    private IReadOnlyDictionary<int, string> LoadItemNames()
    {
        var names = new Dictionary<int, string>();
        var path = Path.Combine(Romfs, "Message", $"String_{NameLanguage}.sarc.zs");
        foreach (var content in ReadSarc(path).Files.Where(file => file.Name.StartsWith("Item/STR_ItemName_", StringComparison.Ordinal)))
        {
            // Labels end in the item ID: "RoomWall_01057".
            foreach (var message in new MsbtFileReader().Read(new MemoryStream(content.Data)).Messages)
                if (int.TryParse(message.Label.AsSpan(message.Label.LastIndexOf('_') + 1), out var id))
                    names.TryAdd(id, PlainText(message.Text));
        }
        return names;
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
