using HeavenTool.IO.FileFormats.BARS.AMTA;
using HeavenTool.IO.FileFormats.BWAV;

namespace HeavenTool.IO.Audio;

internal static class AudioChannelRouting
{
    internal static ResStreamChannel CreateRoute(int index, PanType pan) => new()
    {
        ChannelIndex = checked((byte)index),
        OutputChannel = pan switch
        {
            PanType.Left => AttenuationChannel.Left,
            PanType.Right => AttenuationChannel.Right,
            PanType.LeftSurround => AttenuationChannel.LeftSurround,
            PanType.RightSurround => AttenuationChannel.RightSurround,
            PanType.LowFrequencyEffects => AttenuationChannel.LowFrequencyEffect,
            _ => AttenuationChannel.Center
        }
    };

    internal static BwavPreviewAudio Decode(BinaryWaveFile wave, ResTrack[] tracks, bool padToTotalLength, CancellationToken cancellationToken = default)
    {
        var routes = tracks.SelectMany(t => t.Channels).ToArray();
        if (routes.Any(r => r.ChannelIndex >= wave.Channels.Length || !Enum.IsDefined(r.OutputChannel)))
            throw new InvalidDataException("AMTA contains an invalid channel route.");
        var first = wave.Channels[0];
        if (wave.Channels.Any(c => c.SampleRate != first.SampleRate || c.LocalSampleCount != first.LocalSampleCount))
            throw new InvalidDataException("Routed BWAV channels must have matching sample rates and lengths.");

        bool mono = routes.Length > 0 && routes.All(r => r.OutputChannel == AttenuationChannel.Center);
        int channels = mono ? 1 : routes.Any(r => r.OutputChannel is not AttenuationChannel.Left and not AttenuationChannel.Right) ? 6 : 2;
        int frames = padToTotalLength ? checked((int)first.TotalSampleCount) : first.LocalSampleCount;
        var pcm = new short[checked(frames * channels)];
        if (first.LocalSampleCount > 0)
        {
            var sources = routes.Select(r => r.ChannelIndex).Distinct().ToDictionary(i => i, i => wave.Channels[i].Decode(cancellationToken));
            if (sources.Values.Any(s => s.Length != first.LocalSampleCount))
                throw new InvalidDataException("Routed BWAV is missing channel samples.");
            var mapped = routes.Select(r => (Source: sources[r.ChannelIndex], Output: mono ? 0 : r.OutputChannel switch
            {
                AttenuationChannel.Left => 0,
                AttenuationChannel.Right => 1,
                AttenuationChannel.Center => 2,
                AttenuationChannel.LowFrequencyEffect => 3,
                AttenuationChannel.LeftSurround => 4,
                AttenuationChannel.RightSurround => 5,
                _ => throw new InvalidDataException("Invalid output channel.")
            })).ToArray();
            Span<int> mixed = stackalloc int[channels];
            for (int frame = 0; frame < first.LocalSampleCount; frame++)
            {
                if ((frame & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
                mixed.Clear();
                foreach (var route in mapped) mixed[route.Output] += route.Source[frame];
                for (int channel = 0; channel < channels; channel++)
                    pcm[frame * channels + channel] = (short)Math.Clamp(mixed[channel], short.MinValue, short.MaxValue);
            }
        }
        return new BwavPreviewAudio(pcm, first.SampleRate, channels, first.LoopStart, first.LoopEnd);
    }
}
