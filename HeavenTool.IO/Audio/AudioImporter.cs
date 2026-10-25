using HeavenTool.IO.FileFormats.BWAV;
using NAudio.Wave;

namespace HeavenTool.IO.Audio;

public enum ImportLoopChoice { KeepExisting, UseImported, Disable }

public static class AudioImporter
{
    public static BinaryWaveFile Read(string path, BinaryWaveFile template, ImportLoopChoice loops)
    {
        using var input = File.OpenRead(path);
        bool bwav = Path.GetExtension(path).Equals(".bwav", StringComparison.OrdinalIgnoreCase);
        using var imported = bwav ? new BinaryWaveFile(input) : FromWave(input, template);
        return loops switch
        {
            ImportLoopChoice.KeepExisting when bwav => imported.WithLoopDataFrom(template),
            ImportLoopChoice.Disable => imported.WithLoop(null, null),
            ImportLoopChoice.KeepExisting or ImportLoopChoice.UseImported => imported.CreateSnapshot(),
            _ => throw new ArgumentOutOfRangeException(nameof(loops))
        };
    }

    public static BinaryWaveFile FromWave(Stream stream, BinaryWaveFile template)
    {
        using var reader = new WaveFileReader(stream);
        var source = reader.ToSampleProvider();
        var samples = new List<short>();
        var buffer = new float[checked(4096 * source.WaveFormat.Channels)];
        int count;
        while ((count = source.Read(buffer.AsSpan())) > 0)
            for (int i = 0; i < count; i++)
            {
                if (!float.IsFinite(buffer[i])) throw new InvalidDataException("WAV contains non-finite samples.");
                samples.Add((short)(Math.Clamp(buffer[i], -1f, 32767f / 32768f) * 32768f));
            }
        return template.WithPcm(samples.ToArray(), source.WaveFormat.SampleRate, source.WaveFormat.Channels);
    }
}
