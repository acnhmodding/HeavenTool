using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DarkContextMenuStrip = HeavenTool.Forms.Components.DarkContextMenuStrip;
using HeavenTool.Forms.PBC;
using HeavenTool.IO;
using AeonSake.NintendoTools.FileFormats;
using AeonSake.NintendoTools.Compression.Zstd;
using AeonSake.NintendoTools.FileFormats.Sarc;

namespace HeavenTool.Forms.SARC;

public partial class SarcEditor : Form
{
    public SarcEditor()
    {
        InitializeComponent();

        // probably I should find a better way of initialzing this
        if (!isSarcInitialized)
        {
            var alignmentTable = new AlignmentTable()
            {
                Default = 0x08,
            };

            (string, int)[] extensionAlignments = [
                (".bgenv", 0x04),
                (".bfcpx", 0x10),
                (".bflan", 0x10),
                (".bflyt", 0x10),
                (".bushvt", 0x10),
                (".glsl", 0x10),
                (".byml", 0x20),
                (".pbc", 0x80),
                (".belnk", 0x100),
                (".msbt", 0x100),
                (".barslist", 0x100),
                (".bnsh", 0x1000),
                (".bntx", 0x1000),
                (".sharcb", 0x1000),
                (".arc", 0x2000),
                (".baglmf", 0x2000),
                (".bffnt", 0x2000),
                (".bfotf", 0x2000),
                (".bfres", 0x2000),
                (".bfsha", 0x2000),
                (".bfttf", 0x2000),
                (".bphcw", 0x2000),
                (".bphlik", 0x2000),
                (".genvb", 0x2000),
                (".genvres", 0x2000),
                (".phive", 0x2000),
                (".ptcl", 0x4000)
            ];

            foreach (var (extension, alignment) in extensionAlignments)
                alignmentTable.Add(extension, alignment);

            isSarcInitialized = true;
            SarcCompiler.Alignment = alignmentTable;
        }
    }

    private static bool isSarcInitialized = false;
    private static readonly ZstdCompressor Compressor = new();
    private static readonly ZstdDecompressor Decompressor = new();
    private static readonly SarcFileReader SarcFileParser = new();
    private static readonly SarcFileWriter SarcCompiler = new();

    private string loadedFileName = "";
    private SarcFile? loadedFile;
    private Dictionary<SarcContent, TreeNode>? nodes;
    private List<Form>? openedEditors;

    private bool _isDirty;
    private bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
                _isDirty = value;

            if (loadedFile != null)
                Text = $"SARC Editor: {loadedFileName}{(_isDirty ? "*" : "")}";
            else
                Text = "SARC Editor";
        }
    }

    private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (IsDirty || (openedEditors != null && openedEditors.Count > 0))
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

            using var file = File.OpenRead(path);
            using var fileStream = new MemoryStream();

            // Check for compressor
            if (Decompressor.CanDecompress(file))
                Decompressor.Decompress(file, fileStream);
            else file.CopyTo(fileStream);

            fileStream.Position = 0;
            if (fileStream.Length == 0) throw new Exception("Failed to open SARC file!");

            filesTreeView.Nodes.Clear();

            loadedFileName = Path.GetFileName(path);
            try
            {
                loadedFile = SarcFileParser.Read(fileStream);
            } 
            catch(InvalidDataException)
            {
                MessageBox.Show($"This is not a SARC file! ({fileStream.ReadString(4, Encoding.UTF8)})", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            if (openedEditors != null && openedEditors.Count > 0)
                foreach (var editor in openedEditors)
                    editor.Close();

            openedEditors = [];
            nodes = [];
            IsDirty = false;

            foreach (var sarcContent in loadedFile.Files)
            {
                var treeNode = new TreeNode(sarcContent.Name);
                filesTreeView.Nodes.Add(treeNode);
                var context = new DarkContextMenuStrip();

                if (sarcContent.Name.EndsWith(".pbc"))
                {
                    ToolStripItem item = context.Items.Add("Open with PBC Editor");
                    item.Click += (_, _) =>
                    {
                        void saveFunction(byte[] bytes)
                        {
                            IsDirty = true;
                            sarcContent.Data = bytes;
                            treeNode.Text = $"{sarcContent.Name}*";
                        }

                        var editor = new PBCEditor(sarcContent.Data, sarcContent.Name, saveFunction);
                        editor.Show();

                        openedEditors.Add(editor);
                        item.Enabled = false;

                        editor.FormClosed += (_, _) =>
                        {
                            item.Enabled = true;
                        };
                    };
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
                treeNode.ContextMenuStrip = context;
               
                nodes[sarcContent] = treeNode;
            }

            filesTreeView.Invalidate();
            fileStream.Close();
        }
    }

    private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (loadedFile == null) return;

        var memoryStream = new MemoryStream();

        SarcCompiler.Write(loadedFile, memoryStream);

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
            FileName = isCompressed ? $"{loadedFileName}.Nin_NX_NVN.zs" : loadedFileName
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            var path = saveFileDialog.FileName;
            File.WriteAllBytes(path, memoryStream.ToArray());
        }

        IsDirty = false;

        // Remove Dirty Asterisk
        if (nodes != null)
            foreach (var (sarcConcent, node) in nodes)
                node.Text = sarcConcent.Name;
    }
}