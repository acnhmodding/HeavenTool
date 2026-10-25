using System.Text.Json;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

internal static class ExpHeapChecks
{
    public static void CompareNative(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        int checks = 0;
        void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("ExpHeap native comparison: " + label); }
        foreach (var sample in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var native = sample.GetProperty("allocations").EnumerateArray().ToArray();
            var requests = native.Select((r, i) => new ExpHeapRequest(i.ToString(),
                r.GetProperty("requestBytes").GetInt64(), r.GetProperty("alignment").GetInt32()));
            var predicted = ExpHeapPlanner.Plan(requests, sample.GetProperty("initialFreeBlockAddress").GetInt64());
            Check(predicted.Count == native.Length, "Allocation count");
            for (int i = 0; i < native.Length; i++)
            {
                Check(predicted[i].Address == native[i].GetProperty("address").GetInt64(), "Payload address");
                Check(predicted[i].ChargedBytes == native[i].GetProperty("chargedBytes").GetInt64(), "Free-size charge includes alignment holes");
            }
        }
        // The projection rejects native paths it has not modelled.
        void Reject(Action action, Type expected)
        {
            try { action(); }
            catch (Exception error) when (error.GetType() == expected) { checks++; return; }
            throw new Exception("ExpHeap accepted an unsupported request");
        }
        Reject(() => ExpHeapPlanner.Plan([], -8), typeof(ArgumentOutOfRangeException));
        Reject(() => ExpHeapPlanner.Plan([], 1), typeof(ArgumentOutOfRangeException));
        Reject(() => ExpHeapPlanner.Plan([new("Negative", -1, 8)], 0), typeof(ArgumentOutOfRangeException));
        foreach (int alignment in new[] { -8, 0, 4, 12, 16384 })
            Reject(() => ExpHeapPlanner.Plan([new("Unsupported", 8, alignment)], 0), typeof(NotSupportedException));
        Reject(() => ExpHeapPlanner.Plan([new("Overflow", long.MaxValue, 8)], 0), typeof(OverflowException));
        Console.WriteLine($"Passed {checks} ExpHeap comparisons/validation checks against native ARM64 execution.");
    }
}
