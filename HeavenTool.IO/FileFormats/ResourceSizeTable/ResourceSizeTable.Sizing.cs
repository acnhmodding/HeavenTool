using HeavenTool.IO.Common;
using HeavenTool.IO.FileFormats.BFRES;
using System.Collections.Concurrent;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

// Calculates the resource-heap allocation ACNH 3.0.3 records for a file.
public partial class ResourceSizeTable
{
    // Zstd files are only modelled for these loads; other compressed resources keep the game's fallback.
    private static readonly string[] ZstdLoadFolders = ["Layout/", "Font/", "Message/", "Model/Layout_"];

    /// <summary>
    /// Calculates the allocation ACNH records in the table for a file.
    /// </summary>
    /// <param name="entireFileName">The file on disk, optionally Zstd-compressed (".zs").</param>
    /// <param name="romfsName">The romfs path the table uses, without ".zs".</param>
    /// <returns>
    /// <para>The <b>allocation size</b> in bytes</para>
    /// <para>or <b>-1</b> when the game should size the file itself (remove its entry)</para>
    /// <para>or <b>-2</b> if the file type is unsupported</para>
    /// </returns>
    /// <exception cref="OverflowException">Throws when the size exceeds <see cref="uint.MaxValue"/></exception>
    public static long GetFileSize(string entireFileName, string romfsName)
    {
        if (romfsName.StartsWith("Model/", StringComparison.Ordinal) &&
            !romfsName.StartsWith("Model/Layout_", StringComparison.Ordinal) &&
            romfsName.EndsWith(".Nin_NX_NVN", StringComparison.Ordinal))
            return GetModelArchiveSize(entireFileName, romfsName);

        if (entireFileName.EndsWith(".zs") && !ZstdLoadFolders.Any(romfsName.StartsWith))
            return -1;

        var load = GetLoad(entireFileName, romfsName);
        if (load == null) return -2;

        // Loaded data is always allocated in 32-byte multiples
        long size = load(ExpHeapPlanner.AlignUp(ResourceLength(entireFileName), 32));
        return size <= uint.MaxValue ? size : throw new OverflowException($"{entireFileName} is too big!");
    }

    // The allocations of a file's resource load for its loaded data size, or null when unsupported.
    private static Func<long, long>? GetLoad(string entireFileName, string romfsName)
    {
        // Layout and Font archives are both requested with alignment 0x2000 (0x7103284690)
        if (romfsName.StartsWith("Layout/") || romfsName.StartsWith("Font/"))
            return data => ArchiveLoadBytes(data, 0x2000);
        if (romfsName.StartsWith("Model/Layout_"))
            return data => data + 9264;
        if (romfsName.StartsWith("Message/"))
            return data => ArchiveLoadBytes(data, 0x100);
        if (entireFileName.EndsWith(".bars"))
            return data => LoadBytes(new("Sound resource", 0x30, 32), new("Sound data", data, 0x80), new("Parsed sound", 0xa8, 8));
        // Traced for bfevfl (factory 0x71048837E8); bcsv, byml and asb use the same budget in every vanilla entry.
        if (entireFileName.EndsWith(".bcsv") || entireFileName.EndsWith(".bfevfl") || entireFileName.EndsWith(".byml") || entireFileName.EndsWith(".asb"))
            return data => LoadBytes(new("EventFlow resource", 0x38, 32), new("EventFlow data", data, 32));
        return null;
    }

    private static long GetModelArchiveSize(string entireFileName, string romfsName)
    {
        using var data = ReadResource(entireFileName);
        var archive = data.GetBuffer().AsMemory(0, checked((int)data.Length));
        if (ModelResourceSizeCalculator.TryCalculate(archive.Span, out uint allocation))
            return allocation;

        if (ModelArchive.TryReadSarc(archive.Span, out var members))
        {
            long loaded = ExpHeapPlanner.AlignUp(archive.Length, 32);
            // Collision archives: PBC members are read in place, so only the archive load itself allocates.
            if (members.Length > 0 && members.All(m => m.Name?.EndsWith(".pbc", StringComparison.Ordinal) == true))
                return ModelArchiveLoadBytes(loaded, [], []);

            // Physics-only archives: the member hook adds the helper-bone, cloth or fish-bone requests.
            // Other members (such as cloth wind) are not modelled, so the game's fallback is kept.
            if (members is [{ Name: "output.phive" } phive])
            {
                try { return ModelArchiveLoadBytes(loaded, ModelPhysicsAllocationPlanner.Requests(archive.Span.Slice(phive.Offset, phive.Length)), []); }
                catch (Exception e) when (e is NotSupportedException or InvalidDataException) { return -1; }
            }
        }

        // Geometry needs the shared shader archive from the same romfs; without it the game's fallback is kept.
        var analyzer = GetGeometryAnalyzer(entireFileName, romfsName);
        return analyzer != null && analyzer.TryAnalyze(archive, out var geometry, out _) && geometry.AllocationBytes <= uint.MaxValue
            ? geometry.AllocationBytes : -1;
    }

