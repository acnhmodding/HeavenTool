using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow : Form
{
    public BARSWindow()
    {
        InitializeComponent();

        barsTreeView.NodeMouseClick += TreeView_OnNodeSelection;
        barsContainer.Enabled = false;
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

    private readonly Dictionary<TreeNode, Action> TreeNodeActions = [];
    private readonly WaveOutEvent audioPlayer = new();
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

        void AddItem(string assetName, BinaryWaveFile bwav, AudioMetadata? metadata = null)
        {
            void InitializeBwav()
            {
                short[] pcm = GetPcm(bwav);
                // Convert short[] PCM → byte[]
                byte[] buffer = new byte[pcm.Length * 2];
                Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);

                // Create a stream over it
                var ms = new MemoryStream(buffer);
                using var waveStream = new RawSourceWaveStream(ms, new WaveFormat(bwav.Channels[0].SampleRate, 16, bwav.Channels.Length)); // adjust rate/channels

                //var waveOut = new WaveOutEvent();
                if (audioPlayer.PlaybackState == PlaybackState.Playing)
                    audioPlayer.Stop();

                audioPlayer.Init(waveStream);

                playButton.Enabled = true;
                customWaveViewer1.WaveStream = waveStream;
            }
            
            var nodeItem = barsTreeView.Nodes.Add(assetName);

            TreeNodeActions.Add(nodeItem, () =>
            {
                itemPropertyGrid.SelectedObject = bwav;
                InitializeBwav();
            });

            if (metadata != null)
            {
                AddNode(nodeItem.Nodes, "Metadata", () =>
                {
                    itemPropertyGrid.SelectedObject = metadata;
                    InitializeBwav();
                });
            }

            var ctxMenu = new ContextMenuStrip();
            nodeItem.ContextMenuStrip = ctxMenu;

            ctxMenu.Items.Add("Export as WAV...", null, (_, _) =>
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
            });

            ctxMenu.Items.Add("Preview Sound...", null, (_, _) =>
            {
                short[] pcm = GetPcm(bwav);
                // Convert short[] PCM → byte[]
                byte[] buffer = new byte[pcm.Length * 2];
                Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);

                // Create a stream over it
                var ms = new MemoryStream(buffer);
                using var waveStream = new RawSourceWaveStream(ms, new WaveFormat(bwav.Channels[0].SampleRate, 16, bwav.Channels.Length)); // adjust rate/channels

                if (audioPlayer.PlaybackState == PlaybackState.Playing)
                    audioPlayer.Stop();

                audioPlayer.Init(waveStream);
                audioPlayer.Play();
                playButton.Enabled = true;
                customWaveViewer1.WaveStream = waveStream;
            });

            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("Override...", null, (a, b) =>
            {
                Console.WriteLine("Overriding");
            });

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

                        AddItem(item.ToString(), item.BinaryWave, item.AudioMetadata);
                    }
                    break;
                }

            case "BWAV":
                {
                    reader.Position = 0;
                    var buffer = reader.ReadByteArray((int)stream.Length);
                    var bwav = new BinaryWaveFile(buffer);

                    var name = Path.GetFileNameWithoutExtension(fileName);

                    AddItem(name, bwav);
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

    private void TreeView_OnNodeSelection(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node != null && TreeNodeActions.TryGetValue(e.Node, out Action? onNodeSelected))
            onNodeSelected?.Invoke();
    }

    private void BARSWindow_Load(object sender, EventArgs e)
    {
        barsTreeView.Enabled = false;
        itemPropertyGrid.Enabled = false;
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
                audioPlayer.Pause();
            
            else
            {
                // TODO: We need to init AudioPlayer with .Init() before playing
                // So we probably need to re-init the AudioPlayer every time a new node is selected
                audioPlayer.Play();

            }
        }
    }
}