using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using HeavenTool.Properties;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow : Form
{
    private readonly Dictionary<TreeNode, Action> TreeNodeActions = [];
    private readonly WaveOutEvent audioPlayer = new();
    private IWaveProvider? currentWave;
    private readonly Timer playbackTimer = new();

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
        TreeNodeActions.Clear();

        using var stream = new FileStream(fileName, FileMode.Open);
        using var reader = new BinaryReader(stream);

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

            void InitializeBwav()
            {
                short[] pcm = GetPcm(bwav);

                byte[] buffer = new byte[pcm.Length * 2];
                Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);

                var ms = new MemoryStream(buffer);
                currentWave = new RawSourceWaveStream(ms,
                    new WaveFormat(bwav.Channels[0].SampleRate, 16, bwav.Channels.Length));

                audioPlayer.Stop();
                audioPlayer.Init(currentWave);

                playButton.Enabled = true;
                customWaveViewer1.WaveStream = (WaveStream)currentWave;
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

                    new ToolStripMenuItem("Export as WAV...", null, (_, _) =>
                    {
                        short[] pcm = GetPcm(bwav);

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
                        audioPlayer.Play();
                        playbackTimer.Start();
                        playButton.BackgroundImage = Resources.pause;
                    }),

                    new ToolStripSeparator(),

                    new ToolStripMenuItem("Override...", null, (a, b) =>
                    {
                        Console.WriteLine("Overriding");
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
        switch (magic)
        {
            case "BARS":
                {
                    stream.Position = 0;
                    var bars = new BARSFileReader(stream);

                    foreach (var item in bars.AudioAssets)
                    {
                        if (item.BinaryWave == null) continue;

                        AddItem(item.ToString(), item);
                    }
                    break;
                }

            case "BWAV":
                {
                    reader.Position = 0;
                    var buffer = reader.ReadByteArray((int)stream.Length);
                    var bwav = new BinaryWaveFile(buffer);

                    var name = Path.GetFileNameWithoutExtension(fileName);

                    AddItem(name, new AudioAsset()
                    {
                        BinaryWave = bwav
                    });
                    break;
                }
        }

        barsContainer.Enabled = true;
        barsTreeView.Enabled = true;
        itemPropertyGrid.Enabled = true;
    }

    private static short[] GetPcm(BinaryWaveFile binaryWave)
    {
        var channels = binaryWave.Channels;
        short[] pcm;
        // Mono
        if (channels.Length == 1)
        {
            pcm = channels[0].Decode();
        }
        // Stereo
        else if (channels.Length == 2)
        {
            var left = channels[0].Decode();
            var right = channels[1].Decode();
            pcm = new short[binaryWave.Channels[0].TotalSamples * 2];
            for (int i = 0; i < binaryWave.Channels[0].TotalSamples; i++)
            {
                pcm[i * 2] = left[i];
                pcm[i * 2 + 1] = right[i];
            }
        }
        else
        {
            throw new Exception("Exporting a BWAV with more than 2 channels is not supported at this moment.");
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

                audioPlayer.Play();
                playbackTimer.Start();
                playButton.BackgroundImage = Resources.pause;
            }
        }
    }

    #region time label updating
    private static string FormatTime(TimeSpan time) => string.Format("{0}:{1:00}:{2:000}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

    private void UpdateTimeLabel()
    {
        if (currentWave is not WaveStream ws)
            return;

        TimeSpan current = ws.CurrentTime;
        TimeSpan total = ws.TotalTime;

        timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
    }
    #endregion
}