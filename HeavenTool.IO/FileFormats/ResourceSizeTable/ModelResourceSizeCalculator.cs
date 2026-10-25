namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>
/// Calculates ACNH model archives whose single BFRES has no geometry: textures, animations or both.
/// Geometry, scene animations and unfamiliar layouts deliberately use the game's fallback.
/// </summary>
internal static class ModelResourceSizeCalculator
{
    public static bool TryCalculate(ReadOnlySpan<byte> archive, out uint size)
    {
        size = 0;
        if (!ModelArchive.TryReadSingleBfres(archive, out var member)) return false;
        long pool;
        try
        {
            if (!TryGetRuntimePoolBytes(new ResourceReader(archive.Slice(member.Offset, member.Length)), out pool)) return false;
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or OverflowException)
        {
            return false;
        }

        long allocation = ResourceSizeTable.ModelArchiveLoadBytes(ExpHeapPlanner.AlignUp(archive.Length, 32), [], ResourceRequests(pool));
        if (allocation > uint.MaxValue) return false;
        // Do not apply the generic 32-byte rounding: ACNH's recorded allocations may be 16 mod 32.
        size = (uint)allocation;
        return true;
    }

    // After the archive holder, 0x7103B26680 creates the BFRES resource (0x71038FB680). Its
    // constructor (0x71038FD400) lists one pointer per shader archive: the member hook's
    // static-model shader (0x710070FD2C passes it as a load argument when the archive has no
    // Park_UBER.bfsha) and the global registry's Park_UBER (gsys.bgapkginfo shader_num = 1).
    // Setup (0x71038FB8C0) then allocates the runtime pool; without models nothing else is charged.
    private static ExpHeapRequest[] ResourceRequests(long runtimePool) =>
    [
        new("BFRES resource", 0x308, 8),
        new("Shader archive list", 2 * 8, 8),
        new("BFRES runtime pool", runtimePool, 8)
    ];

    // The runtime pool of a BFRES without models: texture references plus the animation share.
    private static bool TryGetRuntimePoolBytes(ResourceReader bfres, out long pool)
    {
        pool = 0;
        // Version 9's model count and two reserved counts, then the scene animations (not counted
        // by the pool and otherwise untraced), at most one external file and no external GPU flags.
        if (bfres.U16(0xdc) != 0 || bfres.U16(0xde) != 0 || bfres.U16(0xe0) != 0 || bfres.U16(0xea) != 0 ||
            bfres.U16(0xec) > 1 || bfres.Byte(0xee) != 0 || bfres.Byte(0xef) > 1)
            return false;
        // The model and reserved arrays/dictionaries, then the scene animation ones.
        for (int offset = 0x28; offset <= 0x50; offset += 8)
            if (bfres.U64(offset) != 0) return false;
        if (bfres.U64(0x98) != 0 || bfres.U64(0xa0) != 0 || !HasNoGpuData(bfres)) return false;

        pool = BfresRuntimePool.Bytes(0, 0, 0, 0, BfresRuntimePool.TextureCount(bfres), BfresRuntimePool.AnimationBytes(bfres, out _));
        // A BFRES with neither would request an empty pool, which has not been traced.
        return pool != 0;
    }

    // Some exporters emit an empty geometry pool even for texture-only files.
    // Its serialized storage is already part of the loaded archive.
    // Accept only an uninitialized pool with no GPU buffer bytes; actual geometry
    // still fails the count/array checks and cannot enter this calculation.
    private static bool HasNoGpuData(ResourceReader bfres)
    {
        int memoryPool = bfres.Pointer(0xa8), bufferInfo = bfres.Pointer(0xb0);
        if (memoryPool == 0 && bufferInfo == 0) return true;
        return memoryPool != 0 && bufferInfo != 0 &&
            bfres.U32(bufferInfo) == 0x24 && bfres.U32(bufferInfo + 4) == 0 && bfres.U64(bufferInfo + 8) == (ulong)memoryPool &&
            !bfres.Range(memoryPool, 0x120).ContainsAnyExcept((byte)0);
    }
}
