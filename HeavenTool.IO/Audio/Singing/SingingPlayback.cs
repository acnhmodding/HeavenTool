using HeavenTool.IO.FileFormats.BARS.MINF.Sections;
using HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;

namespace HeavenTool.IO.Audio.Singing;

internal sealed class SingingPlayback
{
    private readonly PerformanceSequence? sequence;
    private readonly int sampleRate;
    private readonly IReadOnlyList<VoiceSampleBank> singers;
    private readonly VoiceSampleBank? whistles;
    private readonly List<SingingVoice> voices = [];
    private NpcVoiceState trigger = new();
    private VoiceModulationState modulation = new();
    private VocalTrack.Event? playingNote;
    private long lastTick = -1;

    public SingingPlayback(PerformanceSequence? sequence, int sampleRate,
        IReadOnlyList<VoiceSampleBank> singers, VoiceSampleBank? whistles)
    {
        this.sequence = sequence;
        this.sampleRate = sampleRate;
        this.singers = singers;
        this.whistles = whistles;
        if (sequence?.Vocals?.Events is { } notes)
            for (var i = 1; i < notes.Count; i++)
                if (notes[i].SamplePosition < notes[i - 1].SamplePosition)
                    throw new InvalidDataException("Vocal events must be sorted by sample position.");
    }

    public void Reset()
    {
        trigger = new NpcVoiceState();
        modulation = new VoiceModulationState();
        playingNote = null;
        lastTick = -1;
        voices.Clear();
    }

    public void Update(long elapsedFrame, int songFrame, MusicEventTimeline timeline, bool looped)
    {
        var tick = elapsedFrame * 30 / sampleRate;
        if (tick == lastTick || sequence?.Vocals is not { } notes) return;
        lastTick = tick;
        var position = timeline.Query((long)songFrame * timeline.SampleRate / sampleRate);
        if (looped) position = position with { Looped = true };
        var selected = timeline.Select(position, notes.Events.Count, notes.LoopIndex,
            i => notes.Events[i].SamplePosition, i => notes.Events[i].Duration);
        var current = selected.Active ? notes.Events[selected.Current] : null;
        var action = trigger.Update(true, current == null ? 0 : (int)current.VoiceType,
            current == null ? -1 : current.Pitch, selected.Countdown);
        if (action != NpcVoiceState.Action.Continue)
        {
            foreach (var voice in voices) voice.Release();
            playingNote = action == NpcVoiceState.Action.Trigger ? current : null;
            if (playingNote != null)
            {
                if (playingNote.VoiceType == VocalTrack.VoiceType.Whistle)
                    StartVoice(whistles?.Find("Whistle", playingNote.Pitch));
                else
                    foreach (var singer in singers)
                        StartVoice(singer.Find(playingNote.VoiceType.ToString(), playingNote.Pitch));
            }
        }
        if (playingNote == null) return;
        var offset = modulation.Update(elapsedFrame, sampleRate,
            sequence.PitchBendFor(playingNote.VoiceType), sequence.VibratoFor(playingNote.VoiceType), timeline, position);
        foreach (var voice in voices) voice.SetPitch(playingNote.Pitch, offset);
    }

    public float ReadFrame()
    {
        float mixed = 0;
        for (var i = voices.Count - 1; i >= 0; i--)
        {
            if (voices[i].Finished) voices.RemoveAt(i);
            else mixed += voices[i].ReadFrame();
        }
        return mixed;
    }

    public void SkipFrames(int frames)
    {
        for (var i = voices.Count - 1; i >= 0; i--)
            if (!voices[i].SkipFrames(frames)) voices.RemoveAt(i);
    }

    public void ResolvePositions()
    {
        foreach (var voice in voices) voice.ResolvePosition();
        voices.RemoveAll(voice => voice.Finished);
    }

    private void StartVoice(VoiceSample? sample)
    {
        if (sample is { Samples.Length: > 0 })
            voices.Add(new SingingVoice(sample, playingNote!.Volume, sampleRate));
    }
}