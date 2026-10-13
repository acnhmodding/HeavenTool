using HeavenTool.IO.Audio.DspAdpcm;
using System.Buffers.Binary;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BWAV;

public class BinaryWaveChannel : IDisposable
{
    public record AdpcmContext(ushort PredictorScale, short[] History)
    {
        public static AdpcmContext Read(BinaryReader r) => new(r.ReadUInt16(), r.ReadInt16Array(2));
        public void Write(BinaryWriter writer)
        {
            writer.Write(PredictorScale);
            writer.Write(History);
        }
    }

    public record BinaryWaveLoopInfo(uint LoopStart, AdpcmContext AdpcmContext, ushort Padding = 0)
    {
        public static BinaryWaveLoopInfo Read(BinaryReader r) => new(r.ReadUInt32(), AdpcmContext.Read(r), r.ReadUInt16());
        public void Write(BinaryWriter writer)
        {
            writer.Write(LoopStart);
            AdpcmContext.Write(writer);
            writer.Write(Padding);
        }
    }

    [Category("Audio Configuration")]
    public CodecType Codec { get; set; } = CodecType.Adpcm;

    [Category("Audio Configuration")]
    public PanType ChannelPan { get; set; } = PanType.Center;

    [Category("Audio Configuration")]
    public int SampleRate { get; set; } = 48000;

    /// <summary>
    /// Sample count in total
    /// </summary>
    [Category("Audio Configuration")]
    public uint TotalSampleCount { get; set; }

    /// <summary>
    /// Sample count in this file, which is usually the same as <see href="TotalSampleCount"/> unless the file is prefetched.
    /// </summary>
    [Category("Audio Configuration")]
    public int LocalSampleCount { get; set; }

    /// <summary>
    /// Coefficients for ADPCM decoding, this is only used when <see href="Codec"/> is <see cref="CodecType.Adpcm"/>
    /// </summary>
    [Category("Audio Configuration")]
    public short[]? Coefficients { get; set; }

    [Category("Looping")]
    public ushort LoopCount => checked((ushort)LoopInfo.Length);

    [Category("Looping")]
    public ushort LoopSeekPointIndex { get; private set; }

    [Category("Looping")]
    public int LoopEnd { get; private set; } = -1;

    [Category("Looping")]
    public int LoopStart => LoopSeekPointIndex < LoopInfo.Length
        ? checked((int)LoopInfo[LoopSeekPointIndex].LoopStart) : 0;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public byte[]? ChannelData { get; internal set; }

    public BinaryWaveLoopInfo[] LoopInfo { get; } = [];
    public uint TotalSamplesOffset { get; private set; }
    private bool IsBigEndian { get; }

    internal WriterScopePointer? writeTotalSamples;
    internal WriterScopePointer? writeLocalSamples;

    public BinaryWaveChannel(byte[] channelData, short[] coeffs, int sampleRate = 48000)
    {
        Codec = CodecType.Adpcm;
        SampleRate = sampleRate;
        ChannelData = channelData;
        Coefficients = coeffs;
        LoopInfo = [new(0, new AdpcmContext(channelData.Length > 0 ? channelData[0] : (ushort)0, [0, 0]))];
    }

    public BinaryWaveChannel(BinaryReader reader)
    {
        IsBigEndian = reader.BigEndian;
        var codecType = reader.ReadUInt16();
        if (codecType > 1)
            throw new Exception($"Invalid Codec Type not supported ({codecType})!");

        Codec = (CodecType) codecType;

        var channelPan = reader.ReadUInt16();

        if (channelPan > 6)
            throw new Exception($"Invalid channel pan ({channelPan})!");

        ChannelPan = (PanType) channelPan;
        SampleRate = reader.ReadInt32();
        TotalSampleCount = reader.ReadUInt32();
        LocalSampleCount = reader.ReadInt32();

        if (Codec == CodecType.Adpcm)
            Coefficients = reader.ReadInt16Array(DspAdpcmCodec.CoefficientCount);
        else if (Codec == CodecType.PcmInt16)
            reader.Skip(0x20);

        TotalSamplesOffset = reader.ReadUInt32();
        var audioOffset = reader.ReadUInt32();
        var seekPointCount = reader.ReadUInt16();
        LoopSeekPointIndex = reader.ReadUInt16();
        LoopEnd = reader.ReadInt32();
        LoopInfo = new BinaryWaveLoopInfo[seekPointCount];

        for (uint i = 0; i < LoopCount; i++)
            LoopInfo[i] = BinaryWaveLoopInfo.Read(reader);

        int encodedSize = LocalSampleCount * 2;

        if (Codec == CodecType.Adpcm)
            encodedSize = DspAdpcmCodec.GetByteCount(LocalSampleCount);

        using (reader.CreateScope())
            ChannelData = LocalSampleCount > 0 ? reader.ReadByteArrayAt(audioOffset, encodedSize) : [];
    }

    internal void Write(BinaryWriter writer, bool isPrefetched)
    {
        writer.Write((ushort)Codec);

        writer.Write((ushort)ChannelPan);
        writer.Write(SampleRate);
        writer.Write(TotalSampleCount);
        writer.Write(LocalSampleCount);

        if (Codec == CodecType.Adpcm)
            writer.Write(Coefficients ?? new short[DspAdpcmCodec.CoefficientCount]);
        else if (Codec == CodecType.PcmInt16)
            writer.Pad(0x20);

        writeTotalSamples = null;
        if (isPrefetched)
            writer.Write(TotalSamplesOffset);
        else
            writeTotalSamples = writer.CreatePointer();
        writeLocalSamples = writer.CreatePointer();
        writer.Write(LoopCount);
        writer.Write(LoopSeekPointIndex);
        writer.Write(LoopEnd);
        foreach (var point in LoopInfo)
            point.Write(writer);
    }

    public short[] Decode()
    {
        if (ChannelData == null) return [];

        var newData = new short[LocalSampleCount];

        if (Codec == CodecType.Adpcm)
        {
            if (Coefficients == null) return [];
            DspAdpcmCodec.Decode(ChannelData, newData, Coefficients);
        }
        else if (Codec == CodecType.PcmInt16)
        {
            for (int i = 0; i < LocalSampleCount; i++)
            {
                newData[i] = IsBigEndian
                    ? BinaryPrimitives.ReadInt16BigEndian(ChannelData.AsSpan(i * 2, 2))
                    : BinaryPrimitives.ReadInt16LittleEndian(ChannelData.AsSpan(i * 2, 2));
            }
        }

        return newData;
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
            ChannelData = null;
            Coefficients = null;

            writeTotalSamples = null;
            writeLocalSamples = null;
        }

        _disposed = true;
    }
}