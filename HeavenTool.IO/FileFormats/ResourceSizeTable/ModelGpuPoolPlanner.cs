namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>The two recovered ACNH GPU-pool policies, selected by manager flags.</summary>
internal enum ModelGpuPoolMode { Direct, ManagedReuse }

internal sealed record ModelGpuPoolConfiguration(ModelGpuPoolMode Mode, long MinimumPoolBytes)
{
    // 0x71005C9B44 overrides config+0xE8. 0x71039FFBDC copies it
    // to SystemTask+0x430; 0x7103A024F0 forwards it to GPU args+0x68.
    // 0x71037FEBE0 initializes manager+0x58 and startup flags 2.
    // This describes startup; pools that already exist are passed to Plan separately.
    public static ModelGpuPoolConfiguration Acnh303Startup { get; } = new(ModelGpuPoolMode.ManagedReuse, 0x4000);
}

internal sealed record ModelGpuBufferRequest(string Purpose, long Bytes, int Alignment);
internal sealed record ModelGpuBufferPlacement(ModelGpuBufferRequest Request, int PoolIndex, long Offset,
    IReadOnlyList<ExpHeapRequest> ParentRequests);

/// <summary>
/// Parent requests from 3.0.3's GPU-pool manager (0x71037F8590), without parent
/// heap headers/padding; <see cref="ExpHeapPlanner"/> charges those. No
/// allocations are freed during this plan; all requests use one parent heap and
/// memory flags 3. <c>emptyPools</c> minimum-size pools may already exist.
/// </summary>
internal static class ModelGpuPoolPlanner
{
    public static IReadOnlyList<ModelGpuBufferPlacement> Plan(IEnumerable<ModelGpuBufferRequest> requests,
        ModelGpuPoolConfiguration configuration, int emptyPools = 0)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Plan(requests, configuration.Mode, configuration.MinimumPoolBytes, emptyPools);
    }

    public static IReadOnlyList<ModelGpuBufferPlacement> Plan(IEnumerable<ModelGpuBufferRequest> requests,
        ModelGpuPoolMode mode, long minimumPoolBytes, int emptyPools = 0)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (mode is not (ModelGpuPoolMode.Direct or ModelGpuPoolMode.ManagedReuse))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (minimumPoolBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumPoolBytes));
        if (emptyPools < 0 || emptyPools > 0 && mode != ModelGpuPoolMode.ManagedReuse)
            throw new ArgumentOutOfRangeException(nameof(emptyPools));
        long minimumBackingBytes = ExpHeapPlanner.AlignUp(minimumPoolBytes, 4096);
        var pools = new List<Pool>();
        for (int i = 0; i < emptyPools; i++) pools.Add(new(minimumBackingBytes, Entries(minimumBackingBytes, 0)));
        var placements = new List<ModelGpuBufferPlacement>();
        foreach (var request in requests)
        {
            if (request.Bytes <= 0) throw new ArgumentOutOfRangeException(nameof(requests), "GPU requests must be nonempty.");
            if (request.Alignment <= 0 || (request.Alignment & (request.Alignment - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(requests), "GPU alignment must be a positive power of two.");
            // Virtual heaps start at 0x100000. This implementation handles the
            // resource path's smaller alignments; larger ones need base-aware placement.
            if (mode == ModelGpuPoolMode.ManagedReuse && request.Alignment > 0x100000)
                throw new NotSupportedException("GPU alignments above the native virtual base are unresolved.");
            long rounded = ExpHeapPlanner.AlignUp(request.Bytes, 4096);
            int backingAlignment = checked((int)ExpHeapPlanner.AlignUp(request.Alignment, 4096));
            var parent = new List<ExpHeapRequest>();
            if (mode == ModelGpuPoolMode.Direct)
            {
                parent.Add(new("Direct backing with pool object", checked(rounded + 0x108), backingAlignment));
                placements.Add(new(request, -1, 0, parent.AsReadOnly()));
                continue;
            }
            int best = -1;
            long offset = 0;
            for (int i = 0; i < pools.Count; i++)
            {
                var pool = pools[i];
                if (!pool.TryPlace(request, out long candidate)) continue;
                // Native selection maximizes request/free-size (single-precision).
                // For these fixed requests this is best-fit by total free bytes.
                if (best < 0 || (float)request.Bytes / pool.FreeBytes > (float)request.Bytes / pools[best].FreeBytes)
                {
                    best = i;
                    offset = candidate;
                }
            }
            if (best < 0)
            {
                if (pools.Count == 0) parent.Add(new("Parent heap record", 0x78, 8));
                long backing = Math.Max(rounded, minimumBackingBytes);
                int entries = Entries(backing, request.Bytes);
                // 0x138 pool record + 0x108 virtual heap + 32 bytes per entry.
                parent.Add(new("Pool metadata and virtual heap", checked(0x240 + entries * 0x20L), 8));
                parent.Add(new("Pool backing", backing, backingAlignment));
                pools.Add(new(backing, entries));
                best = pools.Count - 1;
                if (!pools[best].TryPlace(request, out offset))
                    throw new InvalidDataException("New GPU pool cannot satisfy its initial request.");
            }
            pools[best].Add(offset, request.Bytes);
            placements.Add(new(request, best, offset, parent.AsReadOnly()));
        }
        return placements.AsReadOnly();
    }

    private static int Entries(long backing, long initialRequest) =>
        backing - initialRequest <= 0x1ff ? 1 : (int)Math.Min(backing >> 9, 128);

    private sealed class Pool(long bytes, int capacity)
    {
        private readonly long length = bytes;
        private readonly List<(long Start, long Bytes)> allocated = [];
        public long FreeBytes { get; private set; } = bytes;

        public bool TryPlace(ModelGpuBufferRequest request, out long offset)
        {
            offset = 0;
            if (allocated.Count >= capacity) return false;
            long size = Math.Max(request.Bytes, 8);
            int align = Math.Max(request.Alignment, 8);
            foreach (var block in allocated)
            {
                offset = ExpHeapPlanner.AlignUp(offset, align);
                if (size <= block.Start - offset) return true;
                offset = checked(block.Start + block.Bytes);
            }
            offset = ExpHeapPlanner.AlignUp(offset, align);
            return size <= length - offset;
        }

        public void Add(long offset, long size)
        {
            size = Math.Max(size, 8);
            allocated.Add((offset, size));
            allocated.Sort((left, right) => left.Start.CompareTo(right.Start));
            FreeBytes = checked(FreeBytes - size);
        }
    }
}
