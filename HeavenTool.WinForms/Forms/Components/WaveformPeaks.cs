using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace HeavenTool.Forms.Components;

internal sealed class WaveformPeaks
{
    internal const int MinimumSamplesPerPixel = 16;
    internal const int MaximumSamplesPerPixel = 4096;
    internal readonly record struct Peak(float Min, float Max);

    private readonly Peak[][] levels;

    private WaveformPeaks(Peak[][] levels) => this.levels = levels;

    private static WaveformPeaks? BuildLevels(Peak[] finest, CancellationToken cancellation)
    {
        var levels = new Peak[9][];
        levels[0] = finest;
        for (int level = 1; level < levels.Length; level++)
        {
            var previous = levels[level - 1];
            var next = levels[level] = new Peak[(previous.Length + 1) / 2];
            for (int i = 0; i < next.Length; i++)
            {
                if ((i & 4095) == 0 && cancellation.IsCancellationRequested) return null;
                var left = previous[i * 2];
                var right = previous[Math.Min(i * 2 + 1, previous.Length - 1)];
                next[i] = new Peak(Math.Min(left.Min, right.Min), Math.Max(left.Max, right.Max));
            }
        }
        foreach (var level in levels)
            for (int i = 0; i < level.Length; i++)
            {
                if ((i & 4095) == 0 && cancellation.IsCancellationRequested) return null;
                if (!float.IsFinite(level[i].Min)) level[i] = default;
            }
        return cancellation.IsCancellationRequested ? null : new WaveformPeaks(levels);
    }

    internal Peak[] GetLevel(int samplesPerPixel)
    {
        int level = 0;
        for (int size = MinimumSamplesPerPixel; size < samplesPerPixel; size *= 2) level++;
        return levels[level];
    }

    internal static WaveformPeaks? Build(WaveStream stream, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested) return null;
        var format = stream.WaveFormat;
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
        bool isPcm = format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16;
        if ((!isFloat && !isPcm) || format.Channels <= 0
            || format.BlockAlign != format.Channels * format.BitsPerSample / 8)
            throw new NotSupportedException("Waveform preview requires 16-bit PCM or 32-bit float audio.");

        var peaks = new List<Peak>();
        var buffer = new byte[checked(4096 * format.BlockAlign)];
        int buffered = 0, bucketFrames = 0;
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        void AddPeak()
        {
            peaks.Add(new Peak(min, max));
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            bucketFrames = 0;
        }

        lock (stream)
        {
            if (cancellation.IsCancellationRequested) return null;
            long position = stream.Position;
            try
            {
                stream.Position = 0;
                while (true)
                {
                    if (cancellation.IsCancellationRequested) return null;
                    int read = stream.Read(buffer, buffered, buffer.Length - buffered);
                    if (cancellation.IsCancellationRequested) return null;
                    if (read == 0) break;
                    buffered += read;
                    int complete = buffered / format.BlockAlign * format.BlockAlign;
                    var floats = isFloat ? MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, complete)) : default;
                    var shorts = isFloat ? default : MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, complete));
                    int frames = complete / format.BlockAlign;
                    for (int frame = 0; frame < frames;)
                    {
                        int count = Math.Min(MinimumSamplesPerPixel - bucketFrames, frames - frame);
                        int first = frame * format.Channels;
                        int end = (frame + count) * format.Channels;
                        if (isFloat)
                        {
                            for (int i = first; i < end; i++)
                                if (float.IsFinite(floats[i]))
                                {
                                    min = Math.Min(min, floats[i]);
                                    max = Math.Max(max, floats[i]);
                                }
                        }
                        else
                        {
                            int low = short.MaxValue, high = short.MinValue;
                            for (int i = first; i < end; i++)
                            {
                                low = Math.Min(low, shorts[i]);
                                high = Math.Max(high, shorts[i]);
                            }
                            min = Math.Min(min, low / 32768f);
                            max = Math.Max(max, high / 32768f);
                        }
                        frame += count;
                        bucketFrames += count;
                        if (bucketFrames == MinimumSamplesPerPixel) AddPeak();
                    }
                    buffered -= complete;
                    if (buffered > 0) Buffer.BlockCopy(buffer, complete, buffer, 0, buffered);
                }
                if (buffered != 0) throw new InvalidDataException("Audio ends with an incomplete sample frame.");
                if (bucketFrames > 0) AddPeak();
            }
            finally
            {
                if (!cancellation.IsCancellationRequested) stream.Position = position;
            }
        }
        return BuildLevels([.. peaks], cancellation);
    }
}
