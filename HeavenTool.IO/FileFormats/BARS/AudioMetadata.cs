using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

// The property names are probably all wrong. It is guessed by the value and not tested.
public class AudioMetadata
{
    public const string AMTA_MAGIC = "AMTA";
    public enum AudioMetadataVersion : ushort
    {
        V5 = 0x0500
    }

    public AudioMetadata(AudioAsset parent, BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(reader);

        Parent = parent;

        var initialPosition = reader.Position;

        var magic = reader.ReadString(4);
        if (magic != AMTA_MAGIC)
            throw new Exception($"Expected \"{AMTA_MAGIC}\" magic, but got \"{magic}\".");

        //Endian = reader.ReadUInt16();
        IsBigEndian = reader.BigEndian = reader.ReadUInt16() == 0xFFFE;
        Version = (AudioMetadataVersion)reader.ReadUInt16();
        Size = reader.ReadInt32(); // seems to align to 4, be sure to align to 4 when making the write method

        switch (Version)
        {
            case AudioMetadataVersion.V5:
                ReadAMTAV5(reader, initialPosition);
                break;

            default:
                throw new Exception($"Unsupported AMTA version: 0x{((ushort)Version):X4}");
        }
    }

    public AudioAsset Parent;
    public string AssetType => Parent.assetType;

    //public bool BigEndian => Endian == 0xFEFF;
    //private ushort Endian { get; set; }
    public bool IsBigEndian { get; }
    public AudioMetadataVersion Version { get; private set; }
    public int Size { get; private set; }

    // V5-specific fields
    public uint OffsetToData { get; private set; }
    //public string NameHashTranslated => $"0x{NameHash:x}";

    private uint OffsetToFooter;

    public uint DataSize { get; private set; }
    private uint NameHash { get; set; }
    public uint Unknown_C { get; private set; }
    public byte StreamCount { get; private set; }
    public byte ChannelCount { get; private set; }
    public byte Flags { get; private set; }
    public byte CodecId { get; private set; }
    public int Unknown_D { get; private set; } // when datasize == 40, always 257
    public int Unknown { get; private set; } // almost always 15
    public float LoopStart { get; private set; }
    public float LoopEnd { get; private set; }
    public float Volume { get; private set; }
    public float Loudness { get; private set; }
    public string AssetName { get; private set; }

    public uint? UnknownInt { get; private set; } = null;
    public uint? UnknownInt2 { get; private set; } = null;
    public string GameVersion { get; private set; }
    public byte UnknownByte { get; private set; }

    public void ReadAMTAV5(BinaryReader reader, long initialPosition)
    {
        reader.Skip(4); // padding

        OffsetToData = reader.ReadUInt32(); // Offset to "DATA" block

        reader.Skip(8); // padding?

        OffsetToFooter = reader.ReadUInt32();

        reader.Skip(4); // padding? again?

        // 
        DataSize = reader.ReadUInt32();
        NameHash = reader.ReadUInt32();
        Unknown_C = reader.ReadUInt32(); // ?

        StreamCount = reader.ReadByte();
        ChannelCount = reader.ReadByte();
        Flags = reader.ReadByte();
        CodecId = reader.ReadByte();

        if (OffsetToData == 56)
        {
            Unknown_D = reader.ReadInt32();
        }

        // Init of 'DATA' block
        Unknown = reader.ReadInt32(); // Always 15

        LoopStart = reader.ReadSingle();
        LoopEnd = reader.ReadSingle();
        Volume = reader.ReadSingle();
        Loudness = reader.ReadSingle();

        // Init of 'FOOTER' block
        if (OffsetToFooter != 0)
            ReadFileFooter(reader, initialPosition + OffsetToFooter);
        
        AssetName = reader.ReadTerminatedStringAt(initialPosition + DataSize + 36); // Data Size + 36 (Header Size)
    }

    private void ReadFileFooter(BinaryReader reader, long location)
    {
        using (reader.CreateScopeAt(location))
        {
            UnknownInt = reader.ReadUInt32();
            UnknownInt2 = reader.ReadUInt32();

            GameVersion = reader.ReadTerminatedString(7);
        }
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        var binaryWriter = new BinaryWriter(ms);

        binaryWriter.Write(AMTA_MAGIC);
        binaryWriter.Write((ushort) 0xFFFE);
        binaryWriter.Write((ushort) Version);
        
        var sizePosition = binaryWriter.Position;
        binaryWriter.Write(0); // size
        
        switch (Version)
        {
            case AudioMetadataVersion.V5:
                WriteAMTAV5(binaryWriter);
                break;

            default: throw new Exception($"Unsupported AMTA version: 0x{((ushort)Version):X4}");
        }

        binaryWriter.Align(4);

        using (binaryWriter.CreateScope())
            binaryWriter.WriteAt(sizePosition, (int) binaryWriter.Length);

        return ms.ToArray();
    }

    private void WriteAMTAV5(BinaryWriter writer)
    {
        writer.Skip(4);
        writer.Write(OffsetToData);
        writer.Skip(8);
        writer.Write(OffsetToFooter);
        writer.Skip(4);

        var currentDataPosition = (uint) writer.Position;
        writer.Skip(4); // DataSize is written after FileFooter

        writer.Write(NameHash);
        writer.Write(Unknown_C); // ?

        writer.Write(StreamCount);
        writer.Write(ChannelCount);
        writer.Write(Flags);
        writer.Write(CodecId);

        writer.Write(Unknown);

        writer.Write(LoopStart);
        writer.Write(LoopEnd);
        writer.Write(Volume);
        writer.Write(Loudness);

        WriteFileFooter(writer);

        uint dataSize = (uint) writer.Length - currentDataPosition;
        using (writer.CreateScope())
            writer.WriteAt(currentDataPosition, dataSize);

        writer.WriteTerminatedString(AssetName);
        writer.Align(4);
    }

    private void WriteFileFooter(BinaryWriter writer)
    {
        if (UnknownInt != null && UnknownInt2 != null && GameVersion != null)
        {
            writer.Write(UnknownInt.Value);
            writer.Write(UnknownInt2.Value);
            writer.WriteTerminatedString(GameVersion);
        }
    }

    public override string ToString() => AssetName;
}