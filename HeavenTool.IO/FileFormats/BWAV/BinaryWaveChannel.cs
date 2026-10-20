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
    [ReadOnly(true)]
    public CodecType Codec { get; set; } = CodecType.Adpcm;

    [Category("Audio Configuration")]
    [ReadOnly(true)]
    public PanType ChannelPan { get; set; } = PanType.Center;

    [Category("Audio Configuration")]
    [ReadOnly(true)]
    public int SampleRate { get; set; } = 48000;

    /// <summary>
    /// Sample count in total
    /// </summary>
    [Category("Audio Configuration")]
    [ReadOnly(true)]
    public uint TotalSampleCount { get; set; }

    /// <summary>
    /// Sample count in this file, which is usually the same as <see href="TotalSampleCount"/> unless the file is prefetched.
    /// </summary>
    [Category("Audio Configuration")]
    [ReadOnly(true)]
    public int LocalSampleCount { get; set; }

    /// <summary>
    /// Coefficients for ADPCM decoding, this is only used when <see href="Codec"/> is <see cref="CodecType.Adpcm"/>
    /// </summary>
    [Category("Audio Configuration")]
    [ReadOnly(true)]
    [TypeConverter(typeof(ReadOnlyArrayConverter))]
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

    [ReadOnly(true)]
    [TypeConverter(typeof(ReadOnlyArrayConverter))]
    public BinaryWaveLoopInfo[] LoopInfo { get; private set; } = [];
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

    internal BinaryWaveChannel(BinaryWaveChannel template, ReadOnlySpan<short> samples, int sampleRate, PanType pan, bool bigEndian)
    {
        Codec = template.Codec;
        SampleRate = sampleRate;
        ChannelPan = pan;
        IsBigEndian = bigEndian;
        LocalSampleCount = samples.Length;
        TotalSampleCount = checked((uint)samples.Length);

        if (Codec == CodecType.Adpcm)
        {
            ChannelData = DspAdpcmCodec.Encode(samples, out var coefficients);
            Coefficients = coefficients;
        }
        else if (Codec == CodecType.PcmInt16)
        {
            ChannelData = new byte[checked(samples.Length * sizeof(short))];
            for (int i = 0; i < samples.Length; i++)
                if (bigEndian) BinaryPrimitives.WriteInt16BigEndian(ChannelData.AsSpan(i * 2), samples[i]);
                else BinaryPrimitives.WriteInt16LittleEndian(ChannelData.AsSpan(i * 2), samples[i]);
        }
        else throw new NotSupportedException($"Unsupported replacement codec: {Codec}.");

        CopyLoopData(template);
    }

    internal BinaryWaveChannel(BinaryWaveChannel source)
    {
        Codec = source.Codec;
        ChannelPan = source.ChannelPan;
        SampleRate = source.SampleRate;
        TotalSampleCount = source.TotalSampleCount;
        LocalSampleCount = source.LocalSampleCount;
        Coefficients = source.Coefficients?.ToArray();
        LoopSeekPointIndex = source.LoopSeekPointIndex;
        LoopEnd = source.LoopEnd;
        LoopInfo = [.. source.LoopInfo.Select(p => p with
        {
            AdpcmContext = p.AdpcmContext with { History = [.. p.AdpcmContext.History] }
        })];
        TotalSamplesOffset = source.TotalSamplesOffset;
        IsBigEndian = source.IsBigEndian;
        ChannelData = source.ChannelData;
    }

    internal BinaryWaveChannel(BinaryWaveChannel full, int encodedBytes) : this(full)
    {
        int samples = full.Codec == CodecType.Adpcm
            ? checked(encodedBytes / DspAdpcmCodec.BytesPerFrame * DspAdpcmCodec.SamplesPerFrame)
            : encodedBytes / sizeof(short);
        if (samples == 0) throw new InvalidDataException("The prefetch budget must hold at least one encoded frame.");
        LocalSampleCount = Math.Min(full.LocalSampleCount, samples);
        int bytes = Codec == CodecType.Adpcm ? DspAdpcmCodec.GetByteCount(LocalSampleCount) : checked(LocalSampleCount * sizeof(short));
        ChannelData = full.ChannelData![..bytes];
    }

    internal BinaryWaveChannel(BinaryWaveChannel source, BinaryWaveChannel loopTemplate) : this(source)
        => CopyLoopData(loopTemplate);

    private short[] DecodeHistory(int sample)
    {
        if (Codec != CodecType.Adpcm || sample == 0) return [];
        var decoded = new short[sample];
        DspAdpcmCodec.Decode(ChannelData, decoded, Coefficients);
        return decoded;
    }

    private AdpcmContext CreateContext(int sample, short[] decoded) => Codec == CodecType.Adpcm
        ? new AdpcmContext(ChannelData![sample / DspAdpcmCodec.SamplesPerFrame * DspAdpcmCodec.BytesPerFrame],
            [sample > 0 ? decoded[sample - 1] : (short)0, sample > 1 ? decoded[sample - 2] : (short)0])
        : new AdpcmContext(0, [0, 0]);

    private void CopyLoopData(BinaryWaveChannel template)
    {
        if (template.SampleRate <= 0) throw new InvalidDataException("Original audio has an invalid sample rate.");
        long ConvertPosition(long position) => checked((long)Math.Round(position * (double)SampleRate / template.SampleRate, MidpointRounding.AwayFromZero));
        int sampleCount = LocalSampleCount;
        var seekPoints = template.LoopInfo.Select((point, index) =>
                (Sample: ConvertPosition(point.LoopStart), point.Padding, Index: index))
            .Where(point => point.Sample < sampleCount).ToArray();
        int loopIndex = Array.FindIndex(seekPoints, point => point.Index == template.LoopSeekPointIndex);
        LoopSeekPointIndex = checked((ushort)Math.Max(loopIndex, 0));
        LoopEnd = template.LoopEnd < 0 ? template.LoopEnd : (int)Math.Min(ConvertPosition(template.LoopEnd), sampleCount);
        if (LoopEnd > 0 && (loopIndex < 0 || seekPoints[loopIndex].Sample >= LoopEnd)) LoopEnd = -1;
        if (seekPoints.Length == 0 && template.LoopInfo.Length > 0) seekPoints = [(0, 0, -1)];
        var decoded = DecodeHistory(seekPoints.Length == 0 ? 0 : (int)seekPoints.Max(p => p.Sample));
        LoopInfo = [.. seekPoints.Select(point => new BinaryWaveLoopInfo((uint)point.Sample,
            CreateContext((int)point.Sample, decoded), point.Padding))];
    }

    internal BinaryWaveChannel(BinaryWaveChannel source, int? start, int? end) : this(source)
    {
        LoopEnd = end ?? -1;
        if (start is not int sample) return;
        var points = LoopInfo.ToList();
        if (points.Count == 0 && sample > 0) points.Add(new(0, CreateContext(0, [])));
        int index = points.FindIndex(p => p.LoopStart == sample);
        var point = new BinaryWaveLoopInfo((uint)sample, CreateContext(sample, DecodeHistory(sample)),
            index >= 0 ? points[index].Padding : (ushort)0);
        if (index >= 0) points[index] = point;
        else
        {
            if (points.Count == ushort.MaxValue) throw new InvalidDataException("BWAV has too many seek points.");
            index = points.Count;
            points.Add(point);
        }
        LoopInfo = [.. points];
        LoopSeekPointIndex = (ushort)index;
    }

    public BinaryWaveChannel(BinaryReader reader)
    {
        IsBigEndian = reader.BigEndian;
        var codecType = reader.ReadUInt16();
        if (codecType > 1)
            throw new Exception($"Invalid Codec Type not supported ({codecType})!");

        Codec = (CodecType)codecType;

        var channelPan = reader.ReadUInt16();

        if (channelPan > 6)
            throw new Exception($"Invalid channel pan ({channelPan})!");

        ChannelPan = (PanType)channelPan;
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

        var encodedSize = LocalSampleCount * 2;

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

    public short[] Decode(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ChannelData == null) return [];

        var newData = new short[LocalSampleCount];

        if (Codec == CodecType.Adpcm)
        {
            if (Coefficients == null) return [];
            DspAdpcmCodec.Decode(ChannelData, newData, Coefficients, cancellationToken: cancellationToken);
        }
        else if (Codec == CodecType.PcmInt16)
        {
            for (var i = 0; i < LocalSampleCount; i++)
            {
                if ((i & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
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
