using HeavenTool.IO.Audio;
using HeavenTool.IO.FileFormats.BARS.AMTA;
using HeavenTool.IO.FileFormats.BARS.MINF;
using HeavenTool.IO.FileFormats.BARS.MINF.Sections;
using HeavenTool.IO.FileFormats.BWAV;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

public class AudioMetadata
{
    public const string AMTA_MAGIC = "AMTA";

    public enum AudioMetadataVersion : ushort
    {
        V1 = 0x0100,
        V2 = 0X0200,
        V3 = 0x0300,
        V4 = 0x0400,
        V5 = 0x0500
    }

    public AudioMetadata(byte[] data, PerformanceSequenceFormat? performanceFormat = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var initialPosition = reader.Position;

        var magic = reader.ReadString(4);
        if (magic != AMTA_MAGIC)
            throw new Exception($"Expected \"{AMTA_MAGIC}\" magic, but got \"{magic}\".");

        IsBigEndian = reader.BigEndian = reader.ReadUInt16() == 0xFFFE;
        Version = (AudioMetadataVersion)reader.ReadUInt16();
        Size = reader.ReadInt32(); // seems to align to 4, be sure to align to 4 when making the write method

        switch (Version)
        {
            case AudioMetadataVersion.V5:
                ReadAMTAV5(reader, initialPosition, performanceFormat);
                break;

            default:
                throw new Exception($"Unsupported AMTA version: 0x{((ushort)Version):X4}");
        }
    }

    internal void UseWave(ResTrack[] tracks, BinaryWaveFile wave, bool streaming)
    {
        Tracks = tracks;
        Flags &= ~(MetadataFlags.IsStreaming | MetadataFlags.IsLoop | MetadataFlags.HasLocalSamples);
        var channel = wave.Channels[0];
        if (channel.LocalSampleCount > 0) Flags |= MetadataFlags.HasLocalSamples;
        if (channel.LoopEnd > channel.LoopStart) Flags |= MetadataFlags.IsLoop;
        if (streaming) Flags |= MetadataFlags.IsStreaming;
    }

    internal ResTrack[] GetReplacementTracks(BinaryWaveFile? previous, BinaryWaveFile replacement)
    {
        var channels = replacement.Channels;
        if (Tracks.Length == 0) return Tracks;
        if (previous != null && previous.Channels.Select(c => c.ChannelPan).SequenceEqual(channels.Select(c => c.ChannelPan)))
            return Tracks;
        if (channels.Length > byte.MaxValue)
            throw new InvalidDataException("AMTA supports at most 255 channels per track.");

        var previousChannels = previous?.Channels ?? [];
        var available = new SortedSet<int>(Enumerable.Range(0, channels.Length));
        var map = new Dictionary<int, int>();
        for (int old = 0; old < previousChannels.Length; old++)
        {
            var pan = previousChannels[old].ChannelPan;
            int index = available.FirstOrDefault(i => pan != PanType.Invalid && channels[i].ChannelPan == pan, -1);
            if (index < 0) continue;
            map.Add(old, index);
            available.Remove(index);
        }
        for (int old = 0; old < previousChannels.Length; old++)
        {
            if (map.ContainsKey(old)) continue;
            int index = available.Contains(old) ? old : available.FirstOrDefault(-1);
            if (index < 0) break;
            map.Add(old, index);
            available.Remove(index);
        }

        ResStreamChannel Remap(ResStreamChannel route)
        {
            int index = map[route.ChannelIndex];
            return previousChannels[route.ChannelIndex].ChannelPan == channels[index].ChannelPan
                ? new ResStreamChannel { ChannelIndex = checked((byte)index), OutputChannel = route.OutputChannel }
                : AudioChannelRouting.CreateRoute(index, channels[index].ChannelPan);
        }
        var tracks = Tracks.Select(t => new ResTrack
        {
            Channels = [.. t.Channels.Where(c => map.ContainsKey(c.ChannelIndex)).Select(Remap)]
        }).ToArray();
        tracks[0].Channels = [.. tracks[0].Channels, .. available.Select(i => AudioChannelRouting.CreateRoute(i, channels[i].ChannelPan))];
        if (tracks[0].Channels.Length > byte.MaxValue)
            throw new InvalidDataException("Replacement channel routing exceeds the AMTA track limit.");
        return tracks;
    }

    private uint ReservedOffset { get; set; }

    public bool IsBigEndian { get; }
    public AudioMetadataVersion Version { get; private set; }
    public int Size { get; private set; }

    public MetadataFlags Flags { get; private set; }
    public ResTrack[] Tracks { get; private set; } = [];

    public OptionalMetadata? OptionalData { get; private set; }
    public string AssetName { get; private set; } = "";
    public List<string> Identifiers { get; private set; } = [];
    public record AttributeEntry(string Key, uint Value);
    public List<AttributeEntry> Attributes { get; } = [];

    [Browsable(false)]
    public MarkerList? MarkerList { get; private set; }

    [Browsable(false)]
    public MusicInfo? MusicInfo { get; private set; }

