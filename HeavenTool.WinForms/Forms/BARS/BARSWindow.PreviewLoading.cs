using HeavenTool.IO.Audio;
using HeavenTool.IO.Audio.Singing;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow
{
    private readonly SemaphoreSlim previewWorker = new(1, 1);
    private CancellationTokenSource? previewCancellation;
    private Task previewTask = Task.CompletedTask;
    private sealed record PreviewRequest(string Name, AudioAsset Asset, BinaryWaveFile? Wave, AudioMetadata? Metadata)
    {
        public PreviewRequest(string name, AudioAsset asset) : this(name, asset, asset.BinaryWave, asset.AudioMetadata) { }
        public bool Matches(AudioAsset asset) => ReferenceEquals(Asset, asset)
            && ReferenceEquals(Wave, asset.BinaryWave) && ReferenceEquals(Metadata, asset.AudioMetadata);
    }
    private PreviewRequest? pendingPreview;

    private void CancelPreview()
    {
        previewCancellation?.Cancel();
        previewCancellation = null;
        pendingPreview = null;
    }

    private void QueuePreview(string name, AudioAsset asset, bool preservePlayback = false)
    {
        CancelPreview();
        preservePlayback &= ReferenceEquals(currentAudioAsset, asset);
        if (!preservePlayback) ClearPlayback();
        pendingPreview = new PreviewRequest(name, asset);
        var cancellation = previewCancellation = new CancellationTokenSource();
        if (currentWave == null) timeLabel.Text = "Loading preview...";
        previewTask = PreparePreviewAsync(name, asset, preservePlayback, cancellation);
    }

    private async Task PreparePreviewAsync(string name, AudioAsset asset, bool preservePlayback,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        var cache = singingCache;
        var directory = lastLoadedPath;
        bool readStreams = readPrefetchedFilesToolStripMenuItem.Checked;
        bool includeBase = baseToolStripMenuItem.Checked;
        var voiceFiles = GetVoiceFiles();
        var streams = new Dictionary<string, BinaryWaveFile>(StringComparer.Ordinal);
        SingingWaveStream? playback = null, waveform = null;
        try
        {
            using var wave = asset.BinaryWave!.CreateSnapshot();
            var metadataBytes = asset.AudioMetadata?.ToBytes();
            var format = asset.AudioMetadata?.MusicInfo?.PerformanceSequence?.Format;
            foreach (string streamName in new[] { name, name.EndsWith("_L", StringComparison.Ordinal)
                ? name[..^1] + "R" : name.EndsWith("_R", StringComparison.Ordinal) ? name[..^1] + "L" : name }.Distinct())
                if (FindReplacementStream(streamName) is { } replacement)
                    streams.Add(streamName, replacement.CreateSnapshot());

            var audio = await Task.Run(async () =>
            {
                await previewWorker.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    var metadata = metadataBytes == null ? null : new AudioMetadata(metadataBytes, format);
                    var audio = BwavPreviewAudio.Load(wave, name, directory, readStreams, metadata,
                        streamName => streams.GetValueOrDefault(streamName), token);
                    if (!includeBase) Array.Clear(audio.Samples);
                    var music = metadata?.MusicInfo;
                    var (singers, whistle) = LoadVoiceBanks(music, voiceFiles, directory, cache, token);
                    token.ThrowIfCancellationRequested();
                    playback = CreatePreviewStream(audio, music, singers, whistle);
                    token.ThrowIfCancellationRequested();
                    waveform = CreatePreviewStream(audio, music, singers, whistle);
                    waveform.LoopEnabled = false;
                    return audio;
                }
                finally { previewWorker.Release(); }
            });
            token.ThrowIfCancellationRequested();
            if (resourcesDisposed || IsDisposed || !ReferenceEquals(previewCancellation, cancellation)) return;
            ApplyPreview(name, asset, audio, playback!, waveform!, preservePlayback);
            playback = waveform = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested && !resourcesDisposed && !IsDisposed)
                MessageBox.Show(this, ex.Message, "Could not preview audio", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            playback?.Dispose();
            waveform?.Dispose();
            foreach (var stream in streams.Values) stream.Dispose();
            if (ReferenceEquals(previewCancellation, cancellation))
            {
                previewCancellation = null;
                pendingPreview = null;
                if (!resourcesDisposed && !IsDisposed && currentWave == null) UpdateTimeLabel();
            }
            cancellation.Dispose();
        }
    }
}