    // A resource heap (0x71037C6D48) starts with its 0x108-byte ExpHeap object; the first free block follows it.
    private const long ResourceHeapFreeBlock = 0x108;

    /// <summary>
    /// Resource-heap bytes used by a load's allocations, measured from a heap start aligned to
    /// every request. The game adds the load alignment and 8 bytes on top (0x7103E36E30), which
    /// covers any other heap start, so this is the value the table stores.
    /// </summary>
    private static long LoadBytes(params ExpHeapRequest[] requests) =>
        ExpHeapPlanner.HighWaterMark(requests, ResourceHeapFreeBlock);

    // Zstd archives: the 0x58-byte archive resource, then the decompressed data at the caller's load alignment.
    private static long ArchiveLoadBytes(long data, int loadAlignment) =>
        LoadBytes(new("Archive resource", 0x58, 32), new("Archive data", data, loadAlignment));

    // Model archives: after the archive load, the model resource opens the data as a second SARC
    // resource (0x71006E0EC0), runs the member hook (0x710070FD2C) and keeps the archive in a
    // 0x10-byte holder (0x71006E1098). Without shader members the hook only allocates for a phive
    // member. An output.bfres member is then created after the holder (resourceRequests).
    internal static long ModelArchiveLoadBytes(long data, IEnumerable<ExpHeapRequest> memberRequests,
        IEnumerable<ExpHeapRequest> resourceRequests) =>
        LoadBytes([
            new("Archive resource", 0x58, 32), new("Archive data", data, 0x2000),
            new("Model archive resource", 0x58, 32), .. memberRequests, new("Model archive holder", 0x10, 8),
            .. resourceRequests
        ]);

    // The file's bytes, decompressed when it is Zstd data.
    private static MemoryStream ReadResource(string path)
    {
        using var file = File.OpenRead(path);
        var data = new MemoryStream();
        if (IsZstd(file)) ZstdDecompressor.Decompress(file, data);
        else file.CopyTo(data);
        return data;
    }

    // The file's length, decompressed when it is Zstd data.
    private static long ResourceLength(string path)
    {
        using var file = File.OpenRead(path);
        if (!IsZstd(file)) return file.Length;
        using var data = new MemoryStream();
        ZstdDecompressor.Decompress(file, data);
        return data.Length;
    }

    private static bool IsZstd(Stream stream)
    {
        bool zstd = ZstdDecompressor.CanDecompress(stream);
        stream.Position = 0;
        return zstd;
    }

    private const string SharedShaderArchivePath = "Shader/Park_UBER_All.Nin_NX_NVN.szs";

    // Loading the shader archive is expensive, so one analyzer is kept per archive file and version.
    private static readonly ConcurrentDictionary<(string Path, DateTime Modified), Lazy<ModelGeometryAllocationAnalyzer?>> geometryAnalyzers = new();

    private static ModelGeometryAllocationAnalyzer? GetGeometryAnalyzer(string entireFileName, string romfsName)
    {
        string file = Path.GetFullPath(entireFileName);
        if (file.EndsWith(".zs", StringComparison.OrdinalIgnoreCase))
            file = file[..^3];

        string relative = romfsName.Replace('/', Path.DirectorySeparatorChar);
        if (!file.EndsWith(Path.DirectorySeparatorChar + relative, StringComparison.OrdinalIgnoreCase))
            return null;

        string shaderPath = Path.Combine(file[..^relative.Length], SharedShaderArchivePath);
        if (!File.Exists(shaderPath))
            return null;

        return geometryAnalyzers.GetOrAdd((shaderPath, File.GetLastWriteTimeUtc(shaderPath)), key => new(() =>
        {
            try
            {
                return new ModelGeometryAllocationAnalyzer(SwitchShaderArchive.Load(key.Path));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException)
            {
                ConsoleUtilities.WriteLine($"Failed to load {key.Path}; geometry models keep the game's fallback size.\n{ex.Message}", ConsoleColor.Red);
                return null;
            }
        })).Value;
    }
}
