namespace HeavenTool.IO.Audio.Singing;

/// <summary>A sampled voice with pitch, source looping and a three-update release.</summary>
internal sealed class SingingVoice(VoiceSample sample, float volume, int sampleRate)
{
    private readonly int releaseFrames = (int)Math.Round(sampleRate * 0.1);
    private readonly bool looping = sample.LoopStart >= 0 && sample.LoopStart < sample.LoopEnd
        && sample.LoopEnd <= sample.Samples.Length;
    private double position;
    private double step;
    private int releaseFrame = -1;
    private List<(double Step, int Frames)>? deferredAdvance;

    public bool Finished => releaseFrame >= releaseFrames || position >= sample.Samples.Length;

    public void Release()
    {
        if (releaseFrame < 0) releaseFrame = 0;
    }

    public void SetPitch(byte pitch, float modulation)
    {
        if (releaseFrame < 0)
            step = (double)sample.SampleRate / sampleRate
                * MathF.Pow(10f, (pitch - sample.Pitch + modulation) * 0.025086f);
    }

    public float ReadFrame()
    {
        var index = (int)position;
        var next = index + 1;
        if (looping && next >= sample.LoopEnd) next = sample.LoopStart;
        else if (next >= sample.Samples.Length) next = index;
        var value = (float)(sample.Samples[index]
            + (sample.Samples[next] - sample.Samples[index]) * (position - index));
        var gain = releaseFrame < 0 ? 1 : 1 - (double)releaseFrame / releaseFrames;
        AdvanceSource(step);
        if (releaseFrame >= 0) releaseFrame++;
        return value * volume * (float)(gain * gain);
    }

    // Past voices can be discarded before calculating their source positions.
    public bool SkipFrames(int frames)
    {
        if (releaseFrame >= 0 && frames > releaseFrames - releaseFrame) return false;
        deferredAdvance ??= [];
        if (deferredAdvance.Count > 0 && deferredAdvance[^1].Step == step
            && deferredAdvance[^1].Frames <= int.MaxValue - frames)
            deferredAdvance[^1] = (step, deferredAdvance[^1].Frames + frames);
        else deferredAdvance.Add((step, frames));
        if (releaseFrame >= 0) releaseFrame += frames;
        return true;
    }

    public void ResolvePosition()
    {
        if (deferredAdvance == null) return;
        foreach (var (sourceStep, frames) in deferredAdvance)
            for (var frame = 0; frame < frames && position < sample.Samples.Length; frame++)
                AdvanceSource(sourceStep);
        deferredAdvance = null;
    }

    private void AdvanceSource(double sourceStep)
    {
        // Per-frame addition preserves uninterrupted playback's rounding.
        position += sourceStep;
        if (looping && position >= sample.LoopEnd)
            position = sample.LoopStart + (position - sample.LoopStart) % (sample.LoopEnd - sample.LoopStart);
    }
}
