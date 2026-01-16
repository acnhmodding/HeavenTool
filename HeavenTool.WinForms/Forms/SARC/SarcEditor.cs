using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DarkContextMenuStrip = HeavenTool.Forms.Components.DarkContextMenuStrip;
using HeavenTool.Forms.PBC;
using HeavenTool.IO;
using AeonSake.NintendoTools.Compression.Zstd;
using AeonSake.NintendoTools.FileFormats.Sarc;
using AltUI.Controls;

namespace HeavenTool.Forms.SARC;

public partial class SarcEditor : Form
{
    public SarcEditor()
    {
        InitializeComponent();
    }

    private static readonly ZstdCompressor Compressor = new();
    private static readonly ZstdDecompressor Decompressor = new();
    private static readonly SarcFileReader SarcFileParser = new();
    private static readonly SarcFileWriter SarcCompiler = new();

    private string LoadedFileName { get; set; }
    private SarcFile LoadedFile;

    private Dictionary<SarcContent, DarkTreeNode> Nodes;
    private List<SarcContent> OpenedFiles;
    private List<Form> OpenedEditors;

    private bool _isDirty;
    private bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
                _isDirty = value;

            if (LoadedFile != null)
                Text = $"SARC Editor: {LoadedFileName}{(_isDirty ? "*" : "")}";
            else
                Text = "SARC Editor";
        }
    }

    private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (IsDirty || (OpenedEditors != null && OpenedEditors.Count > 0))
        {
            var result = MessageBox.Show("Do you really want to open a new file?\n\nAll current editors will be closed and you'll lose any non-saved progress!", "Open a new file?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
                return;
        }

        var openFileDialog = new OpenFileDialog()
        {
            Title = "Open a SARC file",
            CheckFileExists = true
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            var path = openFileDialog.FileName;

            Stream file = File.OpenRead(path);
            MemoryStream fileStream = new();

            file.CopyTo(fileStream);

            var isDecompressed = fileStream.ReadString(4, Encoding.ASCII) == "SARC";
            fileStream.Position = 0;

            // Check for compressor
            if (!isDecompressed && Decompressor.CanDecompress(file))
                Decompressor.Decompress(file, fileStream);

            if (fileStream.Length == 0) throw new Exception("Failed to open SARC file!");

            filesTreeView.Nodes.Clear();

            LoadedFileName = Path.GetFileName(path);
            try
            {
                LoadedFile = SarcFileParser.Read(fileStream);
            } 
            catch(InvalidDataException)
            {
                MessageBox.Show("This is not a SARC file!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            OpenedFiles = [];

            if (OpenedEditors != null && OpenedEditors.Count > 0)
                foreach (var editor in OpenedEditors)
                    editor.Close();

            OpenedEditors = [];
            Nodes = [];
            IsDirty = false;

            foreach (var sarcContent in LoadedFile.Files)
            {
                var treeNode = new DarkTreeNode(sarcContent.Name);
                filesTreeView.Nodes.Add(treeNode);
                var context = new DarkContextMenuStrip();

                if (sarcContent.Name.EndsWith(".pbc"))
                {
                    ToolStripItem item = null;
                    item = context.Items.Add("Open with PBC Editor", null, (_, _) =>
                    {
                        void saveFunction(byte[] bytes)
                        {
                            IsDirty = true;
                            sarcContent.Data = bytes;
                            treeNode.Text = $"{sarcContent.Name}*";
                        }

                        var editor = new PBCEditor(sarcContent.Data, sarcContent.Name, saveFunction);
                        editor.Show();

                        OpenedFiles.Add(sarcContent);
                        OpenedEditors.Add(editor);
                        item.Enabled = false;

                        editor.FormClosed += (_, _) =>
                        {
                            item.Enabled = true;
                        };
                    });
                }

                context.Items.Add("Export Data...", null, (_, _) =>
                {
                    var saveFileDialog = new SaveFileDialog()
                    {
                        FileName = Path.GetFileName(sarcContent.Name),
                    };

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, sarcContent.Data);
                    }
                });

                context.Items.Add("Replace Data...", null, (_, _) =>
                {
                    var openFileDialog = new OpenFileDialog()
                    {
                        Title = "Select a file to replace"
                    };

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        var stream = File.OpenRead(openFileDialog.FileName);
                        sarcContent.Data = stream.ToArray();
                    }
                });
                //treeNode.ContextMenuStrip = context;
               
                Nodes[sarcContent] = treeNode;
            }

            filesTreeView.Invalidate();
            fileStream.Close();
        }


    }

    private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null) return;

        var memoryStream = new MemoryStream();

        SarcCompiler.Write(LoadedFile, memoryStream);

        var msg = MessageBox.Show("Do you want to compress with Zstd?", "ZSTD Compression", MessageBoxButtons.YesNo);
        bool isCompressed = false;

        if (msg == DialogResult.Yes)
        {
            var compressedStream = new MemoryStream();
            Compressor.Compress(memoryStream, compressedStream);

            // Make sure we are at start
            memoryStream.Position = 0;

            compressedStream.Position = 0;
            compressedStream.CopyTo(memoryStream);

            isCompressed = true;
        }

        var saveFileDialog = new SaveFileDialog()
        {
            Title = "Select where you want to save",
            FileName = isCompressed ? $"{LoadedFileName}.Nin_NX_NVN.zs" : LoadedFileName
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            var path = saveFileDialog.FileName;
            File.WriteAllBytes(path, memoryStream.ToArray());
        }

        IsDirty = false;

        // Remove Dirty Asterisk
        foreach (var (sarcConcent, node) in Nodes)
            node.Text = sarcConcent.Name;

    }

    private void searchTextBox_Click(object sender, EventArgs e)
    {

    }
}
