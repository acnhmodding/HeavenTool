namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

internal sealed record ExpHeapRequest(string Purpose, long Bytes, int Alignment);
internal sealed record ExpHeapPlacement(ExpHeapRequest Request, long Address,
    long PayloadBytes, long HeaderBytes, long PaddingBytes)
{
    public long ChargedBytes => checked(PayloadBytes + HeaderBytes + PaddingBytes);
}

/// <summary>
/// ACNH 3.0.3's forward ExpHeap allocation path (0x71037C77A0), with sufficient
/// space and no frees. The starting address is the free block's header address,
/// not the first payload address. <see cref="ModelGeometryAllocationAnalysis"/>
/// places the GPU pools' parent requests after the CPU high-water mark with it,
/// <see cref="ModelPhysicsAllocationPlanner"/> places an archive's phive requests, and
/// <see cref="ResourceSizeTable.GetFileSize"/> places plain resource loads.
/// </summary>
internal static class ExpHeapPlanner
{
    private const int HeaderBytes = 32;

    public static IReadOnlyList<ExpHeapPlacement> Plan(
        IEnumerable<ExpHeapRequest> requests, long initialFreeBlockAddress)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (initialFreeBlockAddress < 0 || (initialFreeBlockAddress & 7) != 0)
            throw new ArgumentOutOfRangeException(nameof(initialFreeBlockAddress));
        var placements = new List<ExpHeapPlacement>();
        long front = initialFreeBlockAddress;
        foreach (var request in requests)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Bytes < 0) throw new ArgumentOutOfRangeException(nameof(requests), "Heap requests cannot be negative.");
            if (request.Alignment < 8 || request.Alignment > 8192 || (request.Alignment & (request.Alignment - 1)) != 0)
                throw new NotSupportedException("Only the native-verified positive alignments from 8 through 8192 are supported.");
            long address = AlignUp(checked(front + HeaderBytes), request.Alignment);
            long payload = AlignUp(Math.Max(8, request.Bytes), 8);
            placements.Add(new(request, address, payload, HeaderBytes, checked(address - front - HeaderBytes)));
            front = checked(address + payload);
        }
        return placements.AsReadOnly();
    }

    /// <summary>The end of the last payload, or the starting address when nothing is requested.</summary>
    public static long HighWaterMark(IEnumerable<ExpHeapRequest> requests, long initialFreeBlockAddress)
    {
        var heap = Plan(requests, initialFreeBlockAddress);
        return heap.Count == 0 ? initialFreeBlockAddress : checked(heap[^1].Address + heap[^1].PayloadBytes);
    }

    /// <summary>Rounds up to a power-of-two alignment.</summary>
    internal static long AlignUp(long value, long alignment) => checked(value + alignment - 1) & -alignment;
}