    private void ReadAMTAV5(BinaryReader reader, long initialPosition, PerformanceSequenceFormat? performanceFormat)
    {
        ReservedOffset = reader.ReadUInt32();
        var dataOffset = reader.ReadUInt32(); // Offset to "DATA" block
        var markerOffset = reader.ReadUInt32();
        var minfOffset = reader.ReadUInt32();
        var footerOffset = reader.ReadUInt32();
        var attrOffset = reader.ReadUInt32();

        var assetNameOffset = reader.ReadUInt32();
        using (reader.CreateScope())
            AssetName = reader.ReadTerminatedStringAt(reader.Position - 4 + assetNameOffset);

        var assetNameHash = reader.ReadUInt32();

        if (AssetName.ToCRC32() != assetNameHash)
            ConsoleUtilities.WriteLine($"Asset name hash mismatch! Calculated: 0x{AssetName.ToCRC32():X8}, Expected: 0x{assetNameHash:X8}", ConsoleColor.Yellow);

        Flags = (MetadataFlags)reader.ReadUInt32();

        var trackCount = reader.ReadByte();
        Tracks = new ResTrack[trackCount];

        for (byte i = 0; i < trackCount; i++)
            Tracks[i] = new ResTrack(reader);

        if (dataOffset != 0)
            using (reader.CreateScopeAt(initialPosition + dataOffset))
                OptionalData = OptionalMetadata.Read(reader);

        if (markerOffset != 0)
            MarkerList = new MarkerList(reader, initialPosition + markerOffset);

        if (minfOffset != 0)
        {
            using var scope = reader.CreateScopeAt(initialPosition + minfOffset);
            reader.Skip(4);
            var oldEndian = reader.BigEndian;
            reader.BigEndian = reader.ReadUInt16() == (oldEndian ? 0xFEFF : 0xFFFE);
            try
            {
                reader.Skip(2);
                var minfSize = reader.ReadInt32();
                if (minfSize < 0x4c || minfSize > reader.Length - (initialPosition + minfOffset))
                    throw new InvalidDataException("MINF size is outside AMTA.");
                var rawMusicInfo = reader.ReadByteArrayAt(initialPosition + minfOffset, minfSize);
                using var minfStream = new MemoryStream(rawMusicInfo);
                using var minfReader = new BinaryReader(minfStream);
                MusicInfo = new MusicInfo(minfReader, 0, performanceFormat ?? AcnhPerformanceContext.ForAsset(AssetName));
            }
            finally { reader.BigEndian = oldEndian; }
        }
        if (footerOffset != 0)
            ReadFileFooter(reader, initialPosition + footerOffset);
        if (attrOffset != 0)
            using (reader.CreateScopeAt(initialPosition + attrOffset))
            {
                var count = reader.ReadUInt32();
                for (uint i = 0; i < count; i++)
                    Attributes.Add(new AttributeEntry(reader.ReadStringPointer(), reader.ReadUInt32()));
            }
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
        using var binaryWriter = new BinaryWriter(ms);
        binaryWriter.BigEndian = IsBigEndian;

        binaryWriter.Write(AMTA_MAGIC);
        binaryWriter.Write((ushort)0xFEFF);
        binaryWriter.Write((ushort)Version);
        var sizePosition = binaryWriter.CreatePointer();

        switch (Version)
        {
            case AudioMetadataVersion.V5:
                WriteAMTAV5(binaryWriter);
                break;

            default: throw new Exception($"Unsupported AMTA version: 0x{((ushort)Version):X4}");
        }

        binaryWriter.Align(4);

        sizePosition.Resolve((uint)binaryWriter.Length);
        return ms.ToArray();
    }

    private void WriteAMTAV5(BinaryWriter writer)
    {
        const long initialPosition = 0;
        var stringPool = new StringPool();
        writer.Write(ReservedOffset);
        var dataPointer = writer.CreatePointer();
        var markerPointer = writer.CreatePointer();
        var minfPointer = writer.CreatePointer();
        var footerPointer = writer.CreatePointer();
        var attrPointer = writer.CreatePointer();
        var assetNamePointer = writer.CreatePointer();

        writer.Write(AssetName.ToCRC32());
        writer.Write((uint)Flags);

        writer.Write(checked((byte)Tracks.Length));
        foreach (var track in Tracks)
        {
            writer.Write(checked((byte)track.Channels.Length));
            foreach (var channel in track.Channels)
            {
                writer.Write(channel.ChannelIndex);
                writer.Write((byte)channel.OutputChannel);
            }
        }

        writer.Align(4);
        if (OptionalData != null)
            dataPointer.Resolve(OptionalData.Write, initialPosition);

        if (MarkerList != null)
        {
            writer.Align(4);
            markerPointer.Resolve(w => MarkerList.Write(w, stringPool), initialPosition);
        }

        if (MusicInfo != null)
        {
            writer.Align(4);
            minfPointer.Resolve(w => w.Write(MusicInfo.ToBytes()), initialPosition);
        }

        if (Identifiers.Count != 0)
        {
            writer.Align(4);
            footerPointer.Resolve(w => WriteFileFooter(w, stringPool), initialPosition);
        }
        if (Attributes.Count != 0)
        {
            writer.Align(4);
            attrPointer.Resolve(w =>
            {
                w.Write(Attributes.Count);
                foreach (var attribute in Attributes)
                {
                    stringPool.AddString(w, attribute.Key);
                    w.Write(attribute.Value);
                }
            }, initialPosition);
        }
        writer.Align(4);
        stringPool.Strings.TryAdd(AssetName, []);
        stringPool.Strings[AssetName].Add(assetNamePointer);
        stringPool.Write(writer);
        writer.Align(4);
    }

    private void WriteFileFooter(BinaryWriter writer, StringPool stringPool)
    {
        writer.Write(Identifiers.Count);
        foreach (var identifier in Identifiers)
            stringPool.AddString(writer, identifier);
    }

    public override string ToString() => AssetName;
}
