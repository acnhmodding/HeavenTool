using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.Common;

public class BinaryFileHeader
{
    public BinaryFileHeader(BinaryReader reader, string expectedMagic)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(expectedMagic);
        Magic = reader.ReadString(0x8).TrimEnd();

        if (Magic != expectedMagic) throw new Exception($"Expected \"{expectedMagic}\" magic, but got \"{Magic}\".");

        MicroVersion = reader.ReadByte();
        MinorVersion = reader.ReadByte();
        MajorVersion = reader.ReadUInt16();
        BigEndian = reader.ReadUInt16() == 0xFFFE;
        Alignment = reader.ReadByte();
        AddressSize = reader.ReadByte();
        var fileNameOffset = reader.ReadUInt32();
        IsRealocated = reader.ReadUInt16() == 1;
        BlockOffset = reader.ReadUInt16();
        RelocationTableOffset = reader.ReadUInt32();
        FileSize = reader.ReadUInt32();

        //using (reader.CreateScopeAt(FileSize))
        //{
        //    GPUDataOffset = reader.ReadUInt32();
        //    GPUBufferSize = reader.ReadUInt32();
        //}
    }

    public string Magic { get; }
    public byte MicroVersion { get; }
    public byte MinorVersion { get; }
    public ushort MajorVersion { get; }
    public bool BigEndian { get; }
    public byte Alignment { get; }
    public byte AddressSize { get; }
    public bool IsRealocated { get; }
    public ushort BlockOffset { get; }
    public uint RelocationTableOffset { get; }
    public uint FileSize { get; }
    public uint GPUDataOffset { get; }
    public uint GPUBufferSize { get; }
}
