#nullable enable

using HeavenTool.ModManager.NookKit;

namespace HeavenTool.ModManager.Villagers;

/// <summary>A file the generator wrote, with what it's for.</summary>
public sealed record GeneratedFile(string Path, string Description);

/// <summary>The NookKit patches for one villager, before they're written.</summary>
public sealed record VillagerPatches(IReadOnlyList<MessagePatch> Messages, IReadOnlyList<BcsvPatch> Tables, IReadOnlyList<PackPatch> Params);

/// <summary>
/// Turns a <see cref="VillagerProject"/> into NookKit patch files: the villager's messages, its NmlNpcParam row (and AmiiboData
/// row), and its house, room and Gmo params. The model (<c>Model/NpcNml&lt;Label&gt;.Nin_NX_NVN.zs</c>) isn't made here.
/// </summary>
public static class VillagerGenerator
{
    private const string NameFile = "STR_NNpcName";
    private const string PhraseFile = "STR_NNpcPhrase";
    private const string MottoFile = "SYS_Motto";

    /// <summary>What stops the project from being generated. Empty when it's ready.</summary>
    public static IReadOnlyList<string> Validate(VillagerGameData data, VillagerProject project)
    {
        var problems = new List<string>();
        var vanilla = data.Villagers.ToDictionary(villager => villager.Label);

        if (!VillagerLabel.IsValid(project.Label))
            problems.Add("The label must be a three-letter species code and two digits, such as wol13.");
        else if (vanilla.ContainsKey(project.Label))
            problems.Add($"{project.Label} is already {vanilla[project.Label].Name}'s label.");

        if (!vanilla.ContainsKey(project.Donor))
            problems.Add($"The villager it's based on ({project.Donor}) isn't in this romfs.");
        if (string.IsNullOrWhiteSpace(DefaultText(project.Name)))
            problems.Add("The villager needs a name.");

        var table = data.NmlNpcParam;
        var uniqueId = table.ColumnIndex(VillagerColumns.UniqueId);
        var row = BcsvPatch.ReadRow(table, project.Row);
        if (table.Entries.Any(entry => (ushort)entry[uniqueId] == (ushort)row[uniqueId]))
            problems.Add($"UniqueID {row[uniqueId]} is already used by a vanilla villager.");

        if (project.House == null)
            problems.Add("The house exterior isn't set.");
        if (data.ReadParam(RoomPath(project.RoomFrom)) == null)
            problems.Add($"There's no room to copy from {project.RoomFrom}.");
        if (data.ReadParam(GmoPath(project.GmoFrom)) == null)
            problems.Add($"There's no Gmo file to copy from {project.GmoFrom}.");

        if (project.Amiibo is { } ids && !AmiiboGenerator.FreeSlots(data.AmiiboData).Contains(ids))
            problems.Add("The amiibo IDs are already taken. Pick new ones.");

        return problems;
    }

    public static VillagerPatches Build(VillagerGameData data, VillagerProject project)
    {
        var label = project.Label;
        var like = MessageAttributes.LikeLabel(project.Donor);
        MessagePatch Message(string file, string archive, Dictionary<string, string> text) =>
            new(file, archive, [new MessageEntry(label, Text(text), like)]);

        var table = data.NmlNpcParam;
        var row = BcsvPatch.ReadRow(table, project.Row);
        // The label and the copy of UniqueID always follow the project, whatever the row says.
        row[table.ColumnIndex(VillagerColumns.Label)] = label;
        row[table.ColumnIndex(VillagerColumns.UniqueIdCopy)] = row[table.ColumnIndex(VillagerColumns.UniqueId)];

        List<BcsvPatch> tables = [new("NmlNpcParam", [BcsvPatch.Row(table, row)])];
        if (project.Amiibo is { } ids)
            tables.Add(new("AmiiboData", [BcsvPatch.Row(data.AmiiboData, AmiiboGenerator.Row(data.AmiiboData, ids, label))]));

        var donorHouse = data.ReadParam(HouseExterior.PathFor(project.Donor)) ?? throw new FileNotFoundException($"{project.Donor} has no house file.");
        var room = data.ReadParam(RoomPath(project.RoomFrom)) ?? throw new FileNotFoundException($"{project.RoomFrom} has no room file.");
        var gmo = data.ReadParam(GmoPath(project.GmoFrom)) ?? throw new FileNotFoundException($"{project.GmoFrom} has no Gmo file.");
        var house = project.House ?? HouseExterior.Read(donorHouse);

        return new VillagerPatches(
            [
                Message(NameFile, "String", project.Name),
                Message(PhraseFile, "String", project.Catchphrase),
                Message(MottoFile, "TalkSys", project.Motto),
            ],
            tables,
            [
                PackPatch.FromByml(HouseExterior.PathFor(label), VillagerGameData.StaticParamArchive, house.Write(donorHouse, label)),
                PackPatch.FromByml(RoomPath(label), VillagerGameData.StaticParamArchive,
                    project.Room?.Write(room, label) ?? HouseExterior.Rekey(room, label)),
                // Gmo files all share one root key, so the copy needs no changes.
                PackPatch.FromByml(GmoPath(label), VillagerGameData.StaticParamArchive, gmo),
            ]);
    }

