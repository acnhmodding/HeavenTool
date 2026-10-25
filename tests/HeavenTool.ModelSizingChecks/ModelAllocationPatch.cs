using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeonSake.NintendoTools.Compression.Yaz0;
using AeonSake.NintendoTools.Hashing;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

internal static class ModelAllocationPatch
{
    public static void Create(string romfs, string target)
    {
        string source = Path.GetFullPath(Path.Combine(romfs, "System/Resource/ResourceSizeTable.srsizetable"));
        target = Path.GetFullPath(target);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Write the candidate separately from the installed table.");
        byte[] original = File.ReadAllBytes(source);
        using var input = new MemoryStream(original); using var raw = new MemoryStream();
        new Yaz0Decompressor().Decompress(input, raw); byte[] data = raw.ToArray();
        int unique = checked((int)Read32(data, 4)), named = checked((int)Read32(data, 8));
        if (!data.AsSpan(0, 4).SequenceEqual("RSTC"u8) || data.Length != 12L + unique * 12L + named * 136L) throw new InvalidDataException("Invalid RSTC");
        var allocations = new Dictionary<uint, (string path, long size)>(); var crc = new Crc32Hash();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(romfs, "Model"), "*.zs", SearchOption.AllDirectories)) {
            string relative = Path.GetRelativePath(romfs, path).Replace('\\', '/')[..^3];
            if (relative.StartsWith("Model/Layout_", StringComparison.Ordinal)) continue;
            using var name = new MemoryStream(Encoding.UTF8.GetBytes(relative));
            uint hash = BinaryPrimitives.ReadUInt32BigEndian(crc.Compute(name));
            allocations.Add(hash, (relative, ResourceSizeTable.GetFileSize(path, relative)));
        }
        var entries = new SortedDictionary<uint, byte[]>(); var namedEntries = new List<byte[]>();
        var handled = new HashSet<uint>(); int updated = 0, removed = 0, added = 0;
        for (int i = 0; i < unique; i++) {
            byte[] record = data.AsSpan(12 + i * 12, 12).ToArray(); uint hash = Read32(record, 0);
            if (allocations.TryGetValue(hash, out var allocation)) {
                handled.Add(hash);
                if (allocation.size < 0) { removed++; continue; }
                if (allocation.size != Read32(record, 4)) { Write32(record, 4, checked((uint)allocation.size)); updated++; }
            }
            entries.Add(hash, record);
        }
        for (int i = 0; i < named; i++) {
            byte[] record = data.AsSpan(12 + unique * 12 + i * 136, 136).ToArray();
            string path = Encoding.UTF8.GetString(record, 0, 128).TrimEnd('\0');
            using var name = new MemoryStream(Encoding.UTF8.GetBytes(path));
            uint hash = BinaryPrimitives.ReadUInt32BigEndian(crc.Compute(name));
            if (allocations.TryGetValue(hash, out var allocation)) {
                if (path != allocation.path) throw new InvalidDataException("A model path collides with a named RSTC entry; preserve fallback until reviewed.");
                handled.Add(hash);
                if (allocation.size < 0) { removed++; continue; }
                if (allocation.size != Read32(record, 128)) { Write32(record, 128, checked((uint)allocation.size)); updated++; }
            }
            namedEntries.Add(record);
        }
        foreach (var (hash, allocation) in allocations) {
            if (allocation.size < 0 || handled.Contains(hash)) continue;
            byte[] record = new byte[12]; Write32(record, 0, hash); Write32(record, 4, checked((uint)allocation.size));
            entries.Add(hash, record); added++;
        }
        using var result = new MemoryStream();
        using (var writer = new BinaryWriter(result, Encoding.UTF8, true)) {
            writer.Write("RSTC"u8); writer.Write(entries.Count); writer.Write(namedEntries.Count);
            foreach (byte[] record in entries.Values) writer.Write(record);
            foreach (byte[] record in namedEntries) writer.Write(record);
        }
        result.Position = 0; using var packed = new MemoryStream(); new Yaz0Compressor().Compress(result, packed);
        packed.Position = 0; using var reloaded = new MemoryStream(); new Yaz0Decompressor().Decompress(packed, reloaded);
        if (!result.ToArray().AsSpan().SequenceEqual(reloaded.ToArray())) throw new InvalidDataException("Compression round trip failed");
        if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(source))) throw new IOException("Installed source changed during calculation");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, packed.ToArray());
        File.WriteAllBytes(target + ".original", original);
        var report = new { source, target, added, updated, removed, supported = allocations.Values.Count(a => a.size >= 0), fallback = allocations.Values.Count(a => a.size < 0),
            originalSha256 = Convert.ToHexString(SHA256.HashData(original)), candidateSha256 = Convert.ToHexString(SHA256.HashData(packed.ToArray())),
            installedSourceUnchanged = true, compressionRoundTrip = true };
        File.WriteAllText(target + ".verification.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
    private static uint Read32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    private static void Write32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);
}
