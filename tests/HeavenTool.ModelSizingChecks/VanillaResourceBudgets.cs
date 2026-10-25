using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AeonSake.NintendoTools.Compression.Yaz0;
using AeonSake.NintendoTools.Hashing;

internal sealed class VanillaResourceBudgets
{
    private readonly Dictionary<uint, uint> hashes = [];
    private readonly Dictionary<string, uint> names = new(StringComparer.Ordinal);
    private readonly Crc32Hash crc = new();
    public string Sha256 { get; private init; } = "";

    public static VanillaResourceBudgets Read(string romfs)
    {
        byte[] packed = File.ReadAllBytes(Path.Combine(romfs, "System/Resource/ResourceSizeTable.srsizetable"));
        using var input = new MemoryStream(packed);
        using var decoded = new MemoryStream();
        if (packed.AsSpan().StartsWith("Yaz0"u8)) new Yaz0Decompressor().Decompress(input, decoded);
        else decoded.Write(packed);
        byte[] table = decoded.ToArray();
        if (table.Length < 12 || !table.AsSpan(0, 4).SequenceEqual("RSTC"u8)) throw new InvalidDataException("Invalid vanilla RSTC header.");
        uint Read32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset, 4));
        int unique = checked((int)Read32(4)), named = checked((int)Read32(8));
        if (table.Length != 12L + unique * 12L + named * 136L) throw new InvalidDataException("Invalid vanilla RSTC length.");
        var result = new VanillaResourceBudgets { Sha256 = Convert.ToHexStringLower(SHA256.HashData(packed)) };
        for (int i = 0; i < unique; i++) result.hashes.Add(Read32(12 + i * 12), Read32(16 + i * 12));
        for (int i = 0; i < named; i++)
        {
            int at = 12 + unique * 12 + i * 136;
            result.names.Add(Encoding.UTF8.GetString(table, at, 128).TrimEnd('\0'), Read32(at + 128));
        }
        return result;
    }

    public uint? Find(string path)
    {
        if (names.TryGetValue(path, out uint value)) return value;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(path));
        uint hash = BinaryPrimitives.ReadUInt32BigEndian(crc.Compute(input));
        return hashes.TryGetValue(hash, out value) ? value : null;
    }
}
