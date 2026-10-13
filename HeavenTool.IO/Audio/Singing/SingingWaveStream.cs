using HeavenTool.IO.FileFormats.BARS.MINF.Sections;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace HeavenTool.IO.Audio.Singing;

/// <summary>Streams song PCM and voices without resetting their state at song loops.</summary>
public sealed class SingingWaveStream : WaveStream
{
    private readonly Lock sync = new();
    private readonly short[] song;
    private readonly SingingPlayback singing;
    private readonly MusicEventTimeline timeline;
    private readonly MusicEventTimeline onceTimeline;
    private readonly int loopStart;
    private readonly int loopEnd;
    private readonly List<(long Elapsed, int Song)> positionAnchors = [(0, 0)];
    private readonly List<(long Frame, bool Enabled)> loopChanges = [];
    private long elapsedFrames;
    private int songFrame;
    private bool looped;
    private bool loopEnabled;

    public override WaveFormat WaveFormat { get; }
    public override long Length => (long)song.Length * sizeof(short);
    public bool HasLoop => loopStart >= 0 && loopEnd > loopStart && loopEnd <= FrameCount;
    private int FrameCount => song.Length / WaveFormat.Channels;

    public bool LoopEnabled
    {
        get { lock (sync) return loopEnabled; }
        set
        {
            lock (sync)
            {
                bool enabled = value && HasLoop;
                if (loopChanges.Count == 0 || enabled != loopEnabled)
                {
                    if (loopChanges.Count > 0 && loopChanges[^1].Frame == elapsedFrames)
                        loopChanges[^1] = (elapsedFrames, enabled);
                    else
                        loopChanges.Add((elapsedFrames, enabled));
                }
                loopEnabled = enabled;
            }
        }
    }

    public long ElapsedFrames { get { lock (sync) return elapsedFrames; } }

    public long GetPlaybackPosition(long playedFrame)
    {
        lock (sync)
        {
            playedFrame = Math.Clamp(playedFrame, 0, elapsedFrames);
            int first = 0, end = positionAnchors.Count;

            while (first < end)
            {
                int middle = first + (end - first) / 2;
                if (positionAnchors[middle].Elapsed <= playedFrame) first = middle + 1;
                else end = middle;
            }

            var (elapsed, song) = positionAnchors[Math.Max(0, first - 1)];
            return Math.Min(FrameCount, song + playedFrame - elapsed) * WaveFormat.BlockAlign;
        }
    }

    public override long Position
    {
        get { lock (sync) return (long)songFrame * WaveFormat.BlockAlign; }
        set
        {
            lock (sync)
                SeekFrames(Math.Clamp(value / WaveFormat.BlockAlign, 0, FrameCount));
        }
    }