    /// <summary>
    /// Writes the villager to <paramref name="output"/>: the NookKit patches under <c>romfs/NookKit</c>, the emuiibo amiibo
    /// under <c>emuiibo/amiibo</c> when the project has one, and the project file itself.
    /// </summary>
    public static IReadOnlyList<GeneratedFile> Generate(VillagerGameData data, VillagerProject project, string output)
    {
        var problems = Validate(data, project);
        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems));

        var patches = Build(data, project);
        var romfs = Path.Combine(output, "romfs");
        var files = new List<GeneratedFile>();

        void Write<T>(NookKitKind kind, IReadOnlyList<T> list, string description)
        {
            var path = NookKitJson.PathFor(romfs, kind, project.Label);
            NookKitJson.Write(path, list);
            files.Add(new GeneratedFile(path, description));
        }

        Write(NookKitKind.Message, patches.Messages, "Name, catchphrase and motto");
        Write(NookKitKind.Bcsv, patches.Tables, project.Amiibo != null ? "NmlNpcParam and AmiiboData rows" : "NmlNpcParam row");
        Write(NookKitKind.Pack, patches.Params, "House, room and Gmo params");

        if (project.Amiibo is { } ids)
        {
            var name = DefaultText(project.Name);
            var folder = Path.Combine(output, "emuiibo", "amiibo", SafeFolderName(name, project.Label));
            AmiiboGenerator.WriteEmuiibo(folder, name, ids, DateOnly.FromDateTime(DateTime.Today));
            files.Add(new GeneratedFile(Path.Combine(folder, "amiibo.json"), "Emuiibo amiibo"));
        }

        var projectPath = Path.Combine(output, project.Label + VillagerProject.Extension);
        project.Save(projectPath);
        files.Add(new GeneratedFile(projectPath, "Villager Creator project"));
        return files;
    }

    public static string RoomPath(string label) => $"Param/NpcRoom/{label}.byml";
    public static string GmoPath(string label) => $"Param/Gmo/Gmo_{label}.byml";

    /// <summary>The text for <see cref="MessageEntry.DefaultLanguage"/>, falling back to the first language that has some.</summary>
    public static string DefaultText(IReadOnlyDictionary<string, string> text) =>
        text.GetValueOrDefault(MessageEntry.DefaultLanguage) is { Length: > 0 } value
            ? value
            : text.Values.FirstOrDefault(value => value.Length > 0) ?? "";

    // The languages that have text, in the game's order, then the default for the rest.
    private static Dictionary<string, string> Text(IReadOnlyDictionary<string, string> text)
    {
        var result = new Dictionary<string, string>();
        foreach (var language in VillagerGameData.Languages)
            if (text.GetValueOrDefault(language) is { Length: > 0 } value)
                result[language] = MessageText.Escape(value);
        result[MessageEntry.DefaultLanguage] = MessageText.Escape(DefaultText(text));
        return result;
    }

    private static string SafeFolderName(string name, string fallback)
    {
        var safe = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
        return safe.Length > 0 ? safe : fallback;
    }
}
