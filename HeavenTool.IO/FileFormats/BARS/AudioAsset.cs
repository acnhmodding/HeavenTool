using HeavenTool.IO.FileFormats.BWAV;
using System.ComponentModel;

namespace HeavenTool.IO.FileFormats.BARS;

public class AudioAsset
{
    private const int DefaultPrefetchBytesPerChannel = 0x2000;

    /// <summary>
    /// CRC32 Hash for the Asset name
    /// </summary>
    public uint Hash { get; set; }

    /// <summary>
    /// Offset for the asset data, this is used to read the actual audio data from the file.
    /// </summary>
    public int AssetOffset { get; set; }
    public bool IsPrefetch { get; set; } = false;

    /// <summary>
    /// Gets or sets the metadata information associated with the audio content.
    /// </summary>
    public AudioMetadata? AudioMetadata { get; set; }

    /// <summary>
    /// Gets or sets the binary wave file associated with the current instance.
    /// </summary>
    public BinaryWaveFile? BinaryWave { get; set; }

    [Browsable(false)]
    public BinaryWaveFile? ExternalWave { get; private set; }

    internal bool IsStreaming => IsPrefetch || BinaryWave?.AssetType == AssetType.Prefetch
        || AudioMetadata?.Flags.HasFlag(AMTA.MetadataFlags.IsStreaming) == true;

    /// <summary>Retains a snapshot sharing encoded audio, so the caller can dispose the input.</summary>
    public void ReplaceWave(BinaryWaveFile wave) => ReplaceWave(wave, IsStreaming);

    internal void ReplaceWave(BinaryWaveFile wave, bool streaming)
    {
        ArgumentNullException.ThrowIfNull(wave);
        wave.ValidateCompleteAudio();
        var tracks = AudioMetadata?.GetReplacementTracks(BinaryWave, wave);
        ValidateMetadata(AudioMetadata, wave, tracks);
        int budget = BinaryWave?.AssetType == AssetType.Prefetch
            && BinaryWave.Channels[0].LocalSampleCount < BinaryWave.Channels[0].TotalSampleCount
            && BinaryWave.Channels[0].ChannelData is { Length: > 0 } prefix
            ? prefix.Length : DefaultPrefetchBytesPerChannel;
        var embedded = streaming ? wave.CreatePrefetch(budget) : wave.CreateSnapshot();
        var external = streaming ? wave.CreateSnapshot() : null;
        BinaryWave = embedded;
        ExternalWave = external;
        IsPrefetch = streaming;
        if (tracks != null)
            AudioMetadata!.UseWave(tracks, wave, streaming);
    }

    public void ReplaceMetadata(byte[] data)
    {
        var replacement = new AudioMetadata(data);
        if (AudioMetadata != null && replacement.AssetName != AudioMetadata.AssetName)
            throw new InvalidDataException($"Metadata must belong to entry '{AudioMetadata.AssetName}'.");
        if (BinaryWave is { } wave)
        {
            ValidateMetadata(replacement, wave);
            if (wave.AssetType == AssetType.Prefetch)
                replacement.UseWave(replacement.Tracks, wave, streaming: true);
        }

        AudioMetadata = replacement;
        Hash = replacement.AssetName.ToCRC32();
    }

    internal void ValidateForSave()
    {
        var wave = BinaryWave ?? throw new InvalidDataException("BWAV is missing.");
        if (wave.Channels.Length == 0) throw new InvalidDataException("BWAV has no channels.");
        ValidateMetadata(AudioMetadata, wave);
        if (ExternalWave != null) wave.ValidatePrefetch(ExternalWave);
        else if (wave.AssetType == AssetType.Normal && wave.Channels[0].LocalSampleCount > 0)
            wave.ValidateCompleteAudio(decode: false);
    }

    private static void ValidateMetadata(AudioMetadata? metadata, BinaryWaveFile wave, AMTA.ResTrack[]? tracks = null)
    {
        if (metadata == null) return;
        if ((tracks ?? metadata.Tracks).Any(t => t.Channels.Any(c => c.ChannelIndex >= wave.Channels.Length
            || !Enum.IsDefined(c.OutputChannel))))
            throw new InvalidDataException("Metadata contains an invalid channel route.");
        if (metadata.MusicInfo is { } music && (music.SampleRate == 0 || wave.Channels.Any(c =>
            c.SampleRate <= 0 || (ulong)c.TotalSampleCount * music.SampleRate < (ulong)music.LoopEnd * (uint)c.SampleRate)))
            throw new InvalidDataException("Replacement audio must cover the MINF loop/end time.");
    }

    public override string ToString() => AudioMetadata?.AssetName ?? $"[Metadata not found | Hash: {Hash:X}]";
}
