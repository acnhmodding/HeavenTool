using HeavenTool.IO.FileFormats.BARS.MINF;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

// The property names are probably all wrong. It is guessed by the value and not tested.
public class AudioMetadata
{
    [Flags]
    public enum MetadataFlags
    {
        None = 0,           
        Unknown1 = 1 << 0,
        Unknown2 = 1 << 1,
        IsLooped = 1 << 2, 
        Unknown4 = 1 << 3,
        Unknown5 = 1 << 4  
    }

    public const string AMTA_MAGIC = "AMTA";
    public enum AudioMetadataVersion : ushort
    {
        V5 = 0x0500
    }

    public AudioMetadata(AudioAsset parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.RawAudioMetadata == null)
            throw new NullReferenceException("Raw audio metadata was not found on file");

        using var stream = new MemoryStream(parent.RawAudioMetadata);
        using var reader = new BinaryReader(stream);

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

    private StringPool StringPool { get; set; } = new();

    public bool IsBigEndian { get; }
    public AudioMetadataVersion Version { get; private set; }
    public int Size { get; private set; }

    // V5-specific fields
    public uint Unknown_C { get; private set; }
    public byte StreamCount { get; private set; }
    public byte ChannelCount { get; private set; }
    public MetadataFlags Flags { get; private set; }
    public byte CodecId { get; private set; }
    public int? Unknown_D { get; private set; } // when datasize == 40, always 257
    public int Unknown { get; private set; } // almost always 15
    public float LoopStart { get; private set; }
    public float LoopEnd { get; private set; }
    public float Volume { get; private set; }
    public float Loudness { get; private set; }
    public string AssetName { get; private set; } = "";
    public List<string> Identifiers { get; private set; } = [];

    [Browsable(false)]
    public MarkerList? MarkerList { get; private set; }

    [Browsable(false)]
    public MINFReader? MINF { get; private set; }

    public void ReadAMTAV5(BinaryReader reader, long initialPosition)
    {
        reader.Skip(4); // padding - OFFSET to specific regions
        var dataOffset = reader.ReadUInt32(); // Offset to "DATA" block
        var markerOffset = reader.ReadUInt32();
        var minfOffset = reader.ReadUInt32();
        var footerOffset = reader.ReadUInt32();
        reader.Skip(4); // padding

        var assetNameOffset = reader.ReadUInt32();
        using (reader.CreateScope())
            AssetName = reader.ReadTerminatedStringAt(reader.Position - 4 + assetNameOffset);

        var assetNameHash = reader.ReadUInt32();

        if (AssetName.ToCRC32() != assetNameHash)
            ConsoleUtilities.WriteLine($"Asset name hash mismatch! Calculated: 0x{AssetName.ToCRC32():X8}, Expected: 0x{assetNameHash:X8}", ConsoleColor.Yellow);

        Unknown_C = reader.ReadUInt32(); // Seems to be "Type" where 1 is a external stream (located inside "Stream" folder),
                                         // 0 is inside the file itself, 3/5 probably is villager singing (MINF?)

        StreamCount = reader.ReadByte();
        ChannelCount = reader.ReadByte();
        CodecId = reader.ReadByte();
        Flags = (MetadataFlags) reader.ReadByte();

        if (dataOffset == 56)
        {
            // This is problably extra info, so probably 2 bytes with a 4-alignment
            Unknown_D = reader.ReadInt32();
        }

        // Init of 'DATA' block
        Unknown = reader.ReadInt32(); // Always 15

        LoopStart = reader.ReadSingle();
        LoopEnd = reader.ReadSingle();
        Volume = reader.ReadSingle();
        Loudness = reader.ReadSingle();

        // Read 'MARKER' block
        if (markerOffset != 0)
            MarkerList = new MarkerList(reader, initialPosition + markerOffset);

        if (minfOffset != 0)
            MINF = new MINFReader(reader, initialPosition + minfOffset);

        // Init of 'FOOTER' block
        if (footerOffset != 0)
            ReadFileFooter(reader, initialPosition + footerOffset);
    }

    private void ReadFileFooter(BinaryReader reader, long location)
    {
        using (reader.CreateScopeAt(location))
        {
            var entries = reader.ReadInt32();

            for (uint i = 0; i < entries; i++)
                Identifiers.Add(reader.ReadStringPointer());
        }
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        var binaryWriter = new BinaryWriter(ms);

        binaryWriter.Write(AMTA_MAGIC);
        binaryWriter.Write((ushort) 0xFFFE);
        binaryWriter.Write((ushort) Version);
        var sizePosition = binaryWriter.CreatePointer();

        switch (Version)
        {
            case AudioMetadataVersion.V5:
                WriteAMTAV5(binaryWriter);
                break;

            default: throw new Exception($"Unsupported AMTA version: 0x{((ushort)Version):X4}");
        }

        binaryWriter.Align(4);

        sizePosition.Resolve((uint) binaryWriter.Length);
        return ms.ToArray();
    }

    private void WriteAMTAV5(BinaryWriter writer)
    {
        var initialPosition = writer.Position;
        writer.Skip(4);
        var dataPointer = writer.CreatePointer();
        var markerPointer = writer.CreatePointer();
        var minfPointer = writer.CreatePointer();
        var footerPointer = writer.CreatePointer();
        writer.Skip(4);

        var assetNamePointer = writer.CreatePointer();

        writer.Write(AssetName.ToCRC32());
        writer.Write(Unknown_C); // ?

        writer.Write(StreamCount);
        writer.Write(ChannelCount);
        writer.Write(CodecId);
        writer.Write((byte)Flags);

        if (Unknown_D.HasValue)
            writer.Write(Unknown_D.Value);

        // * "DATA" block * //
        dataPointer.Resolve(w =>
        {
            w.Write(Unknown);

            w.Write(LoopStart);
            w.Write(LoopEnd);
            w.Write(Volume);
            w.Write(Loudness);
        }, initialPosition);

        // * "MARKER" block * //
        if (MarkerList != null)
            markerPointer.Resolve(w =>
            {
                MarkerList.Write(w, StringPool);
            }, initialPosition);

        // * "MINF" block * //
        if (MINF != null)
            minfPointer.Resolve(w => {
                // MINF.Write(w, StringPool);
            }, initialPosition);

        // * "FOOTER" block * //
        footerPointer.Resolve(WriteFileFooter, initialPosition);

        StringPool.Write(writer);

        uint assetNameOffset = (uint) writer.Position - (uint)assetNamePointer.Position;
        assetNamePointer.Resolve(assetNameOffset);

        writer.WriteTerminatedString(AssetName);
        writer.Align(4);
    }

    private void WriteFileFooter(BinaryWriter writer)
    {
        writer.Write(Identifiers.Count);
        foreach (var identifier in Identifiers)
            StringPool.AddString(writer, identifier);   
    }

    public override string ToString() => AssetName;
}