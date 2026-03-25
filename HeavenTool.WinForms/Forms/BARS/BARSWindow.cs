using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BARS.MINF.Sections.SectionH_SubSections;
using HeavenTool.IO.FileFormats.BWAV;
using HeavenTool.Properties;
using HeavenTool.Utility;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly Timer playbackTimer = new();

    private string? loadedFileName = null;
    internal string lastLoadedPath = string.Empty;
    private BARSFileReader? barsFile;
    private BinaryWaveFile? bwavFile;

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
            playButton.BackgroundImage = Resources.play;
            playbackTimer.Stop();
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
            Filter = "Audio Resource|*.bars;*.bwav",
            FilterIndex = 1,
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
            LoadFile(openFileDialog.FileName);
    }

    private async void LoadFile(string fileName)
    {
        UnloadFile();

        using var stream = new FileStream(fileName, FileMode.Open);
        using var reader = new BinaryReader(stream);

        loadedFileName = Path.GetFileNameWithoutExtension(fileName);
        lastLoadedPath = Path.GetDirectoryName(fileName) ?? string.Empty;

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

                    if (metadata.MINF != null)
                    {
                        var minfNode = AddNode(metadataNodes, "MINF", () =>
                        {
                            itemPropertyGrid.SelectedObject = metadata.MINF;
                            InitializeBwav(assetName, audioAsset);
                        }, minfNodes =>
                        {
                            AddPropertyNode(minfNodes, "Section A", metadata.MINF.SectionA);
                            AddPropertyNode(minfNodes, "Chords Section", metadata.MINF.SectionB);
                            AddPropertyNode(minfNodes, "Section D", metadata.MINF.SectionD);
                            AddPropertyNode(minfNodes, "Section E", metadata.MINF.SectionE);
                            AddPropertyNode(minfNodes, "Section H", metadata.MINF.SectionH, h =>
                            {
                                AddPropertyNode(h, "Notes", metadata.MINF.SectionH?.Notes);
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
                            var fileName = metadata != null ? $"{metadata.AssetName}.bwav.meta" : $"{assetName}.bwav.meta";

                            if (WinFormsUtility.SaveDialog("BWAV Metadata (*.bwav.meta)|*.bwav.meta", fileName, out string? result))
                                File.WriteAllBytes(result, audioAsset.RawAudioMetadata);
                        });
                    });
                }
            }

            for (int channelIndex = 0; channelIndex < bwav.Channels.Length; channelIndex++)
                AddPropertyNode(nodeItem.Nodes, $"Channel #{channelIndex}", bwav.Channels[channelIndex]);

            nodeItem.ContextMenuStrip = WinFormsUtility.ContextMenu(b =>
            {
                b.AddItem("Export BWAV...", () =>
                {
                    var dialogFileName = metadata != null ? $"{metadata.AssetName}.bwav" : $"{assetName}.bwav";

                    if (WinFormsUtility.SaveDialog("Binary Waveform (*.bwav)|*.bwav", dialogFileName, out string? result))
                        File.WriteAllBytes(result, audioAsset.RawBinaryWave ?? []);
                });

                b.AddItem("Export as WAV...", async () =>
                {
                    var dialogFileName = metadata != null ? $"{metadata.AssetName}.wav" : $"{assetName}.wav";

                    if (WinFormsUtility.SaveDialog("Waveform (*.wav)|*.wav", dialogFileName, out string? result))
                    {
                        short[] pcm = await GetAudioData(assetName, audioAsset);
                        SaveWave(pcm, bwav.Channels[0].SampleRate, bwav.Channels.Length, result);
                    }
                });

                b.AddItem("Preview Sound...", () =>
                {
                    InitializeBwav(assetName, audioAsset);
                    PlayAudio();
                });

                b.AddSeparator();

                b.AddItem("Override...", () =>
                {
                    var openFileDialog = new OpenFileDialog
                    {
                        Title = "Select a .BWAV or .WAV file to override with",
                        Filter = "Audio Files|*.bwav;*.wav",
                        RestoreDirectory = true,
                    };

                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    var selectedFile = openFileDialog.FileName;
                    var ext = Path.GetExtension(selectedFile).ToLower();

                    if (ext == ".wav")
                    {
                        // get pcm data from the wav file using NAudio
                        using var reader = new WaveFileReader(selectedFile);
                        byte[] buffer = new byte[reader.Length];
                        int bytesRead = reader.Read(buffer, 0, buffer.Length);

                        short[] samples = new short[buffer.Length / 2];
                        Buffer.BlockCopy(buffer, 0, samples, 0, buffer.Length);

                        var encoded = BinaryWaveChannel.Encode(samples, out var coeffs);
                        var newBwav = new BinaryWaveFile(reader.WaveFormat.Channels);

                        var newChannel = new BinaryWaveChannel(encoded, coeffs)
                        {
                            Codec = BinaryWaveChannel.CodecType.DSP_ADPCM,
                            ChannelPan = BinaryWaveChannel.PanType.Center,
                            SampleRate = reader.WaveFormat.SampleRate,
                            TotalSamples = samples.Length,
                        };
                    }
                });
            });            
        }

        barsTreeView.SuspendLayout();
        barsTreeView.BeginUpdate();
        var magic = reader.ReadString(4);

        switch (magic)
        {
            case "BARS":
                {
                    stream.Position = 0;
                    barsFile = new BARSFileReader(stream);

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
                    stream.Position = 0;
                    bwavFile = new BinaryWaveFile(stream);

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

    private async Task<short[]> GetAudioData(string assetName, AudioAsset audioAsset)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetName);
        ArgumentNullException.ThrowIfNull(audioAsset);

        var bwav = audioAsset.BinaryWave ?? throw new Exception("BWAV not found!");

        short[] pcm = await GetPcm(bwav);

        if (pcm.Length == 0 || bwav.IsPrefetch == 1)
        {
            pcm = new short[bwav.Channels[0].TotalSamplesPrefetch * bwav.Channels.Length];

            var loadPrefetchedFiles = readPrefetchedFilesToolStripMenuItem.Checked;
            var fileLocation = Path.Combine(lastLoadedPath, "Stream", $"{assetName}.bwav");

            if (loadPrefetchedFiles && File.Exists(fileLocation))
            {
                using var reader = File.OpenRead(fileLocation);
                using var prefetched_bwav = new BinaryWaveFile(reader, true);

                pcm = await GetPcm(prefetched_bwav);
            }
        }

        if (audioAsset.AudioMetadata?.MINF?.SectionH?.Notes is NotesSubSection notesSection)
            AddSingingToPcm(pcm, bwav, notesSection.Notes);

        return pcm;
    }

    private async void InitializeBwav(string assetName, AudioAsset audioAsset)
    {
        var metadata = audioAsset.AudioMetadata;
        var bwav = audioAsset.BinaryWave ?? throw new Exception("BWAV not found!");

        short[] pcm = await GetAudioData(assetName, audioAsset);

        // Convert PCM16 buffer to bytes
        byte[] buffer = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);

        var ms = new MemoryStream(buffer);
        currentWave = new RawSourceWaveStream(ms, new WaveFormat(bwav.Channels[0].SampleRate, 16, bwav.Channels.Length));

        audioPlayer.Stop();
        audioPlayer.Init(currentWave);

        playButton.Enabled = true;
        waveViewer.WaveStream = (WaveStream)currentWave;

        if (bwav.Channels[0].LoopEnd > 0)
            waveViewer.SetLoop(bwav.Channels[0].LoopStart, bwav.Channels[0].LoopEnd);
        else
            waveViewer.RemoveLoop();

        waveViewer.ClearMarkers();

        if (metadata?.MarkerList != null)
            foreach (var marker in metadata.MarkerList)
                waveViewer.AddMarker(marker);

        UpdateTimeLabel();
    }

    private static readonly Dictionary<string, short[]> singingCache = [];
    private async void AddSingingToPcm(short[] target, BinaryWaveFile bwav, List<NotesSubSection.Note> notes)
    {
        foreach (var singer in Singers)
        {
            if (!singer.Checked) continue;

            string singerName = Path.GetFileNameWithoutExtension(singer.FileName);
            string path = Path.Combine(lastLoadedPath, singer.FileName);

            if (!File.Exists(path)) continue;

            using var reader = File.OpenRead(path);
            using var bars = new BARSFileReader(reader);

            var audioFiles = bars.AudioAssets.ToDictionary(x => x.ToString(), y => y.BinaryWave);

            async Task<short[]> GetAudioPCM16(string assetName)
            {
                if (singingCache.TryGetValue(assetName, out var cached))
                    return cached;

                short[] pcm;
                if (audioFiles.TryGetValue(assetName, out var data) && data != null)
                    pcm = await GetPcm(data);
                else pcm = [];

                return singingCache[assetName] = pcm;
            }

            foreach (var note in notes)
            {
                short[] audio = await GetAudioPCM16($"{singerName}_{note.Vowel}_{note.Pitch}");

                if (audio.Length == 0) continue;

                int channels = bwav.Channels.Length;
                int noteSamples = note.Length;

                SingHelper.MixCountPCM16(audio, target, channels, note.Start, 0, note.Length, note.Volume, false);
            }
        }
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

        unloadToolStripMenuItem.Enabled = false;
        barsTreeView.Nodes.Clear();
        waveViewer.WaveStream = null;
        searchBox.Enabled = false;

        singingCache.Clear();

        UpdateTimeLabel();
    }

    private static async Task<short[]> GetPcm(BinaryWaveFile binaryWave)
    {
        int channelCount = binaryWave.Channels.Length;

        if (channelCount == 0)
            throw new Exception("No channels found.");

        int totalSamples = binaryWave.Channels[0].TotalSamples;

        if (totalSamples == 0)
            return [];

        short[][] decoded = new short[channelCount][];
        for (int c = 0; c < channelCount; c++)
            decoded[c] = binaryWave.Channels[c].Decode();

        short[][] ordered = new short[channelCount][];

        for (int i = 0; i < channelCount; i++)
        {
            var ch = binaryWave.Channels[i];
            int index = ch.ChannelPan switch
            {
                BinaryWaveChannel.PanType.Left => 0,
                BinaryWaveChannel.PanType.Right => channelCount > 1 ? 1 : 0,
                BinaryWaveChannel.PanType.Center => channelCount > 2 ? 2 : channelCount - 1,
                _ => 0
            };

            ordered[index] = decoded[i];
        }

        for (int i = 0; i < channelCount; i++)
        {
            if (ordered[i] == null)
                ordered[i] = decoded[0];
        }

        short[] pcm = new short[totalSamples * channelCount];
        for (int i = 0; i < totalSamples; i++)
        {
            for (int c = 0; c < channelCount; c++)
            {
                pcm[i * channelCount + c] = ordered[c][i];
            }
        }

        return pcm;
    }


    public static void SaveWave(short[] pcm, int sampleRate, int channels, string outputPath)
    {
        using var waveFile = new WaveFileWriter(outputPath, new WaveFormat(sampleRate, 16, channels));
        byte[] buffer = new byte[pcm.Length * 2]; // 16-bit = 2 bytes
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
                playbackTimer.Stop();
                playButton.BackgroundImage = Resources.play;
            }
            else
            {
                if (currentWave is WaveStream ws && ws.Position >= ws.Length)
                    ws.Position = 0; // rewind if finished

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

    private void UpdateTimeLabel()
    {
        if (currentWave is not WaveStream ws)
        {
            timeLabel.Text = $"{FormatTime(TimeSpan.Zero)} / {FormatTime(TimeSpan.Zero)}";
            return;
        }

        TimeSpan current = ws.CurrentTime;
        TimeSpan total = ws.TotalTime;

        timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
        waveViewer.Invalidate();
    }
    #endregion

    private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (barsFile != null)
        {
            if (WinFormsUtility.SaveDialog("Audio Resource|*.bars", $"{loadedFileName}.bars", out string? result))
            {

            }

        }
        else if (bwavFile != null)
        {
            if (WinFormsUtility.SaveDialog("Binary Wave|*.bwav", $"{loadedFileName}.bwav", out string? result))
            {

            }
        }
    }

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