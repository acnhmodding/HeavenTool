using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;
using HeavenTool.IO.Audio;
using HeavenTool.IO.Audio.Singing;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using HeavenTool.Properties;
using HeavenTool.Utility;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow : Form
{
    public record class Singer(string Name, string FileName)
    {
        public bool Checked { get; set; }
    }

    private readonly WaveOutEvent audioPlayer = new();
    private IWaveProvider? currentWave;
    private WaveStream? waveformStream;
    private long playbackOriginFrames;
    private readonly Timer playbackTimer = new();

    private string? loadedFileName = null;
    internal string lastLoadedPath = string.Empty;
    private BARSFileReader? barsFile;
    private BinaryWaveFile? bwavFile;
    private AudioAsset? currentAudioAsset;
    private string? currentAssetName;
    private readonly Dictionary<string, VoiceSampleBank> singingCache = new(StringComparer.OrdinalIgnoreCase);

    private ZstdDecompressor Decompressor { get; } = new();

    public List<Singer> Singers =
    [
        new("Girl", "Npc_Vocal_Girl.bars") {
            Checked = true
        },
        new("Boy", "Npc_Vocal_Boy.bars"),
        new("Man", "Npc_Vocal_Man.bars")
    ];

    public BARSWindow()
    {
        InitializeComponent();

        loopToolStripMenuItem.CheckedChanged += (_, _) =>
        {
            if (currentWave is SingingWaveStream stream)
                stream.LoopEnabled = loopToolStripMenuItem.Checked;
        };

        whistlingToolStripMenuItem.CheckedChanged += (_, _) => RefreshPreview();
        baseToolStripMenuItem.CheckedChanged += (_, _) => RefreshPreview();

        timeLabel.Text = "";
        barsTreeView.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is Action action)
                action();
        };

        barsContainer.Enabled = false;
        barsTreeView.Enabled = false;
        itemPropertyGrid.Enabled = false;

        audioPlayer.PlaybackStopped += (s, e) =>
        {
            if (audioPlayer.PlaybackState == PlaybackState.Playing) return;
            playButton.BackgroundImage = Resources.play;
            playbackTimer.Stop();
            UpdateTimeLabel();
        };

        volumeSlider.Volume = audioPlayer.Volume;
        volumeSlider.VolumeChanged += (_, e) => audioPlayer.Volume = volumeSlider.Volume;
        playbackTimer.Interval = 30;
        playbackTimer.Tick += (_, _) => UpdateTimeLabel();

        unloadToolStripMenuItem.Enabled = false;

        searchBox.Enabled = false;
        barsTreeView.DrawMode = TreeViewDrawMode.OwnerDrawText;
        barsTreeView.DrawNode += (_, e) =>
        {
            var searchText = searchBox.Text;
            if (string.IsNullOrEmpty(searchText) || e.Node == null)
            {
                e.DrawDefault = true;
                return;
            }

            if (e.Node.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                e.Graphics.FillRectangle(Brushes.DarkGray, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, barsTreeView.Font, e.Bounds, Color.White, TextFormatFlags.GlyphOverhangPadding);
            }
            else
            {
                e.DrawDefault = true;
            }
        };

        FormClosing += (_, e) => WinFormsUtility.FormClosingConfirmation(e, loadedFileName != null, UnloadFile);
    }

    private void LoadSingers()
    {
        singersToolStripMenuItem.DropDownItems.Clear();
        foreach (var singer in Singers)
        {
            var item = singersToolStripMenuItem.DropDownItems.Add(singer.Name);
            if (item is not ToolStripMenuItem singerMenuItem) continue;

            var path = Path.Combine(lastLoadedPath, singer.FileName);
            if (File.Exists(path))
            {
                item.Click += (_, e) =>
                {
                    singer.Checked = !singer.Checked;
                    singerMenuItem.Checked = singer.Checked;
                    RefreshPreview();
                };

                singerMenuItem.Checked = singer.Checked;
            }
            else
            {
                item.Enabled = false;
            }
        }
    }

    private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Select a .BARS file",
            Filter = "Audio Resource|*.bars;*.bwav;*.bars.zs",
            FilterIndex = 1,
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
            LoadFile(openFileDialog.FileName);
    }

    private void LoadFile(string fileName)
    {
        UnloadFile();

        loadedFileName = Path.GetFileName(fileName);
        lastLoadedPath = Path.GetDirectoryName(fileName) ?? string.Empty;

        ConsoleUtilities.WriteLine($"Loading {loadedFileName}", ConsoleColor.Gray);

        using var compressedStream = new FileStream(fileName, FileMode.Open);
        using var uncompressedStream = new MemoryStream();

        if (Decompressor.CanDecompress(compressedStream))
        {
            ConsoleUtilities.WriteLine("Decompressing BARS file...", ConsoleColor.Gray);
            Decompressor.Decompress(compressedStream, uncompressedStream);
        }
        else compressedStream.CopyTo(uncompressedStream);

        uncompressedStream.Position = 0;

        using var reader = new BinaryReader(uncompressedStream);

        LoadSingers();

        TreeNode AddNode(TreeNodeCollection collection, string name, Action? onSelect = null, Action<TreeNodeCollection>? parentBuilder = null)
        {
            var node = collection.Add(name);
            node.Tag = onSelect;
            parentBuilder?.Invoke(node.Nodes);

            return node;
        }

        TreeNode? AddPropertyNode(TreeNodeCollection collection, string name, object? objectToSelect, Action<TreeNodeCollection>? parentBuilder = null)
        {
            if (objectToSelect == null)
                return null;

            return AddNode(collection, name, () => itemPropertyGrid.SelectedObject = objectToSelect, parentBuilder);
        }

        void AddItem(string assetName, AudioAsset audioAsset)
        {
            var bwav = audioAsset.BinaryWave;
            var metadata = audioAsset.AudioMetadata;

            if (bwav == null)
                return;

            var nodeItem = AddNode(barsTreeView.Nodes, assetName, () =>
            {
                itemPropertyGrid.SelectedObject = bwav;
                InitializeBwav(assetName, audioAsset);
            });

            if (metadata != null)
            {
                var metadataNode = AddNode(nodeItem.Nodes, "Metadata", () =>
                {
                    itemPropertyGrid.SelectedObject = metadata;
                    InitializeBwav(assetName, audioAsset);
                }, metadataNodes =>
                {
                    if (metadata.MarkerList != null)
                    {
                        AddNode(metadataNodes, "Markers", () =>
                        {
                            itemPropertyGrid.SelectedObject = metadata.MarkerList;
                            InitializeBwav(assetName, audioAsset);
                        });
                    }

                    if (metadata.MusicInfo != null)
                    {
                        AddNode(metadataNodes, "Music Info (MINF)", () =>
                        {
                            itemPropertyGrid.SelectedObject = metadata.MusicInfo;
                            InitializeBwav(assetName, audioAsset);
                        }, minfNodes =>
                        {
                            AddPropertyNode(minfNodes, "Tempo Map", metadata.MusicInfo.TempoMap);
                            AddPropertyNode(minfNodes, "Chord Track", metadata.MusicInfo.ChordTrack);
                            AddPropertyNode(minfNodes, "Beat Map", metadata.MusicInfo.BeatMap);
                            AddPropertyNode(minfNodes, "Measure Map", metadata.MusicInfo.MeasureMap);
                            AddPropertyNode(minfNodes, "Named Marker Tracks", metadata.MusicInfo.NamedMarkerTracks, tracks =>
                            {
                                foreach (var track in metadata.MusicInfo.NamedMarkerTracks!.Tracks)
                                    AddPropertyNode(tracks, track.Name, track);
                            });
                            AddPropertyNode(minfNodes, "Named Event Tracks", metadata.MusicInfo.NamedEventTracks, tracks =>
                            {
                                foreach (var track in metadata.MusicInfo.NamedEventTracks!.Tracks)
                                    AddPropertyNode(tracks, track.Name, track);
                            });
                            AddPropertyNode(minfNodes, "Performance Sequence", metadata.MusicInfo.PerformanceSequence, h =>
                            {
                                var sequence = metadata.MusicInfo.PerformanceSequence!;
                                AddPropertyNode(h, "Vocals / Whistle", sequence.Vocals);
                                if (sequence.PitchBends.Length > 0)
                                    AddPropertyNode(h, "Pitch Bend Tracks", sequence.PitchBends);
                                if (sequence.Vibrato.Length > 0)
                                    AddPropertyNode(h, "Vibrato Tracks", sequence.Vibrato);
                                foreach (var (instrument, track) in sequence.Instruments.OrderBy(pair => pair.Key))
                                    AddPropertyNode(h, instrument == InstrumentType.AdditionalTrack ? "Additional Track" : instrument.ToString(), track);
                            });
                        });
                    }
                });

                if (audioAsset.RawAudioMetadata != null)
                {
                    metadataNode.ContextMenuStrip = WinFormsUtility.ContextMenu(b =>
                    {
                        b.AddItem("Export Metadata...", () =>
                        {
                            var fileName = $"{metadata.AssetName}.bwav.meta";

                            if (WinFormsUtility.SaveFileDialog("BWAV Metadata (*.bwav.meta)|*.bwav.meta", fileName, out var result))
                                File.WriteAllBytes(result, metadata.ToBytes());
                        });
                    });
                }
            }

            for (var channelIndex = 0; channelIndex < bwav.Channels.Length; channelIndex++)
                AddPropertyNode(nodeItem.Nodes, $"Channel #{channelIndex}", bwav.Channels[channelIndex]);

            nodeItem.ContextMenuStrip = WinFormsUtility.ContextMenu(b =>
            {
                b.AddItem("Export BWAV...", () =>
                {
                    var dialogFileName = metadata != null ? $"{metadata.AssetName}.bwav" : $"{assetName}.bwav";

                    if (WinFormsUtility.SaveFileDialog("Binary Waveform (*.bwav)|*.bwav", dialogFileName, out var result))
                        File.WriteAllBytes(result, audioAsset.RawBinaryWave ?? []);
                });

                b.AddItem("Export as WAV...", () =>
                {
                    var dialogFileName = metadata != null ? $"{metadata.AssetName}.wav" : $"{assetName}.wav";

                    if (WinFormsUtility.SaveFileDialog("Waveform (*.wav)|*.wav", dialogFileName, out var result))
                    {
                        var audio = GetAudioData(assetName, audioAsset);
                        SaveWave(audio.Samples, audio.SampleRate, audio.Channels, result);
                    }
                });

                b.AddItem("Preview Sound...", () =>
                {
                    InitializeBwav(assetName, audioAsset);
                    PlayAudio();
                });

                b.AddSeparator();

                b.Items.Add("Override...").Enabled = false;
            });
        }

        barsTreeView.SuspendLayout();
        barsTreeView.BeginUpdate();
        var magic = reader.ReadString(4);

        switch (magic)
        {
            case "BARS":
                {
                    uncompressedStream.Position = 0;
                    barsFile = new BARSFileReader(uncompressedStream);

                    foreach (var item in barsFile.AudioAssets)
                    {
                        if (item.BinaryWave == null)
                        {
                            ConsoleUtilities.WriteLine($"Audio asset with hash {item.Hash:X8} has no binary wave data.", ConsoleColor.Yellow);
                            continue;
                        }

                        AddItem(item.ToString(), item);
                    }
                    break;
                }

            case "BWAV":
                {
                    uncompressedStream.Position = 0;
                    bwavFile = new BinaryWaveFile(uncompressedStream);

                    var name = Path.GetFileNameWithoutExtension(fileName);

                    AddItem(name, new AudioAsset()
                    {
                        BinaryWave = bwavFile
                    });
                    break;
                }
        }

        barsTreeView.EndUpdate();
        barsTreeView.ResumeLayout();
        barsContainer.Enabled = true;
        barsTreeView.Enabled = true;
        itemPropertyGrid.Enabled = true;
        unloadToolStripMenuItem.Enabled = true;
        searchBox.Enabled = true;
    }

    private BwavPreviewAudio GetAudioData(string assetName, AudioAsset audioAsset, bool includeSinging = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetName);
        ArgumentNullException.ThrowIfNull(audioAsset);

        var bwav = audioAsset.BinaryWave ?? throw new Exception("BWAV not found!");

        var directory = lastLoadedPath;
        var loadPrefetchedFiles = readPrefetchedFilesToolStripMenuItem.Checked;
        var audio = BwavPreviewAudio.Load(bwav, assetName, directory, loadPrefetchedFiles);

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

    private void RefreshPreview()
    {
        if (currentAudioAsset != null && currentAssetName != null)
            InitializeBwav(currentAssetName, currentAudioAsset, true);
    }

    private void InitializeBwav(string assetName, AudioAsset audioAsset, bool preservePlayback = false)
    {
        var metadata = audioAsset.AudioMetadata;
        currentAudioAsset = audioAsset;
        currentAssetName = assetName;
        var audio = GetAudioData(assetName, audioAsset, includeSinging: false);

        var resume = preservePlayback && audioPlayer.PlaybackState == PlaybackState.Playing;
        var previous = preservePlayback ? currentWave as SingingWaveStream : null;
        var elapsed = previous != null ? GetPlayedFrames(previous) : 0;
        var looping = !preservePlayback || loopToolStripMenuItem.Checked;
        audioPlayer.Stop();
        waveViewer.WaveStream = null;
        waveViewer.PlaybackStream = null;
        waveViewer.PlaybackPosition = null;
        waveformStream?.Dispose();
        var playback = CreatePreviewStream(audio, audioAsset);
        playback.LoopEnabled = looping;
        if (previous != null) playback.RestorePlayback(previous, elapsed);
        if (currentWave is IDisposable previousWave)
            previousWave.Dispose();
        currentWave = playback;
        loopToolStripMenuItem.Enabled = playback.HasLoop;
        loopToolStripMenuItem.Checked = playback.LoopEnabled;
        playbackOriginFrames = playback.ElapsedFrames;
        audioPlayer.Init(currentWave);

        playButton.Enabled = true;
        waveformStream = CreatePreviewStream(audio, audioAsset);
        ((SingingWaveStream)waveformStream).LoopEnabled = false;
        waveViewer.PlaybackStream = playback;
        waveViewer.WaveStream = waveformStream;

        if (playback.HasLoop)
            waveViewer.SetLoop(audio.LoopStart, audio.LoopEnd);
        else
            waveViewer.RemoveLoop();

        waveViewer.ClearMarkers();

        if (metadata?.MarkerList != null)
            foreach (var marker in metadata.MarkerList)
                waveViewer.AddMarker(marker);

        if (resume) PlayAudio();
        UpdateTimeLabel();
    }

    private SingingWaveStream CreatePreviewStream(BwavPreviewAudio audio, AudioAsset asset)
    {
        var music = asset.AudioMetadata?.MusicInfo;
        var singers = new List<VoiceSampleBank>();
        VoiceSampleBank? whistles = null;
        if (music?.PerformanceSequence?.Vocals != null)
        {
            foreach (var singer in Singers.Where(s => s.Checked))
            {
                var path = Path.GetFullPath(Path.Combine(lastLoadedPath, singer.FileName));
                if (File.Exists(path))
                    singers.Add(LoadVoiceBank(path, Path.GetFileNameWithoutExtension(singer.FileName)));
            }
            var whistlePath = Path.GetFullPath(Path.Combine(lastLoadedPath, "Npc_Whistle.bars"));
            if (whistlingToolStripMenuItem.Checked && File.Exists(whistlePath))
                whistles = LoadVoiceBank(whistlePath, "Npc_Whistle");
        }
        return new SingingWaveStream(audio.Samples, audio.SampleRate, audio.Channels,
            audio.LoopStart, audio.LoopEnd, music?.PerformanceSequence,
            checked((int)(music?.SampleRate ?? (uint)audio.SampleRate)), singers, whistles,
            finiteMusicEnd: music?.LoopStart == music?.LoopEnd ? music?.LoopEnd ?? 0 : 0);
    }

    private VoiceSampleBank LoadVoiceBank(string path, string prefix)
    {
        if (!singingCache.TryGetValue(path, out var bank))
        {
            using var reader = File.OpenRead(path);
            using var bars = new BARSFileReader(reader);
            bank = new VoiceSampleBank(bars, prefix);
            singingCache[path] = bank;
        }
        return bank;
    }

    private void UnloadFile()
    {
        itemPropertyGrid.SelectedObject = null;

        barsFile?.Dispose();
        bwavFile?.Dispose();
        barsFile = null;
        bwavFile = null;

        loadedFileName = null;

        audioPlayer.Stop();

        if (currentWave is IDisposable ws)
            ws.Dispose();

        currentWave = null;
        waveformStream?.Dispose();
        waveformStream = null;
        waveViewer.PlaybackStream = null;
        waveViewer.PlaybackPosition = null;
        playbackOriginFrames = 0;
        loopToolStripMenuItem.Enabled = false;
        loopToolStripMenuItem.Checked = false;
        currentAudioAsset = null;
        currentAssetName = null;

        unloadToolStripMenuItem.Enabled = false;
        barsTreeView.Nodes.Clear();
        waveViewer.WaveStream = null;
        searchBox.Enabled = false;

        singingCache.Clear();

        UpdateTimeLabel();
    }

    public static void SaveWave(short[] pcm, int sampleRate, int channels, string outputPath)
    {
        using var waveFile = new WaveFileWriter(outputPath, new WaveFormat(sampleRate, 16, channels));
        var buffer = new byte[pcm.Length * 2]; // 16-bit = 2 bytes
        Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);
        waveFile.Write(buffer, 0, buffer.Length);
    }

    private void PlayStopButton_Click(object sender, EventArgs e)
    {
        if (audioPlayer != null)
        {
            var isPlaying = audioPlayer.PlaybackState == PlaybackState.Playing;
            if (isPlaying)
            {
                audioPlayer.Pause();
                UpdateTimeLabel();
                playbackTimer.Stop();
                playButton.BackgroundImage = Resources.play;
            }
            else
            {
                if (audioPlayer.PlaybackState == PlaybackState.Stopped
                    && currentWave is WaveStream ws && ws.Position >= ws.Length)
                {
                    ws.Position = 0;
                    playbackOriginFrames = 0;
                    audioPlayer.Init(ws);
                }

                PlayAudio();
            }
        }
    }

    private void PlayAudio()
    {
        audioPlayer.Play();
        playbackTimer.Start();
        playButton.BackgroundImage = Resources.pause;
        waveViewer.Invalidate();
    }

    #region time label updating
    private static string FormatTime(TimeSpan time) => string.Format("{0}:{1:00}:{2:000}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

    private long GetPlayedFrames(SingingWaveStream stream) => Math.Min(stream.ElapsedFrames,
        playbackOriginFrames + audioPlayer.GetPosition() / stream.WaveFormat.BlockAlign);

    private long GetPlaybackPosition(WaveStream stream) => stream is SingingWaveStream singing
        ? singing.GetPlaybackPosition(GetPlayedFrames(singing)) : stream.Position;

    private void UpdateTimeLabel()
    {
        if (currentWave is not WaveStream ws)
        {
            timeLabel.Text = $"{FormatTime(TimeSpan.Zero)} / {FormatTime(TimeSpan.Zero)}";
            return;
        }

        var position = GetPlaybackPosition(ws);
        waveViewer.PlaybackPosition = position;
        var current = TimeSpan.FromSeconds((double)position / ws.WaveFormat.AverageBytesPerSecond);
        TimeSpan total = ws.TotalTime;

        timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
        waveViewer.Invalidate();
    }
    #endregion

    private void UnloadToolStripMenuItem_Click(object sender, EventArgs e)
    {
        UnloadFile();
    }

    private void SearchBox_TextChanged(object sender, EventArgs e)
    {
        barsTreeView.Invalidate();
    }

    private void ReadPrefetchedFilesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        readPrefetchedFilesToolStripMenuItem.Checked = !readPrefetchedFilesToolStripMenuItem.Checked;
    }
}
