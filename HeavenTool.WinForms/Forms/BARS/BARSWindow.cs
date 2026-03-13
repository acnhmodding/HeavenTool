using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using HeavenTool.Properties;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow : Form
{
    private readonly Dictionary<TreeNode, Action> TreeNodeActions = [];
    private readonly WaveOutEvent audioPlayer = new();
    private IWaveProvider? currentWave;
    private readonly Timer playbackTimer = new();

    private string loadedFileName = "";
    private BARSFileReader? barsFile;
    private BinaryWaveFile? bwavFile;

    public BARSWindow()
    {
        InitializeComponent();

        timeLabel.Text = "";
        barsTreeView.NodeMouseClick += (_, e) =>
        {
            if (e.Node != null && TreeNodeActions.TryGetValue(e.Node, out Action? onNodeSelected))
                onNodeSelected?.Invoke();
        };

        barsContainer.Enabled = false;
        barsTreeView.Enabled = false;
        itemPropertyGrid.Enabled = false;

        audioPlayer.PlaybackStopped += (s, e) =>
        {
            playButton.BackgroundImage = Resources.play;
            playbackTimer.Stop();
        };

        playbackTimer.Interval = 30;
        playbackTimer.Tick += (_, _) => UpdateTimeLabel();

        unloadToolStripMenuItem.Enabled = false;
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

    private void LoadFile(string fileName)
    {
        UnloadFile();

        using var stream = new FileStream(fileName, FileMode.Open);
        using var reader = new BinaryReader(stream);

        loadedFileName = Path.GetFileNameWithoutExtension(fileName);

        TreeNode AddNode(TreeNodeCollection collection, string name, Action action)
        {
            var node = collection.Add(name);

            TreeNodeActions.Add(node, action);

            return node;
        }

        void AddItem(string assetName, AudioAsset audioAsset)
        {
            var bwav = audioAsset.BinaryWave;
            var metadata = audioAsset.AudioMetadata;

            if (bwav == null)
                return;

            async void InitializeBwav()
            {
                short[] pcm = await GetPcm(bwav);

                byte[] buffer = new byte[pcm.Length * 2];
                Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);

                var ms = new MemoryStream(buffer);
                currentWave = new RawSourceWaveStream(ms,
                    new WaveFormat(bwav.Channels[0].SampleRate, 16, bwav.Channels.Length));

                audioPlayer.Stop();
                audioPlayer.Init(currentWave);

                playButton.Enabled = true;
                waveViewer.WaveStream = (WaveStream)currentWave;

                waveViewer.ClearMarkers();

                if (metadata != null && metadata.MarkerList != null)
                    foreach(var marker in metadata.MarkerList)
                        waveViewer.AddMarker(marker);
                    

                //waveViewer.Invalidate();
                UpdateTimeLabel();
            }

            var nodeItem = barsTreeView.Nodes.Add(assetName);

            TreeNodeActions.Add(nodeItem, () =>
            {
                itemPropertyGrid.SelectedObject = bwav;
                InitializeBwav();
            });

            if (metadata != null)
            {
                var metadataNode = AddNode(nodeItem.Nodes, "Metadata", () =>
                {
                    itemPropertyGrid.SelectedObject = metadata;
                    InitializeBwav();
                });

                if (metadata.MarkerList != null)
                {
                    AddNode(metadataNode.Nodes, "Markers", () =>
                    {
                        itemPropertyGrid.SelectedObject = metadata.MarkerList;
                        InitializeBwav();
                    });
                }

                if (metadata.MINF != null)
                {
                    var minfNode = AddNode(metadataNode.Nodes, "MINF", () =>
                    {
                        itemPropertyGrid.SelectedObject = metadata.MINF;
                        InitializeBwav();
                    });

                    if (metadata.MINF.SectionA != null)
                        AddNode(minfNode.Nodes, "Section A", () =>
                        {
                            itemPropertyGrid.SelectedObject = metadata.MINF.SectionA;
                        });

                    if (metadata.MINF.ChordsSection != null)
                        AddNode(minfNode.Nodes, "Chords Section", () =>
                        {
                            itemPropertyGrid.SelectedObject = metadata.MINF.ChordsSection;
                        });
                }

                if (audioAsset.RawAudioMetadata != null)
                {
                    metadataNode.ContextMenuStrip = new ContextMenuStrip()
                    {
                        Items =
                        {
                            new ToolStripMenuItem("Export Metadata...", null, (_, _) =>
                            {
                                using var saveFileDialog = new SaveFileDialog
                                {
                                    Filter = "BWAV Metadata (*.bwav.meta)|*.bwav.meta",
                                    FilterIndex = 1,
                                    RestoreDirectory = true,
                                    OverwritePrompt = true,
                                    FileName = metadata != null ? $"{metadata.AssetName}.bwav.meta" : $"{assetName}.bwav.meta",
                                };

                                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                                {
                                    File.WriteAllBytes(saveFileDialog.FileName, audioAsset.RawAudioMetadata);
                                }
                            }),
                        }
                    };
                }
            }

            nodeItem.ContextMenuStrip = new ContextMenuStrip()
            {
                Items =
                {
                    new ToolStripMenuItem("Export BWAV...", null, (_, _) =>
                    {
                        using var saveFileDialog = new SaveFileDialog
                        {
                            Filter = "Binary Waveform (*.bwav)|*.bwav",
                            FilterIndex = 1,
                            RestoreDirectory = true,
                            OverwritePrompt = true,
                            FileName = metadata != null ? $"{metadata.AssetName}.bwav" : $"{assetName}.bwav",
                        };

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllBytes(saveFileDialog.FileName, audioAsset.RawBinaryWave ?? []);
                        }
                    }),

                    new ToolStripMenuItem("Export as WAV...", null, async (_, _) =>
                    {
                        short[] pcm = await GetPcm(bwav);

                        using var saveFileDialog = new SaveFileDialog
                        {
                            Filter = "Waveform (*.wav)|*.wav",
                            FilterIndex = 1,
                            RestoreDirectory = true,
                            OverwritePrompt = true,
                            FileName = metadata != null ? $"{metadata.AssetName}.wav" : $"{assetName}.wav",
                        };

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            SaveWave(pcm, bwav.Channels[0].SampleRate, bwav.Channels.Length, saveFileDialog.FileName);
                        }
                    }),

                    new ToolStripMenuItem("Preview Sound...", null, (_, _) =>
                    {
                        InitializeBwav();
                        PlayAudio();
                    }),

                    new ToolStripSeparator(),

                    new ToolStripMenuItem("Override...", null, (a, b) =>
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

                    })

                }
            };

            for (int channelIndex = 0; channelIndex < bwav.Channels.Length; channelIndex++)
            {
                var channel = bwav.Channels[channelIndex];
                AddNode(nodeItem.Nodes, $"Channel #{channelIndex}", () =>
                {
                    itemPropertyGrid.SelectedObject = channel;
                });
            }
        }

        var magic = reader.ReadString(4);
        barsTreeView.SuspendLayout();
        barsTreeView.BeginUpdate();

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
    }

    private void UnloadFile()
    {
        barsFile = null;
        bwavFile = null;

        if (currentWave is IDisposable ws)
            ws.Dispose();

        currentWave = null;

        unloadToolStripMenuItem.Enabled = false;
        barsTreeView.Nodes.Clear();
        TreeNodeActions.Clear();
        waveViewer.WaveStream = null;

        UpdateTimeLabel();
    }

    private static async Task<short[]> GetPcm(BinaryWaveFile binaryWave)
    {
        var channels = binaryWave.Channels;
        int channelCount = channels.Length;

        if (channelCount == 0)
            throw new Exception("No channels found.");

        int totalSamples = channels[0].TotalSamples;

        // Decode all channels
        short[][] decoded = new short[channelCount][];
        for (int c = 0; c < channelCount; c++)
        {
            decoded[c] = channels[c].Decode();

            if (decoded[c].Length != totalSamples)
                throw new Exception("Channel sample counts do not match.");
        }

        // Interleave samples
        short[] pcm = new short[totalSamples * channelCount];

        for (int i = 0; i < totalSamples; i++)
        {
            for (int c = 0; c < channelCount; c++)
            {
                pcm[i * channelCount + c] = decoded[c][i];
            }
        }

        return pcm;
    }


    public static void SaveWave(short[] pcm, int sampleRate, int channels, string outputPath)
    {
        using var waveFile = new WaveFileWriter(outputPath, new WaveFormat(sampleRate, 16, channels));
        byte[] buffer = new byte[pcm.Length * 2]; // 16-bit = 2 bytes
        for (int i = 0; i < pcm.Length; i++)
        {
            buffer[i * 2] = (byte)(pcm[i] & 0xFF);
            buffer[i * 2 + 1] = (byte)((pcm[i] >> 8) & 0xFF);
        }
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
    }
    #endregion

    private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (barsFile != null)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save .BARS file",
                Filter = "Audio Resource|*.bars",
                RestoreDirectory = true,
                FileName = $"{loadedFileName}.bars",
            };

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return;

            

        } else if (bwavFile != null)
        {

        }
    }

    private void UnloadToolStripMenuItem_Click(object sender, EventArgs e)
    {
        UnloadFile();
    }
}