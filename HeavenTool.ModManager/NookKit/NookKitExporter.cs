#nullable enable

using AeonSake.NintendoTools.Compression.Zstd;
using AeonSake.NintendoTools.FileFormats.Byml;
using AeonSake.NintendoTools.FileFormats.Msbt;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.IO.FileFormats.BYML;
using HeavenTool.ModManager.Library;
using HeavenTool.ModManager.Villagers;
using System.Collections;
using System.Text.Json.Nodes;

namespace HeavenTool.ModManager.NookKit;

/// <summary>What one mod's export wrote, and what it couldn't write as NookKit patches.</summary>
public sealed record NookKitModExport(InstalledMod Mod, string Name, IReadOnlyList<string> Files, IReadOnlyList<string> Notes);

/// <summary>
/// Writes what a library mod changes in the game's tables, messages and pack files as NookKit patches: changed and new bcsv rows,
/// msbt messages and pack BYML files, found by comparing the mod's romfs with the game's. Files a mod adds to a pack are written
/// as loose files, since pack patches only replace the pack's own.
/// </summary>
public sealed class NookKitExporter(string vanillaRomfs)
{
    private const string BcsvFolder = "Bcsv", MessageFolder = "Message", PackFolder = "Pack";

    private readonly Dictionary<string, SarcFile?> vanillaArchives = new(StringComparer.OrdinalIgnoreCase);
    // The loose files written so far, since two mods can add the same one.
    private readonly HashSet<string> written = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Exports each mod to <c>&lt;output&gt;/NookKit/&lt;kind&gt;/&lt;name&gt;.json</c>, one file per kind that has changes, and the files it
    /// adds to packs at their path in <paramref name="output"/>. Mods with the same name get a number after it.
    /// </summary>
    public List<NookKitModExport> Export(IEnumerable<InstalledMod> mods, string output, CancellationToken cancellation = default)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<NookKitModExport>();
        foreach (var mod in mods)
        {
            cancellation.ThrowIfCancellationRequested();
            var name = FileName(mod);
            for (var i = 2; !used.Add(name); i++) name = $"{FileName(mod)} ({i})";
            results.Add(Export(mod, output, name));
        }
        return results;
    }

    public NookKitModExport Export(InstalledMod mod, string output, string name)
    {
        var notes = new List<string>();
        var files = new List<string>();
        var romfs = Path.Combine(mod.Folder, "romfs");
        if (!Directory.Exists(romfs))
            return new(mod, name, files, ["Has no romfs files."]);

        var bcsv = new List<BcsvPatch>();
        var messages = new MessageChanges();
        var packs = new List<PackPatch>();
        var loose = new List<LooseFile>();
        var others = new List<string>();

        foreach (var path in Directory.EnumerateFiles(romfs, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(romfs, path).Replace('\\', '/');
            var folder = relative.Contains('/') ? relative[..relative.IndexOf('/')] : "";
            var fileName = relative[(folder.Length + 1)..];
            try
            {
                if (folder.Equals(BcsvFolder, StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".bcsv", StringComparison.OrdinalIgnoreCase) && !fileName.Contains('/'))
                    ExportBcsv(path, relative, bcsv, notes);
                else if (folder.Equals(MessageFolder, StringComparison.OrdinalIgnoreCase) && MessageArchive(fileName) is { } archive)
                    messages.Add(this, path, relative, archive.Name, archive.Language, notes);
                else if (folder.Equals(PackFolder, StringComparison.OrdinalIgnoreCase) && PackName(fileName) is { } pack)
                    ExportPack(path, relative, pack, packs, loose, notes);
                // The resource size table is rebuilt on deploy, and NookKit patches need no entry in it.
                else if (!relative.Equals("System/Resource/ResourceSizeTable.srsizetable", StringComparison.OrdinalIgnoreCase))
                    others.Add(relative);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notes.Add($"{relative}: couldn't be read ({ex.Message}).");
            }
        }

        if (others.Count > 0)
            notes.Add($"{others.Count} file{(others.Count == 1 ? "" : "s")} that NookKit can't patch, so the mod still needs them: " +
                string.Join(", ", others.Take(8)) + (others.Count > 8 ? ", ..." : "") + ".");

        void Write<T>(NookKitKind kind, IReadOnlyList<T> patches)
        {
            if (patches.Count == 0) return;
            var path = NookKitJson.PathFor(output, kind, name);
            NookKitJson.Write(path, patches);
            files.Add(path);
        }
        Write(NookKitKind.Bcsv, bcsv);
        Write(NookKitKind.Message, messages.Build(notes));
        Write(NookKitKind.Pack, packs);
        foreach (var file in loose)
        {
            if (!written.Add(file.Path))
                notes.Add($"{file.Path}: another mod adds it too, so this mod's copy replaces it.");
            files.Add(file.Write(output));
        }
        return new(mod, name, files, notes);
    }

    private void ExportBcsv(string path, string relative, List<BcsvPatch> patches, List<string> notes)
    {
        var vanillaPath = Path.Combine(vanillaRomfs, relative);
        if (!File.Exists(vanillaPath))
        {
            notes.Add($"{relative}: the game has no such table, and NookKit only adds rows to existing ones.");
            return;
        }

        var table = ReadBcsv(path);
        var vanilla = ReadBcsv(vanillaPath);
        if (!table.Fields.SequenceEqual(vanilla.Fields))
        {
            notes.Add($"{relative}: its columns differ from the game's, so its rows can't be matched.");
            return;
        }
        var key = Array.FindIndex(vanilla.Fields, field => BinaryCSV.UniqueHashes.Contains(field.Hash));
        if (key < 0)
        {
            notes.Add($"{relative}: the table has no unique column, so NookKit can't tell which row a change replaces.");
            return;
        }
        var vanillaRows = new Dictionary<object, object[]>();
        if (vanilla.Entries.Any(entry => !vanillaRows.TryAdd(entry[key], entry)) || table.Entries.Select(entry => entry[key]).Distinct().Count() != table.Length)
        {
            notes.Add($"{relative}: some rows share a value in the unique column, so NookKit can't tell which row a change replaces.");
            return;
        }

        var rows = table.Entries
            .Where(entry => !vanillaRows.TryGetValue(entry[key], out var original) || !StructuralComparisons.StructuralEqualityComparer.Equals(entry, original))
            .Select(entry => BcsvPatch.Row(table, entry)).ToList();
        if (rows.Count > 0) patches.Add(new(Path.GetFileNameWithoutExtension(relative), rows));

        var kept = table.Entries.Select(entry => entry[key]).ToHashSet();
        var removed = vanillaRows.Keys.Count(value => !kept.Contains(value));
        if (removed > 0)
            notes.Add($"{relative}: {removed} row{(removed == 1 ? " the mod removes stays" : "s the mod removes stay")}, since NookKit can't remove rows.");
    }

    private void ExportPack(string path, string relative, string pack, List<PackPatch> patches, List<LooseFile> loose, List<string> notes)
    {
        var sarc = ReadSarc(path);
        var vanilla = VanillaArchive(relative)?.Files.ToDictionary(file => file.Name, file => file.Data) ?? [];
        var skipped = new List<string>();
        foreach (var file in sarc.Files)
        {
            if (!vanilla.TryGetValue(file.Name, out var original))
            {
                loose.Add(new(file.Name, file.Data));
                continue;
            }
            if (original.AsSpan().SequenceEqual(file.Data)) continue;
            if (!file.Name.EndsWith(".byml", StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(file.Name);
                continue;
            }
            try
            {
                var data = BymlJson.ToJson(ReadByml(file.Data)) ?? throw new NotSupportedException("Its root is empty.");
                // Saved again by a tool, a file can change its bytes but not its contents.
                if (SameJson(data, original)) continue;
                patches.Add(new(file.Name, pack, data));
            }
            catch (NotSupportedException ex) { notes.Add($"{relative}/{file.Name}: {ex.Message}"); }
        }
        if (skipped.Count > 0)
            notes.Add($"{relative}: {skipped.Count} changed file{(skipped.Count == 1 ? " isn't" : "s aren't")} BYML, which NookKit can't patch: " +
                string.Join(", ", skipped.Take(5)) + (skipped.Count > 5 ? ", ..." : "") + ".");
    }

    private static bool SameJson(JsonNode data, byte[] original)
    {
        try { return JsonNode.DeepEquals(data, BymlJson.ToJson(ReadByml(original))); }
        catch (NotSupportedException) { return false; }
    }

    /// <summary>The game's copy of an archive in the romfs, or null when it has none. Kept for the rest of the export.</summary>
    private SarcFile? VanillaArchive(string relative)
    {
        if (vanillaArchives.TryGetValue(relative, out var cached)) return cached;
        var path = Path.Combine(vanillaRomfs, relative);
        return vanillaArchives[relative] = File.Exists(path) ? ReadSarc(path) : null;
    }

    // "String_USen.sarc.zs" is the String archive in USen.
    private static (string Name, string Language)? MessageArchive(string fileName)
    {
        if (fileName.Contains('/')) return null;
        var name = fileName.EndsWith(".zs", StringComparison.OrdinalIgnoreCase) ? fileName[..^3] : fileName;
        if (!name.EndsWith(".sarc", StringComparison.OrdinalIgnoreCase)) return null;
        name = name[..^5];
        var split = name.LastIndexOf('_');
        return split > 0 && VillagerGameData.Languages.Contains(name[(split + 1)..]) ? (name[..split], name[(split + 1)..]) : null;
    }

    private static string? PackName(string fileName)
    {
        if (fileName.Contains('/')) return null;
        var name = fileName.EndsWith(".zs", StringComparison.OrdinalIgnoreCase) ? fileName[..^3] : fileName;
        return name.EndsWith(".pack", StringComparison.OrdinalIgnoreCase) ? name[..^5] : null;
    }

    private static string FileName(InstalledMod mod)
    {
        var name = string.Concat(mod.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim().TrimEnd('.');
        return name.Length > 0 ? name : Path.GetFileName(mod.Folder);
    }

    private static BinaryCSV ReadBcsv(string path)
    {
        using var stream = Decompress(File.ReadAllBytes(path));
        return new BinaryCSV(stream);
    }

    private static SarcFile ReadSarc(string path) => new SarcFileReader().Read(Decompress(File.ReadAllBytes(path)));

    private static MsbtFile ReadMsbt(byte[] data) => new MsbtFileReader().Read(new MemoryStream(data));

    private static IBymlNode ReadByml(byte[] data) => AcnhByml.Read(data).RootNode;

    private static MemoryStream Decompress(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        if (!ZstdDecompressor.CanDecompressStatic(stream)) return stream;
        var decompressed = new MemoryStream();
        FileMerger.ZstdDecompressor.Decompress(stream, decompressed);
        decompressed.Position = 0;
        return decompressed;
    }

    /// <summary>
    /// The changed messages of one mod, collected across its message archives, since NookKit puts every language of a message in
    /// one entry.
    /// </summary>
    private sealed class MessageChanges
    {
        private sealed record Message(string Text, byte[] Attribute);

        // (archive, msbt path in the archive) -> label -> language -> the mod's message.
        private readonly Dictionary<(string Archive, string File), Dictionary<string, Dictionary<string, Message>>> changes = [];
        // The game's messages that were compared, for the attributes: (archive, msbt path, language) -> label -> message.
        private readonly Dictionary<(string Archive, string File, string Language), Dictionary<string, Message>> vanilla = [];

        public void Add(NookKitExporter exporter, string path, string relative, string archive, string language, List<string> notes)
        {
            var sarc = ReadSarc(path);
            var original = exporter.VanillaArchive(relative)?.Files.ToDictionary(file => file.Name, file => file.Data) ?? [];
            var skipped = 0;
            foreach (var file in sarc.Files)
            {
                if (original.TryGetValue(file.Name, out var data) && data.AsSpan().SequenceEqual(file.Data)) continue;
                if (!file.Name.EndsWith(".msbt", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                if (Messages(file.Data) is not { } messages)
                {
                    notes.Add($"{relative}/{file.Name}: isn't a labelled UTF-16 msbt, so its messages can't be read as NookKit text.");
                    continue;
                }
                var gameMessages = (data != null ? Messages(data) : null) ?? [];
                vanilla[(archive, file.Name, language)] = gameMessages;

                var labels = changes.TryGetValue((archive, file.Name), out var found) ? found : changes[(archive, file.Name)] = [];
                foreach (var (label, message) in messages)
                {
                    if (gameMessages.TryGetValue(label, out var before) && before.Text == message.Text && before.Attribute.AsSpan().SequenceEqual(message.Attribute))
                        continue;
                    (labels.TryGetValue(label, out var texts) ? texts : labels[label] = [])[language] = message;
                }

                var removed = gameMessages.Keys.Count(label => !messages.ContainsKey(label));
                if (removed > 0)
                    notes.Add($"{relative}/{file.Name}: {removed} message{(removed == 1 ? " the mod removes stays" : "s the mod removes stay")}, since NookKit can't remove messages.");
            }
            if (skipped > 0)
                notes.Add($"{relative}: {skipped} changed file{(skipped == 1 ? " isn't an msbt" : "s aren't msbts")}, which NookKit can't patch.");
        }

        // The text from the msbt's own UTF-16 in NookKit's tag syntax, and the attributes from NintendoTools.
        private static Dictionary<string, Message>? Messages(byte[] data)
        {
            var msbt = ReadMsbt(data);
            if (!msbt.HasLbl1 || MsbtText.Read(data) is not { } texts) return null;
            var messages = new Dictionary<string, Message>();
            foreach (var message in msbt.Messages)
                if (texts.TryGetValue(message.Label, out var text)) messages.TryAdd(message.Label, new(text, message.Attribute));
            return messages;
        }

        public List<MessagePatch> Build(List<string> notes)
        {
            var patches = new List<MessagePatch>();
            foreach (var ((archive, file), labels) in changes.OrderBy(pair => pair.Key.Archive).ThenBy(pair => pair.Key.File))
            {
                if (labels.Count == 0) continue;
                var order = OrderOf(archive, file, labels.Keys);
                var entries = labels.OrderBy(pair => order[pair.Key]).Select(pair => Entry(archive, file, pair.Key, pair.Value, notes)).ToList();
                patches.Add(new(Path.GetFileNameWithoutExtension(file), archive, entries));
            }
            return patches;
        }

        // The game's messages first, in its order, then the new ones.
        private Dictionary<string, int> OrderOf(string archive, string file, IEnumerable<string> labels)
        {
            var order = new Dictionary<string, int>();
            foreach (var language in VillagerGameData.Languages)
                foreach (var label in vanilla.GetValueOrDefault((archive, file, language))?.Keys ?? Enumerable.Empty<string>())
                    order.TryAdd(label, order.Count);
            foreach (var label in labels) order.TryAdd(label, order.Count);
            return order;
        }

        private MessageEntry Entry(string archive, string file, string label, Dictionary<string, Message> byLanguage, List<string> notes)
        {
            var ordered = VillagerGameData.Languages.Where(byLanguage.ContainsKey).ToList();
            var texts = ordered.ToDictionary(language => language, language => byLanguage[language].Text);
            // The same text in every language fits in the default.
            if (VillagerGameData.Languages.All(byLanguage.ContainsKey) && texts.Values.Distinct().Count() == 1)
                texts = new() { [MessageEntry.DefaultLanguage] = texts.Values.First() };

            return new(label, texts, Attributes(archive, file, label, byLanguage, ordered, notes));
        }

        // With no attributes in a patch, NookKit keeps a game message's own and zeroes a new message's, so only other changes are
        // written: copied from a game message that has them in every language, since EU languages keep grammar there and differ
        // from each other, or else as hex.
        private MessageAttributes? Attributes(string archive, string file, string label, Dictionary<string, Message> byLanguage, List<string> languages, List<string> notes)
        {
            Dictionary<string, Message> Game(string language) => vanilla.GetValueOrDefault((archive, file, language)) ?? [];
            byte[] Kept(string language) => Game(language).GetValueOrDefault(label)?.Attribute ?? new byte[byLanguage[language].Attribute.Length];

            if (languages.All(language => Kept(language).AsSpan().SequenceEqual(byLanguage[language].Attribute)))
                return null;

            var like = Game(languages[0]).Keys.FirstOrDefault(other => languages.All(language =>
                Game(language).GetValueOrDefault(other)?.Attribute is { } attribute && attribute.AsSpan().SequenceEqual(byLanguage[language].Attribute)));
            if (like != null) return MessageAttributes.LikeLabel(like);

            var first = byLanguage[languages[0]].Attribute;
            if (!languages.All(language => byLanguage[language].Attribute.AsSpan().SequenceEqual(first)))
                notes.Add($"{archive}/{file} {label}: its attributes differ by language and match no game message, so {languages[0]}'s are used for all.");
            return new(Hex: Convert.ToHexStringLower(first));
        }
    }
}
