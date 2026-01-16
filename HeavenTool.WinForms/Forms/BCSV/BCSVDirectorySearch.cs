using AltUI.Controls;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms;

public partial class BCSVDirectorySearch : Form
{
    public BCSVDirectorySearch()
    {
        InitializeComponent();
    }

    private void SelectDirectoryButton_Click(object sender, EventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();

        var result = folderBrowserDialog.ShowDialog();

        if (result == DialogResult.OK)
            directoryPath.Text = folderBrowserDialog.SelectedPath;

    }

    private void SearchButton_Click(object sender, EventArgs e)
    {
        searchButton.Enabled = false;
        if (Directory.Exists(directoryPath.Text))
        {
            
            try
            {
                foreach (var file in Directory.GetFiles(directoryPath.Text))
                {
                    if (Path.GetExtension(file) != ".bcsv")
                        continue;

                    ReadBCSVFileAndSearch(file);
                }
            }
            catch (Exception ex) {
                MessageBox.Show("Error while searching:\n" + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } 
        }

        searchButton.Enabled = true;
    }


    private void ReadBCSVFileAndSearch(string path)
    {
        if (!File.Exists(path))
            return;

        using var stream = File.OpenRead(path);
        var bcsvFile = new BinaryCSV(stream.ToArray());

        foreach (var header in bcsvFile.Fields)
        {
            var name = header.GetTranslatedNameOrHash();
            if ((containButton.Checked && name.Contains(searchField.Text, StringComparison.CurrentCultureIgnoreCase)) || name == searchField.Text)
            {
                var key = Path.GetFileNameWithoutExtension(path);
                if (!foundHits.Nodes.Any(x => x.Text == key))
                    foundHits.Nodes.Add(new DarkTreeNode(key));

                var node = foundHits.Nodes.FirstOrDefault(x => x.Text == key);
                node?.Nodes.Add(new DarkTreeNode($"Header: {name}"));
            }
        }
        
        foreach(var (entry, index) in bcsvFile.Entries.Select((x, y) => (x, y)))
        {
            for (int fieldIndex = 0; fieldIndex < entry.Length; fieldIndex++)
            {
                object item = entry[fieldIndex];
                var value = item.ToString();
                if ((containButton.Checked && value.Contains(searchField.Text, StringComparison.CurrentCultureIgnoreCase)) || value == searchField.Text)
                {
                    var key = Path.GetFileNameWithoutExtension(path);
                    if (!foundHits.Nodes.Any(x => x.Text == key))
                        foundHits.Nodes.Add(new DarkTreeNode(key));

                    var node = foundHits.Nodes.FirstOrDefault(x => x.Text == key);
                    var entryField = bcsvFile.Fields[fieldIndex];
                    if (entryField != null)
                        node?.Nodes.Add(new DarkTreeNode($"Entry: {index} | Header: {entryField.GetTranslatedNameOrHash()} | {value} (value)"));
                    else 
                        node?.Nodes.Add(new DarkTreeNode($"Entry: {index} | {value} (value)"));
                }
            }
        }

        bcsvFile = null;
    }
}
