using HeavenTool.IO.FileFormats.BWAV;

namespace HeavenTool.IO.Audio;

public sealed record BwavPreviewAudio(short[] Samples, int SampleRate, int Channels, int LoopStart, int LoopEnd)
{
    public static BwavPreviewAudio Load(BinaryWaveFile wave, string assetName, string directory, bool readPrefetchedFiles)
    {
        ArgumentNullException.ThrowIfNull(wave);
        ArgumentException.ThrowIfNullOrEmpty(assetName);
        if (wave.Channels.Length == 0) throw new InvalidDataException("No channels found.");

        var needsStream = wave.AssetType == AssetType.Prefetch || wave.Channels[0].LocalSampleCount == 0;
        var sourcePath = needsStream && readPrefetchedFiles ? FindStream(directory, assetName) : null;
        using var external = sourcePath == null ? null : Open(sourcePath);
        var source = external ?? wave;
        var channel = source.Channels[0];
        if (needsStream && external == null)
            return new BwavPreviewAudio(new short[checked((int)channel.TotalSampleCount * source.Channels.Length)],
                channel.SampleRate, source.Channels.Length, channel.LoopStart, channel.LoopEnd);

        var audio = Decode(source);
        if (source.Channels.Length != 1 || source.AssetType == AssetType.Prefetch
            || !assetName.StartsWith("BGM_DJ_", StringComparison.Ordinal)
            || !(assetName.EndsWith("_L", StringComparison.Ordinal) || assetName.EndsWith("_R", StringComparison.Ordinal)))
            return audio;

        var isLeft = assetName.EndsWith("_L", StringComparison.Ordinal);
        var partnerName = assetName[..^1] + (isLeft ? "R" : "L");
        var partnerPath = sourcePath == null ? FindStream(directory, partnerName)
            : Path.Combine(Path.GetDirectoryName(sourcePath)!, partnerName + ".bwav");
        if (partnerPath == null || !File.Exists(partnerPath)) return audio;

        using var partner = Open(partnerPath);
        if (partner.AssetType != AssetType.Normal || partner.Channels.Length != 1) return audio;
        var other = partner.Channels[0];
        if (channel.SampleRate != other.SampleRate || channel.LocalSampleCount != other.LocalSampleCount
            || channel.TotalSampleCount != other.TotalSampleCount || channel.LoopEnd != other.LoopEnd
            || channel.LoopSeekPointIndex != other.LoopSeekPointIndex
            || !channel.LoopInfo.Select(l => l.LoopStart).SequenceEqual(other.LoopInfo.Select(l => l.LoopStart)))
            return audio;

        var otherSamples = other.Decode();
        if (otherSamples.Length != audio.Samples.Length) return audio;
        var left = isLeft ? audio.Samples : otherSamples;
        var right = isLeft ? otherSamples : audio.Samples;
        var stereo = new short[checked(left.Length * 2)];
        for (var i = 0; i < left.Length; i++)
        {
            stereo[i * 2] = left[i];
            stereo[i * 2 + 1] = right[i];
        }
        return audio with { Samples = stereo, Channels = 2 };
    }

    private static string? FindStream(string directory, string assetName)
    {
        var path = Path.Combine(directory, "Stream", assetName + ".bwav");
        if (File.Exists(path)) return path;
        path = Path.Combine(directory, assetName + ".bwav");
        return File.Exists(path) ? path : null;
    }

    private static BinaryWaveFile Open(string path)
    {
        using var stream = File.OpenRead(path);
        return new BinaryWaveFile(stream);
    }

    private static BwavPreviewAudio Decode(BinaryWaveFile wave)
    {
        var channels = wave.Channels.Length;
        if (channels == 0) throw new InvalidDataException("No channels found.");
        var channel = wave.Channels[0];
        var frames = channel.LocalSampleCount;
        var decoded = wave.Channels.Select(c => c.Decode()).ToArray();

        if (channels == 2 && wave.Channels[0].ChannelPan == PanType.Right && wave.Channels[1].ChannelPan == PanType.Left)
            (decoded[0], decoded[1]) = (decoded[1], decoded[0]);

        var pcm = new short[checked(frames * channels)];
        for (var i = 0; i < frames; i++)
            for (var c = 0; c < channels; c++)
                pcm[i * channels + c] = decoded[c][i];

        return new BwavPreviewAudio(pcm, channel.SampleRate, channels, channel.LoopStart, channel.LoopEnd);
    }
}