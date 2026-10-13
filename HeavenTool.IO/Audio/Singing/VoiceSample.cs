using HeavenTool.IO.FileFormats.BARS;

namespace HeavenTool.IO.Audio.Singing;

public sealed record VoiceSample(short[] Samples, int SampleRate, byte Pitch, int LoopStart, int LoopEnd);

public sealed class VoiceSampleBank
{
    private readonly Dictionary<(string VoiceType, byte Pitch), VoiceSample> samples = [];

    public VoiceSampleBank(BARSFileReader archive, string prefix)
    {
        foreach (var asset in archive.AudioAssets)
        {
            var name = asset.AudioMetadata?.AssetName;
            if (name == null || !name.StartsWith(prefix + "_", StringComparison.Ordinal)) continue;
            var separator = name.LastIndexOf('_');
            if (!byte.TryParse(name.AsSpan(separator + 1), out var pitch)) continue;
            var vowel = prefix == "Npc_Whistle" ? "Whistle" : name[(prefix.Length + 1)..separator];
            var wave = asset.BinaryWave;
            if (wave == null || wave.Channels.Length != 1)
                throw new InvalidDataException($"Voice sample {name} must have one channel.");
            var channel = wave.Channels[0];
            samples.TryAdd((vowel, pitch), new VoiceSample(channel.Decode(), channel.SampleRate, pitch,
                channel.LoopStart, channel.LoopEnd));
        }
    }

    public VoiceSample? Find(string vowel, byte pitch)
    {
        var recordedPitch = RecordedPitchFor(vowel, pitch);
        return recordedPitch is int selected && samples.TryGetValue((vowel, (byte)selected), out var sample)
            ? sample : null;
    }

    private static int? RecordedPitchFor(string vowel, byte pitch)
    {
        int minimum, maximum;
        switch (vowel)
        {
            case "A":
            case "I":
            case "U":
            case "E":
            case "O":
                minimum = 56; maximum = 72;
                break;
            case "ILow":
            case "OLow":
                minimum = 52; maximum = 64;
                break;
            case "Whistle":
                minimum = 64; maximum = 84;
                break;
            default:
                return null;
        }
        if (pitch < minimum - 12 || pitch > maximum + 14) return null;
        return Math.Clamp(minimum + Math.Max(0, pitch - minimum) / 2 * 2, minimum, maximum);
    }
}