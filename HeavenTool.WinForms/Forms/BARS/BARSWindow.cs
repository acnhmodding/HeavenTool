using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Windows.Forms;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.Forms.BARS
{
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

            var magic = reader.ReadString(4);
            switch (magic)
            {
                case "BARS":
                    {
                        stream.Position = 0;
                        var bars = new BARSFileReader(stream);

                        foreach (var item in bars.AudioAssets)
                        {
                            if (item.binaryWave == null) continue;

                            var nodeItem = barsTreeView.Nodes.Add(item.amtaData.AssetName);

                            AddNode(nodeItem.Nodes, "Metadata", () =>
                            {
                                itemPropertyGrid.SelectedObject = item.amtaData;
                            });

                            var bwavNode = AddNode(nodeItem.Nodes, "BWAV", () =>
                            {
                                itemPropertyGrid.SelectedObject = item.binaryWave;
                            });


                            var ctxMenu = new ContextMenuStrip();
                            bwavNode.ContextMenuStrip = ctxMenu;
                            ctxMenu.Items.Add("Export as WAV...", null, (_, _) =>
                            {
                                short[] pcm = GetPcm(item.binaryWave);

                                using var saveFileDialog = new SaveFileDialog
                                {
                                    Filter = "Waveform (*.wav)|*.wav",
                                    FilterIndex = 1,
                                    RestoreDirectory = true,
                                    OverwritePrompt = true,
                                    FileName = item.amtaData.AssetName + ".wav",
                                };

                                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                                {
                                    SaveWave(pcm, item.binaryWave.Channels[0].SampleRate, item.binaryWave.Channels.Length, saveFileDialog.FileName);
                                }
                            });

                            ctxMenu.Items.Add("Preview Sound...", null, (_, _) =>
                            {
                                short[] pcm = GetPcm(item.binaryWave);
                                // Convert short[] PCM → byte[]
                                byte[] buffer = new byte[pcm.Length * 2];
                                Buffer.BlockCopy(pcm, 0, buffer, 0, buffer.Length);

                                // Create a stream over it
                                var ms = new MemoryStream(buffer);
                                using var waveStream = new RawSourceWaveStream(ms, new WaveFormat(item.binaryWave.Channels[0].SampleRate, 16, item.binaryWave.Channels.Length)); // adjust rate/channels

                                //var waveOut = new WaveOutEvent();
                                if (audioPlayer.PlaybackState == PlaybackState.Playing)
                                    audioPlayer.Stop();

                                audioPlayer.Init(waveStream);
                                audioPlayer.Play();

                                customWaveViewer1.WaveStream = waveStream;
                            });

                            ctxMenu.Items.Add(new ToolStripSeparator());
                            ctxMenu.Items.Add("Override...", null, (a, b) =>
                            {
                                Console.WriteLine("Overriding");
                            });

                            for (int channelIndex = 0; channelIndex < item.binaryWave.Channels.Length; channelIndex++)
                            {
                                var channel = item.binaryWave.Channels[channelIndex];
                                AddNode(bwavNode.Nodes, $"Channel #{channelIndex}", () =>
                                {
                                    itemPropertyGrid.SelectedObject = channel;
                                });
                            }


                        }
                        break;
                    }

                case "BWAV":
                    {
                        reader.Position = 0;
                        var buffer = reader.ReadByteArray((int)stream.Length);
                        var bwav = new BinaryWaveFile(buffer);

                        var name = Path.GetFileNameWithoutExtension(fileName);
                        var nodeItem = barsTreeView.Nodes.Add(name);
                        var bwavNode = AddNode(nodeItem.Nodes, "BWAV", () =>
                        {
                            itemPropertyGrid.SelectedObject = bwav;
                        });


                        var ctxMenu = new ContextMenuStrip();
                        bwavNode.ContextMenuStrip = ctxMenu;
                        ctxMenu.Items.Add("Export as WAV...", null, (_, _) =>
                        {
                            short[] pcm = GetPcm(bwav);

                            using var saveFileDialog = new SaveFileDialog
                            {
                                Filter = "Waveform (*.wav)|*.wav",
                                FilterIndex = 1,
                                RestoreDirectory = true,
                                OverwritePrompt = true,
                                FileName = name + ".wav",
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

                            //var waveOut = new WaveOutEvent();
                            if (audioPlayer.PlaybackState == PlaybackState.Playing)
                                audioPlayer.Stop();

                            audioPlayer.Init(waveStream);
                            audioPlayer.Play();

                            customWaveViewer1.WaveStream = waveStream;
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

        private void TreeView_OnNodeSelection(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (TreeNodeActions.TryGetValue(e.Node, out Action onNodeSelected))
                onNodeSelected();
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

        private void darkButton1_Click(object sender, EventArgs e)
        {
            if (audioPlayer != null)
            {
                var isPlaying = audioPlayer.PlaybackState == PlaybackState.Playing;
                if (isPlaying)
                {
                    audioPlayer.Pause();
                 
                }
                else
                {
                    audioPlayer.Play();
   
                }
            }
        }
    }
}