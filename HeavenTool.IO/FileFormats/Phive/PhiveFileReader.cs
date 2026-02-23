using System;
using System.Collections.Generic;
using System.Text;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.Phive;

public class PhiveFileReader
{
    internal readonly byte[] MAGIC = "Phive\0"u8.ToArray();

    public uint Reserve1 { get; }
    public bool BigEndian { get; }
    public byte MajorVersion { get; }
    public byte MinorVersion { get; }
    public uint HktOffset { get; }
    public uint TableOffset0 { get; }
    public uint TableOffset1 { get; }
    public uint FileSize { get; }
    public uint HktSize { get; }
    public uint TableSize0 { get; }
    public uint TableSize1 { get; }
    public uint Reserve2 { get; }
    public uint Reserve3 { get; }

    public PhiveFileReader(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadByteArray(6);

        if (!MAGIC.SequenceEqual(magic))
            throw new Exception("This is not a Phive file!");

        Reserve1 = reader.ReadUInt32();
        BigEndian = reader.ReadUInt16() != 0xFEFF;
        MajorVersion = reader.ReadByte();
        MinorVersion = reader.ReadByte();
        HktOffset = reader.ReadUInt32();
        TableOffset0 = reader.ReadUInt32();
        TableOffset1 = reader.ReadUInt32();
        FileSize = reader.ReadUInt32();
        HktSize = reader.ReadUInt32();
        TableSize0 = reader.ReadUInt32();
        TableSize1 = reader.ReadUInt32();
        Reserve2 = reader.ReadUInt32();
        Reserve3 = reader.ReadUInt32();
    }

    public PhiveFileReader(byte[] data) : this(new MemoryStream(data))
    { }


    
}
