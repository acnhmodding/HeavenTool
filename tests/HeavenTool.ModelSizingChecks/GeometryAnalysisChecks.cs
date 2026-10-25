using System.Text.Json;
using System.Buffers.Binary;
using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

internal static class GeometryAnalysisChecks
{
    public static void Run(string romfs, string inventory, string output, string? reference)
    {
        var analyzer = new ModelGeometryAllocationAnalyzer(SwitchShaderArchive.Load(Path.Combine(romfs, "Shader/Park_UBER_All.Nin_NX_NVN.szs")));
        var budgets = VanillaResourceBudgets.Read(romfs);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        using var comparison = reference == null ? null : JsonDocument.Parse(File.ReadAllText(reference));
        var references = comparison?.RootElement.GetProperty("resources").EnumerateArray()
            .ToDictionary(r => r.GetProperty("path").GetString()!, r => r);
        var resources = new List<object>();
        var mismatches = new List<object>();
        var declined = new List<object>();
        int shapes = 0, materials = 0;
        long slots = 0;
        int editChecks = 0;
        int animationEditChecks = 0;
        int exact = 0, singleModel = 0, singleModelExact = 0, singleBfres = 0;
        // Other layouts are analyzed when supported (BFRES with output.phive); the rest keep the game's fallback.
        int physics = 0, physicsExact = 0, otherLayoutsDeclined = 0, underReports = 0;
        var otherLayoutReasons = new SortedDictionary<string, int>();
        var sizeDifferences = new List<object>();
        foreach (string line in File.ReadLines(inventory))
        {
            using var row = JsonDocument.Parse(line);
            string category = row.RootElement.GetProperty("category").GetString()!;
            bool otherLayout = category == "other archive layouts";
            if (category != "single BFRES geometry" && !otherLayout) continue;
            string path = row.RootElement.GetProperty("path").GetString()!;
            using var compressed = File.OpenRead(Path.Combine(romfs, path + ".zs"));
            using var raw = new MemoryStream();
            new ZstdDecompressor().Decompress(compressed, raw);
            if (!analyzer.TryAnalyze(raw.GetBuffer().AsMemory(0, checked((int)raw.Length)), out var result, out string reason))
            {
                if (otherLayout)
                {
                    otherLayoutsDeclined++;
                    otherLayoutReasons[reason] = otherLayoutReasons.GetValueOrDefault(reason) + 1;
                }
                else declined.Add(new { path, reason });
                continue;
            }
            if (otherLayout) physics++;
            else singleBfres++;
            materials += result.Materials;
            shapes += result.Shapes;
            slots += result.SelectedPassVariationSlots;
            uint vanilla = budgets.Find(path) ?? throw new InvalidDataException("Vanilla RSTC is missing " + path);
            long allocation = result.AllocationBytes;
            bool matches = allocation == vanilla;
            if (matches) exact++;
            else sizeDifferences.Add(new { path, expected = vanilla, actual = allocation, delta = allocation - vanilla, result.Models });
            if (matches && otherLayout) physicsExact++;
            if (allocation < vanilla) underReports++;
            if (result.Models == 1)
            {
                singleModel++;
                if (matches) singleModelExact++;
            }
            resources.Add(new { path, vanillaAllocation = vanilla, allocation, cpuExpHeapBytes = result.CpuExpHeapBytes, analysis = result });
            long production = ResourceSizeTable.GetFileSize(Path.Combine(romfs, path + ".zs"), path);
            if (production != allocation) mismatches.Add(new { path, field = "production GetFileSize", expected = allocation, actual = production });
            if (references != null && !otherLayout)
            {
                if (!references.TryGetValue(path, out var expected)) mismatches.Add(new { path, field = "missing reference" });
                else
                {
                    var actual = JsonSerializer.SerializeToElement(result.RenderRequests, options);
                    foreach (var field in expected.GetProperty("counts").EnumerateObject())
                        if (!actual.TryGetProperty(field.Name, out var value) || value.GetInt64() != field.Value.GetInt64())
                            mismatches.Add(new { path, field = field.Name, expected = field.Value.GetInt64(), actual = value.ValueKind == JsonValueKind.Number ? (long?)value.GetInt64() : null });
                    const string commandBytes = "commandBufferRequestedBytes";
                    if (actual.GetProperty(commandBytes).GetInt64() != expected.GetProperty(commandBytes).GetInt64())
                        mismatches.Add(new { path, field = commandBytes, expected = expected.GetProperty(commandBytes).GetInt64(), actual = actual.GetProperty(commandBytes).GetInt64() });
                }
            }
            if (editChecks == 0 && result.Models == 1 && result.Materials == 1 && result.Shapes == 1 && result.Meshes == 1)
                editChecks = CheckEdits(analyzer, raw.GetBuffer().AsSpan(0, (int)raw.Length).ToArray(), result);
            if (animationEditChecks == 0 && result.AnimationNameStorageBytes == 0)
                animationEditChecks = CheckAnimationEdit(analyzer, raw.GetBuffer().AsSpan(0, (int)raw.Length).ToArray(), result);
            if (resources.Count % 500 == 0) Console.WriteLine($"Geometry: {resources.Count} analyzed, {declined.Count} declined, {mismatches.Count} comparison differences");
        }
        var summary = new { resources = resources.Count, materials, shapes, selectedPassVariationSlots = slots,
            rstcSha256 = budgets.Sha256,
            comparedResources = references == null ? 0 : singleBfres, declined = declined.Count, mismatches = mismatches.Count,
            editAndMalformedChecks = editChecks,
            animationEditChecks,
            exactAllocations = exact, singleModelResources = singleModel, singleModelExactAllocations = singleModelExact,
            physicsResources = physics, physicsExactAllocations = physicsExact, otherLayoutsDeclined, underReports };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { summary, sizeDifferences, resources, declined, otherLayoutReasons, mismatches }, options) + "\n");
        Console.WriteLine(JsonSerializer.Serialize(summary));
        if (declined.Count != 0 || mismatches.Count != 0 || references != null && references.Count != singleBfres) Environment.ExitCode = 1;
    }

    // Records compare lists by reference; compare the per-model block sizes by value.
    private static bool Same(ModelGeometryAllocationAnalysis left, ModelGeometryAllocationAnalysis right) =>
        left.ModelBlockBufferBytes.SequenceEqual(right.ModelBlockBufferBytes)
        && left with { ModelBlockBufferBytes = right.ModelBlockBufferBytes } == right;

    private static int CheckEdits(ModelGeometryAllocationAnalyzer analyzer, byte[] original, ModelGeometryAllocationAnalysis baseline)
    {
        int checks = 0;
        void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("Geometry edit regression: " + label); }
        const int f = 0x2000;
        int Pointer(byte[] bytes, int at) => checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at, 8)));
        int model = f + Pointer(original, f + 0x28);
        int material = f + Pointer(original, model + 0x38);
        int shape = f + Pointer(original, model + 0x28);
        int mesh = f + Pointer(original, shape + 0x18);
        Check(!ModelResourceSizeCalculator.TryCalculate(original, out _), "Texture calculator declines geometry");
        byte[] changed = (byte[])original.Clone();
        uint before = BinaryPrimitives.ReadUInt32LittleEndian(changed.AsSpan(mesh + 0x30, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(mesh + 0x30, 4), before == 0 ? 1u : 0u);
        Check(analyzer.TryAnalyze(changed, out var drawEdit, out _), "Edited mesh still analyzes");
        Check(drawEdit!.RenderRequests.RecordedMeshDrawBytes == baseline.RenderRequests.RecordedMeshDrawBytes + (before == 0 ? 4 : -4), "Base-vertex edit changes actual command cost");
        Check(drawEdit.RenderRequests.CommandBufferRequestedBytes == baseline.RenderRequests.CommandBufferRequestedBytes + (before == 0 ? 4 : -4), "Pool request follows the edit");
        Check(drawEdit.GpuBufferRequests().Last().Bytes == baseline.GpuBufferRequests().Last().Bytes + (before == 0 ? 4 : -4), "GPU command request follows the edit");
        Check(drawEdit.CpuExpHeapBytes == baseline.CpuExpHeapBytes && drawEdit.AllocationBytes >= baseline.CpuExpHeapBytes, "Command edits only affect GPU pools");
        changed = (byte[])original.Clone();
        ushort source = BinaryPrimitives.ReadUInt16LittleEndian(changed.AsSpan(material + 0xa2, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(material + 0xa2, 2), checked((ushort)(source + 64)));
        Check(analyzer.TryAnalyze(changed, out var workEdit, out _), "Edited material work metadata still analyzes");
        Check(workEdit!.MaterialCpuWorkBytes == baseline.MaterialCpuWorkBytes + 64, "Material work is read from changed metadata");
        Check(workEdit.RenderRequests == baseline.RenderRequests, "Material source length does not change render commands");
        Check(workEdit.MaterialUniformBufferRequestedBytes == baseline.MaterialUniformBufferRequestedBytes, "CPU source length does not change shader-defined GPU uniform storage");
        changed = (byte[])original.Clone();
        int assign = f + Pointer(original, material + 0x20);
        int archiveName = f + Pointer(original, assign);
        changed[archiveName + 2] ^= 1;
        Check(!analyzer.TryAnalyze(changed, out var rejected, out string reason) && rejected == null && reason.Length != 0, "Unknown archive returns no partial result");
        changed = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt64LittleEndian(changed.AsSpan(f + 0x28, 8), ulong.MaxValue);
        Check(!analyzer.TryAnalyze(changed, out rejected, out reason) && rejected == null && reason.Length != 0, "Invalid model pointer is bounded");
        changed = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(shape + 0x52, 2), ushort.MaxValue);
        Check(!analyzer.TryAnalyze(changed, out rejected, out reason) && rejected == null, "Invalid material reference is bounded");
        for (int length = 0; length < original.Length; length += Math.Max(1, original.Length / 32))
            Check(!analyzer.TryAnalyze(original.AsMemory(0, length), out rejected, out reason) && rejected == null, "Truncated archive");
        Check(analyzer.TryAnalyze(original, out var again, out _) && Same(again!, baseline), "Edits do not contaminate archive caches");
        Console.WriteLine($"Passed {checks} geometry edit/malformed-input checks.");
        return checks;
    }

    private static int CheckAnimationEdit(ModelGeometryAllocationAnalyzer analyzer, byte[] original, ModelGeometryAllocationAnalysis baseline)
    {
        const int f = 0x2000;
        if (BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(f + 0xe4, 2)) == 0) return 0;
        int animation = f + checked((int)BinaryPrimitives.ReadUInt64LittleEndian(original.AsSpan(f + 0x68, 8)));
        int name = f + checked((int)BinaryPrimitives.ReadUInt64LittleEndian(original.AsSpan(animation + 8, 8)));
        if (BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(name, 2)) < 5) return 0;
        int checks = 0;
        void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("Geometry animation edit: " + label); }
        var changed = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(name, 2), 5);
        "A.fsp\0"u8.CopyTo(changed.AsSpan(name + 2));
        Check(analyzer.TryAnalyze(changed, out var result, out _), "Recognized animation suffix still analyzes");
        Check(result!.AnimationNameStorageBytes == 8, "Normalized name storage is page-independent and aligned to eight");
        Check(result.ConfirmedRuntimePoolBytes == baseline.ConfirmedRuntimePoolBytes + 8, "Runtime pool reserves normalized names");
        Check(result.MaterialUniformBufferRequestedBytes == baseline.MaterialUniformBufferRequestedBytes && result.RenderRequests == baseline.RenderRequests,
            "Name edit leaves GPU requests unchanged");
        changed[name + 2] = 0xff;
        Check(!analyzer.TryAnalyze(changed, out var rejected, out _) && rejected == null, "Invalid UTF-8 name returns no allocation analysis");
        changed[name + 2] = 0;
        Check(!analyzer.TryAnalyze(changed, out rejected, out _) && rejected == null, "Embedded string terminator is rejected");
        Check(analyzer.TryAnalyze(original, out var again, out _) && Same(again!, baseline), "Name edits do not contaminate cached analysis");
        Console.WriteLine($"Passed {checks} geometry animation edit checks.");
        return checks;
    }
}
