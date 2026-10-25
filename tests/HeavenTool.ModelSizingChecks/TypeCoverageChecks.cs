using System.Buffers.Binary;
using System.Text.Json;
using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>Compares ResourceSizeTable.GetFileSize with the vanilla RSTC for every romfs file, grouped by type.</summary>
internal static class TypeCoverageChecks
{
    private sealed class Group
    {
        public int Files, InTable, Exact, Above, Below, Fallback, Unsupported, Errors;
        public List<object> Samples { get; } = [];
    }

    public static void Run(string romfs, string output)
    {
        romfs = Path.GetFullPath(romfs);
        var budgets = VanillaResourceBudgets.Read(romfs);
        var groups = new SortedDictionary<string, Group>(StringComparer.Ordinal);
        var zstd = new ZstdDecompressor();
        int inspected = 0;
        var entries = new List<object>();
        foreach (string path in Directory.EnumerateFiles(romfs, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(romfs, path).Replace('\\', '/');
            if (!relative.Contains('/') || relative == "System/Resource/ResourceSizeTable.srsizetable") continue;
            string key = relative.EndsWith(".zs", StringComparison.Ordinal) ? relative[..^3] : relative;
            uint? expected = budgets.Find(key);

            string type;
            long actual;
            try
            {
                type = TypeOf(key, path, zstd);
                actual = ResourceSizeTable.GetFileSize(path, key);
            }
            catch (Exception error)
            {
                type = TypeOf(key, null, zstd);
                Get(groups, type).Errors++;
                Get(groups, type).Files++;
                if (Get(groups, type).Samples.Count < 5) Get(groups, type).Samples.Add(new { path = key, error = error.Message });
                continue;
            }
            var group = Get(groups, type);
            if (!type.StartsWith("Model", StringComparison.Ordinal) && expected != null)
                entries.Add(new { path = key, type, expected, actual });
            group.Files++;
            if (expected != null) group.InTable++;
            if (actual == -1) group.Fallback++;
            else if (actual < 0) group.Unsupported++;
            else if (expected == null) { }
            else if (actual == expected) group.Exact++;
            else
            {
                if (actual > expected) group.Above++; else group.Below++;
                if (group.Samples.Count < 5) group.Samples.Add(new { path = key, expected, actual, delta = actual - (long)expected });
            }
            if (++inspected % 10000 == 0) Console.WriteLine($"Inspected {inspected} files");
        }

        var rows = groups.Select(g => new
        {
            type = g.Key, files = g.Value.Files, inTable = g.Value.InTable, exact = g.Value.Exact, above = g.Value.Above,
            below = g.Value.Below, fallback = g.Value.Fallback, unsupported = g.Value.Unsupported, errors = g.Value.Errors,
            samples = g.Value.Samples
        }).ToList();
        File.WriteAllText(output, JsonSerializer.Serialize(new { rstcSha256 = budgets.Sha256, rows, entries }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"Wrote {rows.Count} file types for {rows.Sum(r => r.files)} files.");
    }

    private static Group Get(SortedDictionary<string, Group> groups, string type) =>
        groups.TryGetValue(type, out var group) ? group : groups[type] = new Group();

    // Top-level folder plus extension; Model archives are split by their member layout.
    private static string TypeOf(string key, string? path, ZstdDecompressor zstd)
    {
        string folder = key[..key.IndexOf('/')], name = key[(key.LastIndexOf('/') + 1)..];
        int dot = name.IndexOf('.');
        string extension = dot < 0 ? "(none)" : name[dot..];
        if (folder != "Model" || path == null) return $"{folder} {extension}";
        if (key.StartsWith("Model/Layout_", StringComparison.Ordinal)) return "Model Layout_* textures";

        using var compressed = File.OpenRead(path);
        using var raw = new MemoryStream();
        zstd.Decompress(compressed, raw);
        var archive = raw.GetBuffer().AsSpan(0, (int)raw.Length);
        if (ModelResourceSizeCalculator.TryCalculate(archive, out _)) return "Model BFRES textures or animations";
        if (!ModelArchive.TryReadSarc(archive, out var members)) return "Model (not a SARC)";
        var kinds = members.Select(m => m.Name == null ? "?" : m.Name[(m.Name.LastIndexOf('.') + 1)..]).Distinct().Order().ToArray();
        if (kinds is ["bfres"] && members.Length == 1)
        {
            var bfres = archive.Slice(members[0].Offset, members[0].Length);
            return bfres.Length > 0xde && BinaryPrimitives.ReadUInt16LittleEndian(bfres[0xdc..]) == 0
                ? "Model BFRES without geometry (declined)" : "Model BFRES geometry";
        }
        return "Model archive: " + string.Join(" + ", kinds);
    }
}
