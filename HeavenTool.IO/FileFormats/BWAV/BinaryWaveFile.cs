using HeavenTool.IO.Audio.DspAdpcm;
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

    private BinaryWaveFile(BinaryWaveFile template, BinaryWaveChannel[] channels, AssetType assetType = AssetType.Normal)
    {
        IsBigEndian = template.IsBigEndian;
        Version = template.Version;
        AssetType = assetType;
        Hash = template.Hash;
        Channels = channels;
    }

    /// <summary>Copies channel configuration while retaining the immutable encoded audio for background reads.</summary>
    public BinaryWaveFile CreateSnapshot() => new(this, [.. Channels.Select(c => new BinaryWaveChannel(c))], AssetType);

    public BinaryWaveFile WithPcm(ReadOnlySpan<short> samples, int sampleRate, int channels)
    {
        if (channels <= 0 || channels > ushort.MaxValue || sampleRate <= 0 || Channels.Length == 0)
            throw new InvalidDataException("Replacement audio needs a valid channel count, sample rate and template.");
        if (samples.Length == 0 || samples.Length % channels != 0)
            throw new InvalidDataException("Replacement audio must contain complete sample frames.");
        int frames = samples.Length / channels;
        var result = new BinaryWaveChannel[channels];
        var mono = new short[frames];
        bool sameLayout = channels == Channels.Length;
        bool reversedStereo = sameLayout && channels == 2 && Channels[0].ChannelPan == PanType.Right && Channels[1].ChannelPan == PanType.Left;
        for (int c = 0; c < channels; c++)
        {
            int inputChannel = reversedStereo ? 1 - c : c;
            for (int i = 0; i < frames; i++) mono[i] = samples[i * channels + inputChannel];
            var template = Channels[c < Channels.Length ? c : 0];
            var pan = sameLayout ? template.ChannelPan : GetDefaultPan(channels, c);
            result[c] = new BinaryWaveChannel(template, mono, sampleRate, pan, IsBigEndian);
        }
        return new BinaryWaveFile(this, result);
    }

    internal BinaryWaveFile CreatePrefetch(int encodedBytesPerChannel)
    {
        if (encodedBytesPerChannel <= 0)
            throw new ArgumentOutOfRangeException(nameof(encodedBytesPerChannel));
        using var serialized = new BinaryWaveFile(ToBytes());
        return new BinaryWaveFile(serialized,
            [.. serialized.Channels.Select(c => new BinaryWaveChannel(c, encodedBytesPerChannel))], AssetType.Prefetch);
    }

    public BinaryWaveFile WithLoop(int? start, int? end)
    {
        ValidateCompleteAudio(decode: false);
        if (start.HasValue != end.HasValue || (start.HasValue
            && (start.Value < 0 || end!.Value <= start.Value || end.Value > Channels[0].LocalSampleCount)))
            throw new InvalidDataException("Loop positions must satisfy 0 <= start < end <= the audio's sample count.");
        return new BinaryWaveFile(this, [.. Channels.Select(c => new BinaryWaveChannel(c, start, end))]);
    }

    public BinaryWaveFile WithLoopDataFrom(BinaryWaveFile template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ValidateCompleteAudio(decode: false);
        if (template.Channels.Length == 0) throw new InvalidDataException("Loop source has no audio channels.");
        return new BinaryWaveFile(this, [.. Channels.Select((channel, index) =>
        {
            var source = index < template.Channels.Length && template.Channels[index].ChannelPan == channel.ChannelPan
                ? template.Channels[index] : template.Channels.FirstOrDefault(c => c.ChannelPan == channel.ChannelPan)
                    ?? template.Channels[Math.Min(index, template.Channels.Length - 1)];
            return new BinaryWaveChannel(channel, source);
        })]);
    }

    private static PanType GetDefaultPan(int channels, int index) => (channels, index) switch
    {
        (1, _) => PanType.Center,
        (_, 0) => PanType.Left,
        (_, 1) => PanType.Right,
        (4, 2) or (5, 3) => PanType.LeftSurround,
        (4, 3) or (5, 4) => PanType.RightSurround,
        (_, 2) => PanType.Center,
        (_, 3) => PanType.LowFrequencyEffects,
        (_, 4) => PanType.LeftSurround,
        (_, 5) => PanType.RightSurround,
        _ => PanType.Invalid
    };

    internal void ValidatePrefetch(BinaryWaveFile full)
    {
        const string mismatch = "The embedded prefetch does not match its replacement stream. Import the audio again.";
        full.ValidateCompleteAudio(decode: false);
        if (AssetType != AssetType.Prefetch || Channels.Length != full.Channels.Length)
            throw new InvalidDataException(mismatch);
        using var serialized = new BinaryWaveFile(full.ToBytes());
        var channels = new BinaryWaveChannel[Channels.Length];
        for (int i = 0; i < channels.Length; i++)
        {
            int bytes = Channels[i].ChannelData?.Length ?? 0;
            bytes = serialized.Channels[i].Codec == CodecType.Adpcm
                ? Math.Max(checked((bytes + DspAdpcmCodec.BytesPerFrame - 1) / DspAdpcmCodec.BytesPerFrame * DspAdpcmCodec.BytesPerFrame), DspAdpcmCodec.BytesPerFrame)
                : Math.Max(bytes, sizeof(short));
            channels[i] = new BinaryWaveChannel(serialized.Channels[i], bytes);
        }
        using var expected = new BinaryWaveFile(serialized, channels, AssetType.Prefetch);
        if (!ToBytes().AsSpan().SequenceEqual(expected.ToBytes()))
            throw new InvalidDataException(mismatch);
    }

    public void ValidateCompleteAudio(bool decode = true)
    {
        if (AssetType != AssetType.Normal || Channels.Length == 0)
            throw new InvalidDataException("Import a complete BWAV, rather than a prefetch file.");
        var first = Channels[0];
        foreach (var channel in Channels)
        {
            if (channel.SampleRate <= 0 || channel.LocalSampleCount <= 0
                || channel.LocalSampleCount != channel.TotalSampleCount
                || channel.LocalSampleCount != first.LocalSampleCount
                || channel.SampleRate != first.SampleRate
                || channel.LoopEnd > channel.LocalSampleCount
                || channel.LoopInfo.Any(p => p.LoopStart >= channel.LocalSampleCount))
                throw new InvalidDataException("Replacement BWAV has invalid or inconsistent channel data.");
            if (channel.Codec is not CodecType.Adpcm and not CodecType.PcmInt16
                || channel.ChannelData == null
                || channel.ChannelData.Length < (channel.Codec == CodecType.Adpcm
                    ? DspAdpcmCodec.GetByteCount(channel.LocalSampleCount) : checked(channel.LocalSampleCount * sizeof(short)))
                || (channel.Codec == CodecType.Adpcm && channel.Coefficients?.Length != DspAdpcmCodec.CoefficientCount)
                || (decode && channel.Decode().Length != channel.LocalSampleCount))
                throw new InvalidDataException("Replacement BWAV is missing channel samples or coefficients.");
        }
    }

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
