using HeavenTool.IO.FileFormats.BARS;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BWAV;

public class BinaryWaveFile
{
    public int FileSize { get; }
    public bool IsBigEndian { get; }
    public ushort Version { get; }
    private uint Hash { get; }
    public ushort IsPrefetch { get; }
    public AudioMetadata? Metadata { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BinaryWaveChannel[] Channels { get; }

    public BinaryWaveFile(int channelQuantity)
    {
        Channels = new BinaryWaveChannel[channelQuantity];
    }

    public BinaryWaveFile(byte[] buffer, AudioMetadata? metadata = null) : this(new MemoryStream(buffer), metadata)
    { 
    
    }

    public BinaryWaveFile(Stream stream, AudioMetadata? metadata = null)
    {
        using var reader = new BinaryReader(stream);

        Metadata = metadata;

        var MAGIC = reader.ReadString(4);
        if (MAGIC != "BWAV") throw new Exception($"File is not a BWAV ({MAGIC})!");

        FileSize = (int) stream.Length;
        IsBigEndian = reader.BigEndian = reader.ReadUInt16() == 0xFFFE;
        Version = reader.ReadUInt16();
        Hash = reader.ReadUInt32();
        IsPrefetch = reader.ReadUInt16();
        var channelCount = reader.ReadUInt16();

        Channels = new BinaryWaveChannel[channelCount];

        for (int i = 0; i < channelCount; i++)
            Channels[i] = new BinaryWaveChannel(reader);
    }

    public bool ValidateHash(string fileName) => fileName.ToCRC32() != Hash;

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("BWAV"u8);
        ushort endian = (ushort) (IsBigEndian ? 0xFFFE : 0xFEFF);
        writer.Write(endian);
        writer.Write(Version);
        writer.Write(Hash);
        writer.Write(IsPrefetch);
        writer.Write(Channels.Length);

        for (int i = 0; i < Channels.Length; i++)
            Channels[i].Write(writer);

        writer.Align(0x40); // Align to 64 bytes before writing audio data.

        // Write audio data for each channel, updating the audio offset values in the channel headers as we go.
        foreach (var channel in Channels)
        {
            if (channel.ChannelData == null)
                continue;

            channel.writeAudioOffsetNonPrefetch?.Resolve((uint) writer.Position);
            channel.writeAudioOffsetPrefetch?.Resolve((uint)writer.Position);

            writer.Write(channel.ChannelData);
            writer.Align(0x40);
        }

        return stream.ToArray();
    }
}