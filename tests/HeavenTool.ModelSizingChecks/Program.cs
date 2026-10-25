using System.Text.Json;
using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

RegressionTests.Run();
if (args.Length == 0) return;
if (args.Length == 3 && args[0] == "cpu-setup") {
    CpuSetupChecks.CompareNative(args[1], args[2]);
    return;
}
if (args.Length == 2 && args[0] == "gpu-startup") {
    GpuPoolChecks.CompareStartup(args[1]);
    return;
}
if (args.Length == 2 && args[0] == "heap") {
    ExpHeapChecks.CompareNative(args[1]);
    return;
}
if (args.Length == 2 && args[0] == "gpu-pools") {
    GpuPoolChecks.CompareNative(args[1]);
    return;
}
if (args.Length is 4 or 5 && args[0] == "geometry") {
    GeometryAnalysisChecks.Run(args[1], args[2], args[3], args.Length == 5 ? args[4] : null);
    return;
}
if (args.Length == 3 && args[0] == "types") {
    TypeCoverageChecks.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "patch") {
    ModelAllocationPatch.Create(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "sizes") {
    var sizes = new List<object>();
    foreach (string path in Directory.EnumerateFiles(Path.Combine(args[1], "Model"), "*.zs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)) {
        string relative = Path.GetRelativePath(args[1], path).Replace('\\', '/')[..^3];
        if (relative.StartsWith("Model/Layout_", StringComparison.Ordinal)) continue;
        sizes.Add(new { path = relative, size = ResourceSizeTable.GetFileSize(path, relative) });
    }
    File.WriteAllText(args[2], JsonSerializer.Serialize(sizes, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"Inspected {sizes.Count} installed model allocations; no installed files were written.");
    return;
}
if (args.Length < 3 || args[0] != "verify") throw new ArgumentException("Commands: verify <romfs> <report.json> [limit]; geometry <romfs> <inventory.jsonl> <report.json> [reference-reuse.json]; cpu-setup <romfs> <native-cpu-report.json>; heap <native-heap-report.json>; gpu-pools <native-gpu-report.json>; gpu-startup <native-startup-report.json>; sizes <romfs> <report.json>; types <romfs> <report.json>; patch <romfs> <candidate.srsizetable>");
string romfs = Path.GetFullPath(args[1]);
var budgets = VanillaResourceBudgets.Read(romfs);
string[] files = Directory.EnumerateFiles(Path.Combine(romfs, "Model"), "*.zs").OrderBy(p => p, StringComparer.Ordinal).ToArray();
if (args.Length > 3) {
    int limit = int.Parse(args[3]); int total = files.Length;
    files = files.Where((_, i) => i == 0 || (long)i * limit / total != (long)(i - 1) * limit / total).Take(limit).ToArray();
}
var mismatches = new List<object>(); var errors = new List<object>();
int exact = 0, fallback = 0, missingBudget = 0, inspected = 0, layout = 0, layoutCalculatorExact = 0;
foreach (string path in files) {
    string relative = Path.GetRelativePath(romfs, path).Replace('\\', '/')[..^3];
    uint? expected = budgets.Find(relative);
    try {
        if (relative.StartsWith("Model/Layout_", StringComparison.Ordinal)) {
            // These files retain their existing production policy, but validate the shared
            // texture profile against them as an additional independent corpus.
            layout++;
            using var compressed = File.OpenRead(path); using var raw = new MemoryStream();
            new ZstdDecompressor().Decompress(compressed, raw);
            if (!ModelResourceSizeCalculator.TryCalculate(raw.GetBuffer().AsSpan(0, (int)raw.Length), out uint calculated) || calculated != expected)
                mismatches.Add(new { path = relative, expected, actual = (long)calculated });
            else layoutCalculatorExact++;
            continue;
        }
        long actual = ResourceSizeTable.GetFileSize(path, relative);
        if (actual < 0) fallback++;
        else if (expected == null) missingBudget++;
        else if (actual == expected) exact++;
        else mismatches.Add(new { path = relative, expected, actual });
    } catch (Exception e) { errors.Add(new { path = relative, error = e.ToString() }); }
    if (++inspected % 5000 == 0) Console.WriteLine($"Verified {inspected}/{files.Length}: {exact} exact, {fallback} fallback, {mismatches.Count} mismatches");
}
var report = new { configuration =
#if DEBUG
"Debug",
#else
"Release",
#endif
romfs, rstcSha256 = budgets.Sha256, scanned = files.Length, inspected, layoutFilesExcluded = layout, layoutCalculatorExact, exact, fallback, missingBudget, mismatches, errors };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(report));
if (mismatches.Count != 0 || errors.Count != 0 || missingBudget != 0) Environment.ExitCode = 1;
