using HeavenTool.Forms.BARS;
using HeavenTool.Forms.Pack;
using HeavenTool.Forms.RSTB;
using HeavenTool.Forms.SARC;
using HeavenTool.IO;
using HeavenTool.IO.Compression;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool;

public partial class HeavenMain : Form
{
    public HeavenMain()
    {
        InitializeComponent();

        Text = $"Heaven Tool {Program.VERSION}";
    }

    // Forms

    private void BcsvEditorButton_Click(object sender, EventArgs e)
    {
        var bcsvEditor = new BCSVForm();

        bcsvEditor.Show();
        bcsvEditor.BringToFront();
    }

    private void RstbEditorButton_Click(object sender, EventArgs e)
    {
        var rstbEditor = new RSTBEditor();

        rstbEditor.Show();
        rstbEditor.BringToFront();
    }

    private void SarcEditorButton_Click(object sender, EventArgs e)
    {
        var sarcEditor = new SarcEditor();

        sarcEditor.Show();
        sarcEditor.BringToFront();
    }

    private void ItemParamHelperButton_Click(object sender, EventArgs e)
    {
        var itemIDHelper = new ItemIDHelper();

        itemIDHelper.Show();
        itemIDHelper.BringToFront();
    }

    private void BcsvReworkButton_Click(object sender, EventArgs e)
    {
        var bcsvRework = new BCSVForm();

        bcsvRework.Show();
        bcsvRework.BringToFront();
    }

    private void Yaz0DecompressToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog()
        {
            Multiselect = false
        };

        var selectedFile = openFileDialog.ShowDialog();

