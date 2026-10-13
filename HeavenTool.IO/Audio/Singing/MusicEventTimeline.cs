namespace HeavenTool.IO.Audio.Singing;

public sealed class MusicEventTimeline
{
    // Look-ahead is a fixed sample count rather than a rate-scaled duration.
    public const int GameLookAheadSamples = 0xc80;
    public int SampleRate { get; }
    public uint LoopStart { get; }
    public uint LoopEnd { get; }
    public int LookAheadSamples { get; }
    public long Margin { get; }
    public long LoopLength => LoopEnd > LoopStart ? (long)LoopEnd - LoopStart : 0;
    public readonly record struct Position(long Sample, bool Looped);
    public readonly record struct Selection(int Current, int Next, bool Active, int Countdown);

    public MusicEventTimeline(int sampleRate, uint loopStart = 0, uint loopEnd = 0, int lookAheadSamples = GameLookAheadSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegative(lookAheadSamples);
        SampleRate = sampleRate;
        LoopStart = loopStart;
        LoopEnd = loopEnd;
        LookAheadSamples = lookAheadSamples;
        Margin = (uint)(sampleRate * 0.016666668f);
    }

    public Position Query(long playedSample)
    {
        var sample = checked(playedSample + LookAheadSamples);

        if (LoopLength > 0 && sample >= LoopEnd)
            return new Position(LoopStart + (sample - LoopEnd) % LoopLength, true);

        // The music writer invalidates the query at a finite non-looping end.
        if (LoopEnd > 0 && LoopEnd == LoopStart && sample >= LoopEnd)
            return new Position(-1, false);

        return new Position(sample, false);
    }

    // Select the next event early and extend current-event activity by the margin.
    public Selection Select(Position position, int count, int loopIndex,
        Func<int, long> start, Func<int, long> duration)
    {
        if (count == 0 || position.Sample < 0) return new Selection(-1, -1, false, -1);

        var current = -1;
        if (start(0) <= position.Sample)
        {
            int first = 0, end = count;
            while (true)
            {
                var middle = first + (end - first) / 2;
                var sample = start(middle);
                // The game stops on an exact match, including duplicate starts.
                if (sample == position.Sample) { current = middle; break; }
                if (sample < position.Sample)
                {
                    if (first == middle) { current = first; break; }
                    first = middle;
                }
                else end = middle;
            }
        }

        var hasLoop = LoopLength > 0 && loopIndex >= 0 && loopIndex < count;

        if (position.Looped && hasLoop && current < loopIndex)
            current = count - 1;

        int Next(int index) => index + 1 < count ? index + 1 : hasLoop ? loopIndex : -1;
        long FutureStart(int index)
        {
            var sample = start(index);
            return sample < position.Sample && hasLoop ? sample + LoopLength : sample;
        }

        var next = Next(current);
        var upcoming = next >= 0 && position.Sample <= FutureStart(next)
            && FutureStart(next) - Margin <= position.Sample;
        var active = upcoming;

        if (current >= 0)
        {
            var currentStart = start(current);
            if (currentStart > position.Sample && hasLoop) currentStart -= LoopLength;
            active |= currentStart <= position.Sample
                && position.Sample < currentStart + duration(current) + Margin;
        }

        if (upcoming)
        {
            current = next;
            next = Next(next);
        }

        var countdown = next < 0 ? -1 : (int)MathF.Ceiling(
            (float)Math.Max(0, FutureStart(next) - Margin - position.Sample) / SampleRate * 30f);

        return new Selection(current, next, active, countdown);
    }
}