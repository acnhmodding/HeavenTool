using System.Security.Cryptography;
using System.Text.Json;
using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

internal static class CpuSetupChecks
{
    public static void CompareNative(string romfs, string fixture)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(fixture));
        var native = document.RootElement;
        var budgets = VanillaResourceBudgets.Read(romfs);
        string shaderPath = Path.Combine(romfs, "Shader/Park_UBER_All.Nin_NX_NVN.szs");
        var shaders = SwitchShaderArchive.Load(shaderPath);
        var analyzer = new ModelGeometryAllocationAnalyzer(shaders);
        int checks = 0, resources = 0, materials = 0, completeRestrictedSetups = 0;
        void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new Exception("CPU setup native comparison: " + label);
        }
        static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Check(native.GetProperty("summary").GetProperty("rstcSha256").GetString() == budgets.Sha256, "RSTC identity");
        Check(native.GetProperty("shaderArchiveSha256").GetString() == Hash(File.ReadAllBytes(shaderPath)), "Shader archive identity");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shader in native.GetProperty("shaderWork").EnumerateArray())
        {
            string name = shader.GetProperty("name").GetString()!;
            Check(names.Add(name), "Duplicate shader fixture");
            var model = shaders.Models.Single(m => m.Name == name);
            Check(model.AllocationShaderObjectWorkBytes == shader.GetProperty("nativeObjectWorkBytes").GetInt32(), name + " object work");
            Check(model.AllocationSelectorWorkBytes == shader.GetProperty("nativeSelectorWorkBytes").GetInt32(), name + " selector work");
        }
        Check(names.Count == shaders.Models.Count, "All shared shader work layouts covered");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in native.GetProperty("resources").EnumerateArray())
        {
            string path = sample.GetProperty("path").GetString()!;
            Check(paths.Add(path), "Duplicate resource fixture");
            byte[] packed = File.ReadAllBytes(Path.Combine(romfs, path + ".zs"));
            Check(Hash(packed) == sample.GetProperty("sourceSha256").GetString(), path + " source identity");
            using var compressed = new MemoryStream(packed);
            using var raw = new MemoryStream();
            new ZstdDecompressor().Decompress(compressed, raw);
            if (!analyzer.TryAnalyze(raw.GetBuffer().AsMemory(0, checked((int)raw.Length)), out var result, out string reason))
                throw new Exception($"CPU setup analysis declined {path}: {reason}");
            Check(result.ConfirmedRuntimePoolBytes == sample.GetProperty("nativeRuntimePoolBytes").GetInt64(), path + " runtime pool");
            var requests = sample.GetProperty("requests").EnumerateArray().ToArray();
            Check(requests.Length == 2, path + " request count in sizing prefix/restricted setup");
            Check(requests[0].GetProperty("heap").GetString() == "temporary"
                && requests[0].GetProperty("requestBytes").GetInt32() == 0x18000
                && requests[0].GetProperty("alignment").GetInt32() == 8, path + " separate scratch allocation");
            Check(requests[1].GetProperty("heap").GetString() == "resource"
                && requests[1].GetProperty("requestBytes").GetInt64() == result.ConfirmedRuntimePoolBytes
                && requests[1].GetProperty("alignment").GetInt32() == 8, path + " resource allocation");
            var work = sample.GetProperty("materialWork").EnumerateArray().ToArray();
            Check(work.Length == result.Materials, path + " material layout coverage");
            // Native helpers calculate a layout even when a shader would not use
            // it. Activation remains a separate reflection/selection assumption.
            long active = work.Where(m => m.GetProperty("reflectedMaterialUniformBytes").GetInt32() != 0)
                .Sum(m => m.GetProperty("nativeWorkBytes").GetInt64());
            Check(active == result.MaterialCpuWorkBytes, path + " material work with reflected uniform activation");
            Check(work.Sum(m => m.GetProperty("reflectedMaterialUniformBytes").GetInt64()) == result.MaterialUniformBlockBytes,
                path + " material uniform reflection");
            Check(work.Sum(m => m.GetProperty("nativeWorkBytes").GetInt64()) == sample.GetProperty("nativeMaterialWorkBytesBeforeActivation").GetInt64(),
                path + " native material subtotal");
            if (sample.GetProperty("fullRestrictedSetup").GetBoolean())
            {
                Check(result.Materials == 0 && result.Shapes == 0 && result.Textures == 0, path + " restricted setup scope");
                Check(sample.GetProperty("nativeConsumedPoolBytes").GetInt64() == result.ConfirmedRuntimePoolBytes,
                    path + " consumed pool");
                Check(sample.GetProperty("nativeStoredPoolBytes").GetInt64() == result.ConfirmedRuntimePoolBytes,
                    path + " stored pool capacity");
                Check(sample.GetProperty("nativePostPoolChargeBytes").GetInt32() == 0, path + " no later resource allocations");
                completeRestrictedSetups++;
            }
            resources++;
            materials += work.Length;
        }
        var summary = native.GetProperty("summary");
        Check(resources > 0 && resources == summary.GetProperty("resources").GetInt32(), "Resource coverage");
        Check(materials == summary.GetProperty("materials").GetInt32(), "Material coverage");
        Check(summary.GetProperty("differences").GetInt32() == 0 && native.GetProperty("differences").GetArrayLength() == 0,
            "Fixture has no runtime-pool differences");
        Console.WriteLine($"Passed {checks} native CPU work/pool comparisons for {resources} resources/{materials} materials; {completeRestrictedSetups} restricted setups completed.");
    }
}
