using System.Buffers.Binary;
using System.Text;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>
/// Bounds-checked little-endian reads over a resource's bytes. Reads outside the data throw
/// <see cref="InvalidDataException"/>, so malformed files never produce a partial size.
/// </summary>
internal readonly ref struct ResourceReader(ReadOnlySpan<byte> data)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> data = data;

    public int Length => data.Length;

    public ReadOnlySpan<byte> Range(long at, long count) =>
        at < 0 || count < 0 || at > data.Length - count
            ? throw new InvalidDataException("Resource data exceeds its bounds.")
            : data.Slice((int)at, (int)count);

    public ResourceReader Slice(long at, long count) => new(Range(at, count));
    public byte Byte(long at) => Range(at, 1)[0];
    public ushort U16(long at) => BinaryPrimitives.ReadUInt16LittleEndian(Range(at, 2));
    public uint U32(long at) => BinaryPrimitives.ReadUInt32LittleEndian(Range(at, 4));
    public ulong U64(long at) => BinaryPrimitives.ReadUInt64LittleEndian(Range(at, 8));
    public uint U32BigEndian(long at) => BinaryPrimitives.ReadUInt32BigEndian(Range(at, 4));

    public bool Magic(long at, ReadOnlySpan<byte> magic) => Range(at, magic.Length).SequenceEqual(magic);

    public void RequireMagic(long at, ReadOnlySpan<byte> magic)
    {
        if (!Magic(at, magic)) throw new InvalidDataException("Expected " + Encoding.ASCII.GetString(magic));
    }

    /// <summary>A relocated 64-bit offset into this data (BFRES, BNTX); zero is a null pointer.</summary>
    public int Pointer(long at)
    {
        ulong value = U64(at);
        return value <= (ulong)data.Length ? (int)value : throw new InvalidDataException("Resource pointer exceeds its bounds.");
    }

    /// <summary>A BFRES string: a 16-bit length, strict UTF-8 bytes and a terminator. A null pointer reads as "".</summary>
    public string Name(long at)
    {
        if (at == 0) return "";
        int length = U16(at);
        if (Byte(at + 2 + length) != 0) throw new InvalidDataException("Unterminated BFRES string.");
        var bytes = Range(at + 2, length);
        if (bytes.Contains((byte)0)) throw new InvalidDataException("BFRES string contains an embedded terminator.");
        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("BFRES string is not valid UTF-8.", error); }
    }
}
