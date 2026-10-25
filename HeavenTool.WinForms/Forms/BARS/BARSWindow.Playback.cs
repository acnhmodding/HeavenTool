using HeavenTool.IO.Audio;
using HeavenTool.IO.Audio.Singing;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BARS.MINF;
using HeavenTool.IO.FileFormats.BWAV;
using HeavenTool.Properties;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow
{
    private ConcurrentDictionary<string, VoiceSampleBank> singingCache = new(StringComparer.OrdinalIgnoreCase);
    private string? currentAssetName;
    private AudioAsset? currentAudioAsset;
    private BinaryWaveFile? previewSourceWave;
    private AudioMetadata? previewSourceMetadata;
    private readonly Timer playbackTimer = new();
    private long playbackOriginFrames;
    private SingingWaveStream? waveformStream;
    private SingingWaveStream? currentWave;
    private readonly WaveOut audioPlayer = new();

    private BwavPreviewAudio GetAudioData(string assetName, AudioAsset audioAsset, bool includeSinging = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetName);
        ArgumentNullException.ThrowIfNull(audioAsset);

        var bwav = audioAsset.BinaryWave ?? throw new Exception("BWAV not found!");

        var audio = BwavPreviewAudio.Load(bwav, assetName, lastLoadedPath,
            readPrefetchedFilesToolStripMenuItem.Checked, audioAsset.AudioMetadata, FindReplacementStream);

        // Keep the song's frame count so isolated voices retain their timing.
        if (!baseToolStripMenuItem.Checked)
            Array.Clear(audio.Samples);

        if (includeSinging && audioAsset.AudioMetadata?.MusicInfo?.PerformanceSequence?.Vocals != null)
        {
            using var preview = CreatePreviewStream(audio, audioAsset);
            preview.LoopEnabled = false;
            var rendered = new byte[audio.Samples.Length * sizeof(short)];
            preview.ReadExactly(rendered);
            Buffer.BlockCopy(rendered, 0, audio.Samples, 0, rendered.Length);
        }

        return audio;
    }

    private void RefreshPreview(bool preservePlayback = true)
    {
        var asset = pendingPreview?.Asset ?? currentAudioAsset;
        var name = pendingPreview?.Name ?? currentAssetName;
        if (!importing && asset != null && name != null)
            QueuePreview(name, asset, preservePlayback);
    }

    private BinaryWaveFile? FindReplacementStream(string assetName) =>
        barsFile?.AudioAssets.FirstOrDefault(a => a.AudioMetadata?.AssetName == assetName)?.ExternalWave
        ?? (assetName == StandaloneAssetName ? standaloneAsset?.ExternalWave : null);

    private void SelectPreview(string assetName, AudioAsset asset)
    {
        if (pendingPreview?.Matches(asset) == true) return;
        if (ReferenceEquals(currentAudioAsset, asset)
            && ReferenceEquals(previewSourceWave, asset.BinaryWave)
            && ReferenceEquals(previewSourceMetadata, asset.AudioMetadata)) return;
        QueuePreview(assetName, asset);
    }

    private void ApplyPreview(string assetName, AudioAsset audioAsset, BwavPreviewAudio audio,
        SingingWaveStream playback, SingingWaveStream waveform, bool preservePlayback)
    {
        var metadata = audioAsset.AudioMetadata;
        var resume = preservePlayback && audioPlayer.PlaybackState == PlaybackState.Playing;
        var previous = preservePlayback ? currentWave : null;
        var elapsed = previous != null ? GetPlayedFrames(previous) : 0;
        var looping = !preservePlayback || loopToolStripMenuItem.Checked;
        playback.LoopEnabled = looping;
        audioPlayer.Stop();
        playbackTimer.Stop();
        if (previous != null) playback.RestorePlayback(previous, elapsed);
        ClearPlayback();
        currentAudioAsset = audioAsset;
        previewSourceWave = audioAsset.BinaryWave;
        previewSourceMetadata = metadata;
        currentAssetName = assetName;
        currentWave = playback;
        waveformStream = waveform;
        loopToolStripMenuItem.Enabled = playback.HasLoop;
        loopToolStripMenuItem.Checked = playback.LoopEnabled;
        playbackOriginFrames = playback.ElapsedFrames;
        audioPlayer.Init(currentWave);

        playButton.Enabled = true;
        waveViewer.PlaybackStream = playback;
        waveViewer.WaveStream = waveformStream;

        if (playback.HasLoop)
            waveViewer.SetLoop(audio.LoopStart, audio.LoopEnd);

        waveViewer.SetMarkers(metadata?.MarkerList?.Markers ?? []);

        if (resume) PlayAudio();
        UpdateTimeLabel();
    }

    private string[] GetVoiceFiles() => Singers.Where(s => s.Checked).Select(s => s.FileName)
        .Concat(whistlingToolStripMenuItem.Checked ? new[] { "Npc_Whistle.bars" } : Array.Empty<string>()).ToArray();

    private SingingWaveStream CreatePreviewStream(BwavPreviewAudio audio, AudioAsset asset)
    {
        var music = asset.AudioMetadata?.MusicInfo;
        var (singers, whistles) = LoadVoiceBanks(music, GetVoiceFiles(), lastLoadedPath, singingCache);
        return CreatePreviewStream(audio, music, singers, whistles);
    }

    private static SingingWaveStream CreatePreviewStream(BwavPreviewAudio audio, MusicInfo? music,
        List<VoiceSampleBank> singers, VoiceSampleBank? whistles)
        => new(audio.Samples, audio.SampleRate, audio.Channels,
            audio.LoopStart, audio.LoopEnd, music?.PerformanceSequence,
            checked((int)(music?.SampleRate ?? (uint)audio.SampleRate)), singers, whistles,
            finiteMusicEnd: music?.LoopStart == music?.LoopEnd ? music?.LoopEnd ?? 0 : 0);

    private static (List<VoiceSampleBank> Singers, VoiceSampleBank? Whistles) LoadVoiceBanks(
        MusicInfo? music, string[] files, string directory, ConcurrentDictionary<string, VoiceSampleBank> cache,
        System.Threading.CancellationToken cancellation = default)
    {
        var singers = new List<VoiceSampleBank>();
        VoiceSampleBank? whistles = null;
        if (music?.PerformanceSequence?.Vocals != null)
            foreach (var file in files)
            {
                cancellation.ThrowIfCancellationRequested();
                string path = Path.GetFullPath(Path.Combine(directory, file));
                if (!File.Exists(path)) continue;
                if (!cache.TryGetValue(path, out var bank))
                {
                    using var input = File.OpenRead(path);
                    using var bars = new BARSFileReader(input);
                    bank = new VoiceSampleBank(bars, Path.GetFileNameWithoutExtension(file), cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    bank = cache.GetOrAdd(path, bank);
                }
                if (file == "Npc_Whistle.bars") whistles = bank;
                else singers.Add(bank);
            }
        return (singers, whistles);
    }

    private void ResetPreview()
    {
        CancelPreview();
        ClearPlayback();
    }

    private void ClearPlayback()
    {
        audioPlayer.Stop();
        playbackTimer.Stop();
        waveViewer.WaveStream = null;
        waveViewer.PlaybackStream = null;
        waveViewer.PlaybackPosition = null;
        waveViewer.RemoveLoop();
        waveViewer.ClearMarkers();
        currentWave?.Dispose();
        waveformStream?.Dispose();
        currentWave = null;
        waveformStream = null;
        currentAudioAsset = null;
        previewSourceWave = null;
        previewSourceMetadata = null;
        currentAssetName = null;
        playbackOriginFrames = 0;
        playButton.Enabled = false;
        playButton.BackgroundImage = Resources.play;
        loopToolStripMenuItem.Enabled = false;
        loopToolStripMenuItem.Checked = false;
        UpdateTimeLabel();
    }

    private void PlayStopButton_Click(object sender, EventArgs e)
    {
        if (currentWave == null) return;
        if (audioPlayer.PlaybackState == PlaybackState.Playing)
        {
            audioPlayer.Pause();
            UpdateTimeLabel();
            playbackTimer.Stop();
            playButton.BackgroundImage = Resources.play;
            return;
        }

        if (audioPlayer.PlaybackState == PlaybackState.Stopped && currentWave.Position >= currentWave.Length)
        {
            currentWave.Position = 0;
            playbackOriginFrames = 0;
            audioPlayer.Init(currentWave);
        }
        PlayAudio();
    }

    private void PlayAudio()
    {
        audioPlayer.Play();
        playbackTimer.Start();
        playButton.BackgroundImage = Resources.pause;
    }

    private void AudioPlayer_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (resourcesDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => AudioPlayer_PlaybackStopped(sender, e)); }
            catch (InvalidOperationException) when (resourcesDisposed || !IsHandleCreated) { }
            return;
        }
        if (audioPlayer.PlaybackState == PlaybackState.Playing) return;
        playButton.BackgroundImage = Resources.play;
        playbackTimer.Stop();
        UpdateTimeLabel();
    }

    private void SeekPlayback(long frame)
    {
        if (importing || currentWave is not { } stream) return;
        bool resume = audioPlayer.PlaybackState == PlaybackState.Playing;
        audioPlayer.Stop();
        playbackTimer.Stop();
        bool looping = stream.LoopEnabled;
        try
        {
            stream.LoopEnabled = false;
            stream.SeekFrames(Math.Clamp(frame, 0, stream.Length / stream.WaveFormat.BlockAlign));
        }
        finally { stream.LoopEnabled = looping; }
        playbackOriginFrames = stream.ElapsedFrames;
        audioPlayer.Init(stream);
        playButton.BackgroundImage = Resources.play;
        waveViewer.EnsurePositionVisible(stream.Position);
        UpdateTimeLabel();
        if (resume) PlayAudio();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => HandlePlaybackKey(keyData) || base.ProcessCmdKey(ref msg, keyData);

    private bool HandlePlaybackKey(Keys keyData)
    {
        if (!importing && currentWave is { } stream && !searchBox.ContainsFocus && !itemPropertyGrid.ContainsFocus)
        {
            if (keyData == Keys.Space)
            {
                PlayStopButton_Click(this, EventArgs.Empty);
                return true;
            }
            if ((waveViewer.ContainsFocus || playButton.ContainsFocus) && keyData is Keys.Left or Keys.Right or Keys.Home)
            {
                long frame = stream.GetPlaybackPosition(GetPlayedFrames(stream)) / stream.WaveFormat.BlockAlign;
                long target = keyData == Keys.Home ? 0 : frame + (keyData == Keys.Left ? -5L : 5L) * stream.WaveFormat.SampleRate;
                RunFileAction(() => SeekPlayback(target), "Could not seek audio");
                return true;
            }
        }
        return false;
    }

    private static string FormatTime(TimeSpan time) => string.Format("{0}:{1:00}:{2:000}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

    private long GetPlayedFrames(SingingWaveStream stream) => Math.Min(stream.ElapsedFrames,
        playbackOriginFrames + audioPlayer.GetPosition() / stream.WaveFormat.BlockAlign);

    private void UpdateTimeLabel()
    {
        if (currentWave is not { } ws)
        {
            timeLabel.Text = $"{FormatTime(TimeSpan.Zero)} / {FormatTime(TimeSpan.Zero)}";
            return;
        }

        var position = ws.GetPlaybackPosition(GetPlayedFrames(ws));
        waveViewer.PlaybackPosition = position;
        var current = TimeSpan.FromSeconds((double)position / ws.WaveFormat.AverageBytesPerSecond);
        var total = ws.TotalTime;

        timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
    }
}
