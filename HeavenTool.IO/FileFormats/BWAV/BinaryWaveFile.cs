using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BWAV;

public class BinaryWaveFile : IDisposable
{
    public bool IsBigEndian { get; }
    public ushort Version { get; } = 1;
    private uint Hash { get; }
    public AssetType AssetType { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BinaryWaveChannel[] Channels { get; }

    public BinaryWaveFile(byte[] buffer) : this(new MemoryStream(buffer))
    {

    }

    public BinaryWaveFile(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        var MAGIC = reader.ReadString(4);
        if (MAGIC != "BWAV") throw new Exception($"File is not a BWAV ({MAGIC})!");

        IsBigEndian = reader.BigEndian = reader.ReadUInt16() == 0xFFFE;
        Version = reader.ReadUInt16();
        if (Version != 1)
            throw new NotSupportedException($"Unsupported BWAV version: {Version}");
        Hash = reader.ReadUInt32();
        AssetType = (AssetType)reader.ReadUInt16();
        if (AssetType is not AssetType.Normal and not AssetType.Prefetch)
            throw new InvalidDataException($"Invalid BWAV asset type: {AssetType}");
        var channelCount = reader.ReadUInt16();

        Channels = new BinaryWaveChannel[channelCount];

        for (var i = 0; i < channelCount; i++)
            Channels[i] = new BinaryWaveChannel(reader);
    }

    // BWAV hashes describe encoded channel data, unlike BARS/AMTA name hashes.
    public bool ValidateHash() => Hash == CalculateDataHash();

    private uint CalculateDataHash()
    {
        using var samples = new MemoryStream();
        foreach (var channel in Channels)
            if (channel.ChannelData != null)
                samples.Write(channel.ChannelData);
        return Force.Crc32.Crc32Algorithm.Compute(samples.ToArray());
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.BigEndian = IsBigEndian;

        writer.Write("BWAV"u8);
        writer.Write((ushort)0xFEFF);
        writer.Write(Version);
        writer.Write(AssetType == AssetType.Prefetch ? Hash : CalculateDataHash());
        writer.Write((ushort)AssetType);
        writer.Write((ushort)Channels.Length);

        for (var i = 0; i < Channels.Length; i++)
            Channels[i].Write(writer, AssetType == AssetType.Prefetch);

        writer.Align(0x40); // Align to 64 bytes before writing audio data.

        foreach (var channel in Channels)
        {
            if (channel.ChannelData == null)
                continue;

            var offsetPosition = (uint)writer.Position;
            channel.writeTotalSamples?.Resolve(offsetPosition);
            channel.writeLocalSamples?.Resolve(offsetPosition);

            writer.Write(channel.ChannelData);
            writer.Align(0x40);
        }

        return stream.ToArray();
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var channel in Channels)
                channel.Dispose();
        }

        _disposed = true;
    }
}