        if (selectedFile == DialogResult.OK
            && !string.IsNullOrEmpty(openFileDialog.FileName)
            && File.Exists(openFileDialog.FileName))
        {
            using var fileStream = File.OpenRead(openFileDialog.FileName);
            //MemoryStream memoryStream = new();

            byte[] decompressedBytes = Yaz0CompressionAlgorithm.Decompress(fileStream)?.ToArray();


            if (decompressedBytes == null) return;

            var saveFileDialog = new SaveFileDialog()
            {
                FileName = openFileDialog.FileName,
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                var savePath = saveFileDialog.FileName;
                File.WriteAllBytes(savePath, decompressedBytes);
            }
        }
    }

    private void ExportLabelsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var folderBrowserDialog = new FolderBrowserDialog();

        var result = folderBrowserDialog.ShowDialog();

        if (result == DialogResult.OK)
        {
            var path = folderBrowserDialog.SelectedPath;
            var outputDirectory = Path.Combine(path, "output");

            if (Directory.Exists(path))
            {
                Directory.CreateDirectory(outputDirectory);
                foreach (var file in Directory.GetFiles(path))
                {
                    if (Path.GetExtension(file) != ".bcsv") continue;

                    var outputFile = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(file)}-values.txt");

                    using var stream = File.OpenRead(file);
                    var bcsvFile = new BinaryCSV(stream.ToArray());
                    var list = new List<string>();

                    if (bcsvFile.Fields.Any(x => x.GetTranslatedNameOrHash().StartsWith("Label string")))
                    {
                        var fieldId = bcsvFile.Fields.First(x => x.GetTranslatedNameOrHash().StartsWith("Label string"));

                        for (int i = 0; i < bcsvFile.Entries.Count; i++)
                            list.Add(bcsvFile[i, fieldId].ToString());

                        File.WriteAllLines(outputFile, list);
                    }
                }

                Process.Start(outputDirectory);
            }
        }
    }

    private void ExportEnumsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var folderBrowserDialog = new FolderBrowserDialog();

        var result = folderBrowserDialog.ShowDialog();

        if (result == DialogResult.OK)
        {
            var path = folderBrowserDialog.SelectedPath;
            var outputDirectory = Path.Combine(path, "enum-output");

            if (Directory.Exists(path))
            {
                Directory.CreateDirectory(outputDirectory);
                foreach (var file in Directory.GetFiles(path))
                {
                    if (Path.GetExtension(file) != ".bcsv") continue;

                    using var stream = File.OpenRead(file);
                    var bcsvFile = new BinaryCSV(stream.ToArray());

                    var hashedFields = bcsvFile.Fields.Where(x => x.DataType == DataType.CRC32).ToList();
                    if (bcsvFile.Entries.Count > 0 && hashedFields.Count > 0)
                    {
                        //var directoryPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file));
                        //Directory.CreateDirectory(directoryPath);
                        Directory.CreateDirectory(outputDirectory);

                        foreach (var field in hashedFields)
                        {
                            var outputFile = Path.Combine(outputDirectory, $"{field.HEX}.txt");
                            //var outputFile = Path.Combine(directoryPath, $"{field.HEX}.txt");
                            var parsedList = new List<uint>();
                            var registers = new List<string>();

                            if (File.Exists(outputFile))
                                registers = [.. File.ReadAllLines(outputFile)];

                            var fieldIndex = Array.IndexOf(bcsvFile.Fields, field);

                            foreach (var entry in bcsvFile.Entries)
                            {
                                if (entry[fieldIndex] is uint fieldHash && !parsedList.Contains(fieldHash))
                                {
                                    if (HashManager.GetHashTranslationOrNull(fieldHash) is string value && !registers.Contains(value))
                                        registers.Add(value);

                                    parsedList.Add(fieldHash);
                                }
                            }

                            File.WriteAllLines(outputFile, registers);
                        }
                    }
                }

                Process.Start(outputDirectory);
            }
        }
    }

    private void ExportUsedHashesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var folderBrowserDialog = new FolderBrowserDialog();

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            string dir = "Exported Hashes";
            Directory.CreateDirectory(dir);

            var usedHashesHeaders = new List<string>();
            var usedHashesValues = new List<string>();

            var filesPath = folderBrowserDialog.SelectedPath;
            foreach (var file in Directory.GetFiles(filesPath))
            {
                if (Path.GetExtension(file) != ".bcsv")
                    continue;

                using var stream = File.OpenRead(file);
                var bcsvFile = new BinaryCSV(stream.ToArray());

                foreach (var item in bcsvFile.Fields)
                    if (HashManager.GetHashTranslationOrNull(item.Hash) is string hashName && !usedHashesHeaders.Contains(hashName))
                        usedHashesHeaders.Add(hashName);


                foreach (var entry in bcsvFile.Entries)
                    foreach (var entryField in entry)
                        if (entryField is uint hashValue && (hashValue > 100000))
                        {
                            if (HashManager.GetHashTranslationOrNull(hashValue) is string hashName && !usedHashesValues.Contains(hashName))
                                usedHashesValues.Add(hashName);
                        }


            }

            File.WriteAllLines(Path.Combine(dir, "headers.txt"), usedHashesHeaders);
            File.WriteAllLines(Path.Combine(dir, "values.txt"), usedHashesValues);

            // Open directory
            Process.Start(dir);
        }
    }

    private void BarsEditorButton_Click(object sender, EventArgs e)
    {
        var barsWindow = new BARSWindow();

        barsWindow.Show();
    }

    private void findBCSVToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var folderBrowserDialog = new FolderBrowserDialog();

        var result = folderBrowserDialog.ShowDialog();

        if (result == DialogResult.OK)
        {
            var path = folderBrowserDialog.SelectedPath;
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    if (Path.GetExtension(file) != ".bcsv") continue;

                    using var stream = File.OpenRead(file);
                    var bcsvFile = new BinaryCSV(stream.ToArray());

                    if (bcsvFile.Entries.Count > 10 && bcsvFile.Entries.Count < 13)
                    {
                        Console.WriteLine($"Possible bcsv: {file}");
                    }
                }
            }

        }

    }

    private void yaz0CompressToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog()
        {
            Multiselect = false
        };

        var selectedFile = openFileDialog.ShowDialog();

        if (selectedFile == DialogResult.OK
            && !string.IsNullOrEmpty(openFileDialog.FileName)
            && File.Exists(openFileDialog.FileName))
        {
            using var fileStream = File.OpenRead(openFileDialog.FileName);

            byte[] compressedBytes = Yaz0CompressionAlgorithm.Compress(fileStream)?.ToArray();

            if (compressedBytes == null || compressedBytes.Length == 0) return;

            var saveFileDialog = new SaveFileDialog()
            {
                FileName = openFileDialog.FileName,
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                var savePath = saveFileDialog.FileName;
                File.WriteAllBytes(savePath, compressedBytes);
            }
        }
    }
}
