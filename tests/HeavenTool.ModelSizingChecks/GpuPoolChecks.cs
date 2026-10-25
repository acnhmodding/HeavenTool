using System.Text.Json;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

internal static class GpuPoolChecks
{
    public static void CompareStartup(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var native = document.RootElement;
        var configuration = ModelGpuPoolConfiguration.Acnh303Startup;
        if (native.GetProperty("gameMinimumBytes").GetInt64() != configuration.MinimumPoolBytes ||
            native.GetProperty("copiedTaskMinimumBytes").GetInt64() != configuration.MinimumPoolBytes ||
            native.GetProperty("forwardedGpuMinimumBytes").GetInt64() != configuration.MinimumPoolBytes ||
            native.GetProperty("managerMinimumBytes").GetInt64() != configuration.MinimumPoolBytes ||
            native.GetProperty("managerFlags").GetInt32() != 2 ||
            configuration.Mode != ModelGpuPoolMode.ManagedReuse)
            throw new Exception("ACNH GPU startup configuration differs from native execution.");
        Console.WriteLine("Passed native ACNH GPU startup configuration comparison (game override, task copy, manager minimum and policy).");
    }

    public static void CompareNative(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        int checks = 0;
        void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("GPU pool native comparison: " + label); }
        foreach (var sample in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var requests = sample.GetProperty("allocations").EnumerateArray().Select((a, i) => new ModelGpuBufferRequest(
                i.ToString(), a.GetProperty("requestBytes").GetInt64(), a.GetProperty("alignment").GetInt32())).ToArray();
            var mode = sample.GetProperty("flags").GetInt32() == 1 ? ModelGpuPoolMode.Direct : ModelGpuPoolMode.ManagedReuse;
            var planned = ModelGpuPoolPlanner.Plan(requests, mode, sample.GetProperty("minimum").GetInt64());
            var poolIds = new Dictionary<long, int>();
            int index = 0;
            foreach (var actual in sample.GetProperty("allocations").EnumerateArray())
            {
                var prediction = planned[index++];
                var expected = actual.GetProperty("parentRequests").EnumerateArray().ToArray();
                Check(prediction.ParentRequests.Count == expected.Length, "Number of parent requests");
                for (int j = 0; j < expected.Length; j++)
                {
                    Check(prediction.ParentRequests[j].Bytes == expected[j].GetProperty("requestBytes").GetInt64(), "Parent request bytes");
                    Check(prediction.ParentRequests[j].Alignment == expected[j].GetProperty("alignment").GetInt32(), "Parent alignment");
                }
                long nativePool = actual.GetProperty("pool").GetInt64();
                if (nativePool != 0 && !poolIds.ContainsKey(nativePool)) poolIds.Add(nativePool, poolIds.Count);
                Check(prediction.PoolIndex == (nativePool == 0 ? -1 : poolIds[nativePool]), "Pool reuse");
                Check(prediction.Offset == actual.GetProperty("poolOffset").GetInt64(), "Virtual suballocation offset");
            }
        }
        foreach (var sample in document.RootElement.GetProperty("animationNameCases").EnumerateArray())
            Check(BfresRuntimePool.NormalizedAnimationNameBytes(sample.GetProperty("name").GetString()!)
                == sample.GetProperty("normalizedStorageBytes").GetInt32(), "Normalized animation name storage");
        Console.WriteLine($"Passed {checks} GPU-pool/animation comparisons against native ARM64 execution.");
    }
}