    public SingingWaveStream(short[] song, int sampleRate, int channels, int loopStart, int loopEnd,
        PerformanceSequence? sequence = null, int noteSampleRate = 0,
        IReadOnlyList<VoiceSampleBank>? singers = null, VoiceSampleBank? whistles = null,
        int lookAheadSamples = MusicEventTimeline.GameLookAheadSamples, uint finiteMusicEnd = 0)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        if (song.Length % channels != 0) throw new ArgumentException("PCM must contain complete frames.", nameof(song));
        WaveFormat = new WaveFormat(sampleRate, 16, channels);
        this.song = song;
        singing = new SingingPlayback(sequence, sampleRate, singers ?? [], whistles);
        this.loopStart = loopStart;
        this.loopEnd = loopEnd;
        int musicRate = noteSampleRate > 0 ? noteSampleRate : sampleRate;
        // Music positions follow the actual playback loop, including rate conversion.
        timeline = new MusicEventTimeline(musicRate,
            HasLoop ? checked((uint)((long)loopStart * musicRate / sampleRate)) : finiteMusicEnd,
            HasLoop ? checked((uint)((long)loopEnd * musicRate / sampleRate)) : finiteMusicEnd,
            lookAheadSamples);
        onceTimeline = new MusicEventTimeline(musicRate, finiteMusicEnd, finiteMusicEnd, lookAheadSamples);
        LoopEnabled = HasLoop;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Buffer range is invalid.");
        lock (sync)
        {
            int requestedFrames = count / WaveFormat.BlockAlign;
            var output = MemoryMarshal.Cast<byte, short>(
                buffer.AsSpan(offset, requestedFrames * WaveFormat.BlockAlign));
            int frames = 0;
            while (frames < requestedFrames && PrepareFrame())
            {
                UpdateSinging();
                float voice = singing.ReadFrame();
                for (int channel = 0; channel < WaveFormat.Channels; channel++)
                    output[frames * WaveFormat.Channels + channel] = (short)Math.Clamp(
                        Math.Round(song[songFrame * WaveFormat.Channels + channel] + (double)voice),
                        short.MinValue, short.MaxValue);
                songFrame++;
                elapsedFrames++;
                frames++;
            }
            return frames * WaveFormat.BlockAlign;
        }
    }

    /// <summary>Reconstructs playback state from the beginning, including previous loop passes.</summary>
    public void SeekFrames(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        lock (sync)
            Reconstruct(frame, [(0, loopEnabled)], loopEnabled);
    }

    /// <summary>Restores the heard position and prior loop changes with this preview's voice banks.</summary>
    public void RestorePlayback(SingingWaveStream previous, long playedFrame)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentOutOfRangeException.ThrowIfNegative(playedFrame);
        if (WaveFormat.SampleRate != previous.WaveFormat.SampleRate || FrameCount != previous.FrameCount
            || loopStart != previous.loopStart || loopEnd != previous.loopEnd)
            throw new ArgumentException("Playback timelines must match.", nameof(previous));

        (long Frame, bool Enabled)[] changes;
        bool enabled;
        lock (previous.sync)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(playedFrame, previous.elapsedFrames);
            changes = previous.loopChanges.Where(c => c.Frame <= playedFrame).ToArray();
            enabled = previous.loopEnabled;
        }
        lock (sync)
            Reconstruct(playedFrame, changes, enabled);
    }

    private void Reconstruct(long frame, (long Frame, bool Enabled)[] changes, bool enabled)
    {
        loopChanges.Clear();
        loopEnabled = changes[0].Enabled && HasLoop;
        loopChanges.Add((0, loopEnabled));
        int nextChange = 1;
        songFrame = 0;
        elapsedFrames = 0;
        looped = false;
        singing.Reset();
        positionAnchors.Clear();
        positionAnchors.Add((0, 0));
        while (elapsedFrames < frame)
        {
            while (nextChange < changes.Length && changes[nextChange].Frame <= elapsedFrames)
                LoopEnabled = changes[nextChange++].Enabled;
            if (!PrepareFrame()) break;
            UpdateSinging();
            // Between updates, only source positions and release ages change.
            long nextTick = ((elapsedFrames * 30 / WaveFormat.SampleRate + 1)
                * WaveFormat.SampleRate + 29) / 30;
            long boundary = Math.Min(frame, nextTick);
            if (nextChange < changes.Length) boundary = Math.Min(boundary, changes[nextChange].Frame);
            int songEnd = loopEnabled ? loopEnd : FrameCount;
            int skipped = (int)Math.Min(boundary - elapsedFrames, songEnd - songFrame);
            singing.SkipFrames(skipped);
            songFrame += skipped;
            elapsedFrames += skipped;
        }
        singing.ResolvePositions();
        // Queued audio is discarded on refresh, so the current setting applies at the heard position.
        LoopEnabled = enabled;
    }

    private bool PrepareFrame()
    {
        if (loopEnabled && songFrame >= loopEnd)
        {
            songFrame = loopStart;
            looped = true;
            positionAnchors.Add((elapsedFrames, songFrame));
        }
        return songFrame < FrameCount;
    }

    private void UpdateSinging() => singing.Update(elapsedFrames, songFrame,
        loopEnabled ? timeline : onceTimeline, looped);
}
