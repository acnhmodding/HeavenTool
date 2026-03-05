using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.Forms.Search;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.ResourceSizeTable;
using HeavenTool.Properties;
using HeavenTool.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeavenTool.Forms.RSTB;

public partial class RSTBEditor : Form, ISearchable
{
    private string OriginalText { get; }

    private SearchBox? searchBox;
    public RSTBEditor()
    {
        InitializeComponent();

        Text = $"Heaven Tool | {Program.VERSION} | RSTB Editor";
        OriginalText = Text;

       
        if (TryGetColumn("FileName", out var fileNameColumn))
            fileNameColumn.ValueType = typeof(string);

        if (TryGetColumn("FileSize", out var fileSizeColumn))
            fileSizeColumn.ValueType = typeof(uint);

        if (TryGetColumn("DLC", out var dlcColumn))
            dlcColumn.ValueType = typeof(uint);

        dataGrid.ShowCellToolTips = false;
        dataGrid.VirtualMode = true;
        dataGrid.CellValueNeeded += DataGrid_CellValueNeeded;

        statusProgressBar.Visible = false;
        statusBar.Visible = false;

        RefreshData();

        //DoubleBuffered = true;
        associateRstbToolStripMenuItem.Checked = ProgramAssociation.GetAssociatedProgram(".srsizetable") == Application.ExecutablePath;
    }

    private bool TryGetColumn(string columnName, [MaybeNullWhen(false)] out DataGridViewColumn column)
    {
        column = null;

        if (dataGrid.Columns.Contains(columnName) && dataGrid.Columns[columnName] is DataGridViewColumn c)
        {
            column = c;
            return true;
        }
        else return false;
    }


    private void DataGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (LoadedFile == null) return;

        if (e.RowIndex < 0 || e.RowIndex >= LoadedFile.Length)
            return;

        var entry = LoadedFile[e.RowIndex];

        switch (dataGrid.Columns[e.ColumnIndex].Name)
        {
            case "FileName":
                e.Value = entry.FileName;
                break;

            case "FileSize":
                e.Value = entry.FileSize;
                break;

            case "DLC":
                e.Value = entry.DLC.HasValue ? entry.DLC.Value.ToString() : "";
                break;

            case "Diff":
                e.Value = DiffDictionary.TryGetValue(e.RowIndex, out long value) ? value : 0;
                break;
        }
    }

    public ConcurrentDictionary<int, long> DiffDictionary = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ResourceSizeTable? LoadedFile { get; set; }

    private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Select a .srsizetable file",
            Filter = "RSTB (*.srsizetable)|*.srsizetable",
            DefaultExt = "*.srsizetable",
            FilterIndex = 1,
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
            LoadFile(openFileDialog.FileName);
    }

    public void LoadFile(string path)
    {
        if (!File.Exists(path)) return;

        using var file = File.OpenRead(path);
        LoadedFile = new ResourceSizeTable(file.ToArray());

        if (LoadedFile == null || !LoadedFile.IsLoaded)
        {
            MessageBox.Show("Failed to load this file!");
            return;
        }

        RefreshData();
    }

    private void RefreshData()
    {
        saveAsButton.Enabled = LoadedFile?.IsLoaded == true;
        updateFromModdedRomFs.Enabled = LoadedFile?.IsLoaded == true;
        closeFileButton.Enabled = LoadedFile?.IsLoaded == true;

        if (LoadedFile?.IsLoaded != true)
        {
            dataGrid.RowCount = 0;
            return;
        }

        var uniqueEntries = LoadedFile.Dictionary.Values.Where(x => !x.IsCollided).ToList();
        var nonUniqueEntries = LoadedFile.Dictionary.Values.Where(x => x.IsCollided).ToList();
        dataGrid.RowCount = LoadedFile.Length;

        Text = $"{OriginalText}: {LoadedFile.Length} ({LoadedFile.UniqueEntries.Count}/{LoadedFile.NonUniqueEntries.Count})";
    }

    private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null) return;

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "RSTB/RSTC (*.srsizetable)|*.srsizetable",
            FilterIndex = 1,
            RestoreDirectory = true,
            OverwritePrompt = true
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            var file = LoadedFile.Save();
            if (file != null)
                File.WriteAllBytes(saveFileDialog.FileName, file);
        }
    }

    private void UpdateFromModdedRomFs_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null || !LoadedFile.IsLoaded) return;

        using var folderBrowserDialog = new FolderBrowserDialog()
        {
            Description = "Select your RomFs"
        };

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            CreateUpdatedRSTBFromModdedRomFs(LoadedFile, folderBrowserDialog.SelectedPath);

            TopMenu.Enabled = true;
            statusBar.Visible = false;
            statusProgressBar.Visible = false;

            RefreshData();
        }
    }

    public async void CreateUpdatedRSTBFromModdedRomFs(ResourceSizeTable rstb, string moddedRomFsPath, bool showResults = true)
    {
        var allFiles = Directory.GetFiles(moddedRomFsPath, "*", SearchOption.AllDirectories);

        int changedFiles = 0;
        int addedFiles = 0;
        int skippedFiles = 0;
        int removedFiles = 0;

        TopMenu.Enabled = false;

        statusLabel.Text = $"";
        statusBar.Visible = true;
        statusProgressBar.Visible = true;
        statusProgressBar.Maximum = allFiles.Length;
        statusProgressBar.Value = 0;

        var progress = new Progress<(int Index, string FileName)>(value =>
        {
            statusLabel.Text = $"Getting file size... {value.FileName} ({value.Index}/{allFiles.Length})";
            statusProgressBar.Value = value.Index;
        });

        await Task.Run(() =>
        {
            int currentPosition = 1;

            foreach (var originalFile in allFiles)
            {
                if (IsDisposed || Disposing) break;

                // TODO: Update this based on HeavenTool.ModManager
                var path = Path.GetRelativePath(moddedRomFsPath, originalFile).Replace('\\', '/');
                if (path == "System/Resource/ResourceSizeTable.srsizetable" || path == "System/Resource/ResourceSizeTable.rsizetable")
                {
                    skippedFiles++;
                    continue;
                }

                if (path.EndsWith(".byml") && path != "EventFlow/Info/EventFlowInfoProduct.byml")
                    continue;

                // Report progress to our progress bar
                (progress as IProgress<(int, string)>).Report((currentPosition, path));

                if (path.EndsWith(".zs"))
                    path = path[..^3];

                var fileSize = ResourceSizeTable.GetFileSize(originalFile, path);

                if (fileSize < 0 || fileSize > uint.MaxValue)
                {
                    rstb.Dictionary.Remove(path);
                    removedFiles++;
                }
                else if (rstb.Dictionary.TryGetValue(path, out var entry) && fileSize != entry.FileSize)
                {
                    entry.FileSize = (uint)fileSize;
                    changedFiles++;
                }
                else if (!rstb.Dictionary.ContainsKey(path))
                {
                    rstb.AddEntry(new ResourceSizeTable.ResourceTableEntry(path, (uint)fileSize, 0, false));
                    addedFiles++;
                }

                currentPosition++;
            }

            if (IsDisposed || Disposing) return;

        });

        
        if (showResults && (changedFiles > 0 || addedFiles > 0 || removedFiles > 0))
        {
            MessageBox.Show($"Successfully updated table values!" +
                            (changedFiles > 0 ? $"\nUpdated {changedFiles} entries." : "") +
                            (addedFiles > 0 ? $"\nAdded {addedFiles} entries." : "") +
                            (skippedFiles > 0 ? $"\nSkipped {skippedFiles} entries." : "") +
                            (removedFiles > 0 ? $"\nRemoved {removedFiles} entries." : "") +
                            "\n\nYou need to manually save your file in File > Save as...",
                "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
    }

    private void UpdateHashListToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var choose = MessageBox.Show("This action need the entire RomFs dump! (Including game-updates and DLC)\nIf you don't have all these files CANCEL the operation.", "Attention", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (choose == DialogResult.Cancel) return;

        using var openFolderDialog = new FolderBrowserDialog()
        {
            ShowNewFolderButton = false,
            Description = "Select a RomFs directory",
            SelectedPath = Settings.Default.LastSelectedRomFsDirectory ?? ""
        };

        if (openFolderDialog.ShowDialog() == DialogResult.OK)
        {
            var selectedPath = openFolderDialog.SelectedPath;
            Settings.Default.LastSelectedRomFsDirectory = selectedPath;
            Settings.Default.Save();

            var files = Directory.GetFiles(selectedPath, "*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".srsizetable")).ToArray();

            files = [.. files.Select(x =>
            {
                var text = Path.GetRelativePath(selectedPath, x).Replace('\\', '/');

                if (text.EndsWith(".zs"))
                    text = text[..^3];

                return text;
            })];

            // Save
            RomFsNameManager.Update(files);
        }
    }

    private void CloseFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        LoadedFile?.Dispose();
        LoadedFile = null;

        if (dataGrid.Columns.Contains("Diff"))
            dataGrid.Columns.Remove("Diff");

        Text = OriginalText;

        RefreshData();
    }

    private void AssociateSrsizetableToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var exePath = Application.ExecutablePath;
        var arguments = $"--{(associateRstbToolStripMenuItem.Checked ? "disassociate" : "associate")} srsizetable";


        var process = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            Verb = "runas",
            UseShellExecute = true
        });

        process?.WaitForExit();
        associateRstbToolStripMenuItem.Checked = ProgramAssociation.GetAssociatedProgram(".srsizetable") == Application.ExecutablePath;
    }

    private void AddNewEntriesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        addNewEntriesToolStripMenuItem.Checked = !addNewEntriesToolStripMenuItem.Checked;
    }

    public void SearchClosing()
    {
        if (oldColorCache != null)
        {
            foreach (var cache in oldColorCache)
                cache.Key.Style.BackColor = cache.Value;

            oldColorCache.Clear();
            oldColorCache = null;
        }
    }
    internal void ClearSearchCache()
    {
        if (searchCache != null && searchCache.Length > 0)
        {
            SearchClosing();

            lastSearchCell = null;
            searchCache = null;
        }
    }

    public void Search(string search, SearchType searchType, bool searchBackwards, bool caseSensitive)
    {
        if (lastSearch != search || lastSearchType != searchType || lastCaseSensitive != caseSensitive)
        {
            lastSearch = search;
            lastSearchType = searchType;
            lastCaseSensitive = caseSensitive;

            ClearSearchCache();
        }

        // Create cache if doesn't exist
        oldColorCache ??= [];

        if (searchCache == null || searchCache.Length == 0)
        {
            var rows = dataGrid.Rows.Cast<DataGridViewRow>();
            var cells = rows.SelectMany(x => x.Cells.Cast<DataGridViewCell>())
                .Where(cell =>
                {
                    var formattedValue = cell.FormattedValue?.ToString();

                    // Make sure the cell value can be formatted to a string, if not skip it
                    if (formattedValue == null) return false;

                    if (!caseSensitive)
                    {
                        formattedValue = formattedValue.ToLower();
                        search = search.ToLower();
                    }

                    return (searchType == SearchType.Contains && formattedValue.Contains(search)) || (searchType == SearchType.Exactly && formattedValue == search);
                });

            searchCache = [.. cells];

            if (searchCache.Length > 1 && searchBackwards)
                currentSearchIndex = searchCache.Length - 1;
            else
                currentSearchIndex = 0;

            searchBox?.UpdateMatchesFound(searchCache.Length, currentSearchIndex);

            if (searchCache.Length == 0)
            {
                MessageBox.Show("No matches found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else
            {
                foreach (var current in searchCache)
                {
                    if (!oldColorCache.ContainsKey(current) && current.Style.BackColor != HIGHLIGHT_COLOR && current.Style.BackColor != HIGHLIGHT_COLOR_CURRENT)
                        oldColorCache.Add(current, current.Style.BackColor);

                    current.Style.BackColor = HIGHLIGHT_COLOR;
                }
            }

        }


        // If its out of bounds, return to start (in case o backwards return to the last one)
        if (currentSearchIndex >= searchCache.Length || currentSearchIndex < 0)
        {
            currentSearchIndex = searchBackwards ? searchCache.Length - 1 : 0;
        }

        // Safe check, don't should occur but checking again doesn't hurt
        if (searchCache.Length > 0 && currentSearchIndex < searchCache.Length && currentSearchIndex >= 0)
        {
            if (lastSearchCell != null)
            {
                if (!oldColorCache.ContainsKey(lastSearchCell) && lastSearchCell.Style.BackColor != HIGHLIGHT_COLOR && lastSearchCell.Style.BackColor != HIGHLIGHT_COLOR_CURRENT)
                    oldColorCache.Add(lastSearchCell, lastSearchCell.Style.BackColor);

                lastSearchCell.Style.BackColor = HIGHLIGHT_COLOR;
            }

            searchBox?.UpdateMatchesFound(searchCache.Length, currentSearchIndex);

            var current = searchCache[currentSearchIndex];

            dataGrid.FirstDisplayedScrollingColumnIndex = current.ColumnIndex;
            dataGrid.FirstDisplayedScrollingRowIndex = current.RowIndex;

            lastSearchCell = current;

            if (!oldColorCache.ContainsKey(lastSearchCell) && lastSearchCell.Style.BackColor != HIGHLIGHT_COLOR && lastSearchCell.Style.BackColor != HIGHLIGHT_COLOR_CURRENT)
                oldColorCache.Add(lastSearchCell, lastSearchCell.Style.BackColor);

            lastSearchCell.Style.BackColor = Color.YellowGreen;

            if (searchBackwards)
            {
                if (currentSearchIndex == 0)
                    currentSearchIndex = searchCache.Length - 1;
                else
                    currentSearchIndex--;
            }
            else
            {
                if (currentSearchIndex >= searchCache.Length)
                    currentSearchIndex = 0;
                else
                    currentSearchIndex++;
            }

        }
    }

    private void SearchToolStripMenuItem_Click(object sender, EventArgs e)
    {
        // Create SearchBox Window if needed
        searchBox ??= new SearchBox(this)
        {
            StartPosition = FormStartPosition.CenterParent
        };

        searchBox.Show();

        RestoreSearchCache();
    }

    private string lastSearch = "";
    private bool lastCaseSensitive = false;
    private SearchType lastSearchType = SearchType.Contains;
    private int currentSearchIndex = -1;
    private DataGridViewCell[]? searchCache;
    private DataGridViewCell? lastSearchCell;
    private Dictionary<DataGridViewCell, Color>? oldColorCache = null;

    public static readonly Color HIGHLIGHT_COLOR = Color.FromArgb(180, 180, 10);
    public static readonly Color HIGHLIGHT_COLOR_CURRENT = Color.YellowGreen;

    private void RestoreSearchCache()
    {
        // Create cache if doesn't exist
        oldColorCache ??= [];

        if (searchCache != null)
        {
            foreach (var cell in searchCache)
            {
                if (!oldColorCache.ContainsKey(cell) && cell.Style.BackColor != HIGHLIGHT_COLOR && cell.Style.BackColor != HIGHLIGHT_COLOR_CURRENT)
                    oldColorCache.Add(cell, cell.Style.BackColor);

                cell.Style.BackColor = cell == lastSearchCell ? HIGHLIGHT_COLOR_CURRENT : HIGHLIGHT_COLOR;
            }
        }
    }

    private async void CompareDifferenceToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null) return;

        using var openFolderDialog = new FolderBrowserDialog()
        {
            ShowNewFolderButton = false,
            Description = "Select a RomFs directory",
            SelectedPath = Settings.Default.LastSelectedRomFsDirectory ?? ""
        };

        if (openFolderDialog.ShowDialog() != DialogResult.OK)
            return;

        var selectedPath = openFolderDialog.SelectedPath;

        if (string.IsNullOrEmpty(selectedPath) || !Directory.Exists(selectedPath))
            return;

        if (!dataGrid.Columns.Contains("Diff"))
        {
            var column = dataGrid.Columns.Add("Diff", "Diff");
            dataGrid.Columns[column].ValueType = typeof(long);
        }

        statusBar.Visible = true;
        statusProgressBar.Visible = true;
        statusProgressBar.Maximum = dataGrid.RowCount;
        statusProgressBar.Value = 0;

        var rowCount = LoadedFile.Length;
        var rowsData = new (int Index, string? FileName, uint FileSize)[rowCount];
        
        for (int i = 0; i < rowCount; i++)
        {
            var row = LoadedFile[i];

            rowsData[i] = (i, row.FileName, row.FileSize);
        }

        var rowsToColor = new ConcurrentBag<int>();

        int processed = 0;

        await Task.Run(() =>
        {
            Parallel.ForEach(rowsData, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            },
            row =>
            {
                if (string.IsNullOrEmpty(row.FileName)) return;

                var actualPath = Path.Combine(selectedPath, row.FileName);

                if (!File.Exists(actualPath) && File.Exists(actualPath + ".zs"))
                    actualPath += ".zs";

                long actualValue = -1;

   
                var generatedFileSize = ResourceSizeTable.GetFileSize(actualPath, row.FileName);
                actualValue = generatedFileSize;

                if (row.FileSize is uint fileSize)
                {
                    if (generatedFileSize != fileSize)
                        rowsToColor.Add(row.Index);
                }

                DiffDictionary[row.Index] = actualValue;

                int current = Interlocked.Increment(ref processed);

                if (current % 50 == 0)
                {
                    BeginInvoke(() =>
                    {
                        statusProgressBar.Value = current;
                        statusLabel.Text = $"Processing {current}/{rowsData.Length}";
                    });
                }
            });
        });

        foreach (var index in rowsToColor)
        {
            dataGrid.Rows[index].DefaultCellStyle.BackColor = Color.Red;
        }

        statusProgressBar.Value = rowsData.Length;
        statusLabel.Text = $"Completed ({rowsData.Length}/{rowsData.Length})";

        dataGrid.Invalidate();

        statusBar.Visible = false;
        statusProgressBar.Visible = false;
    }


    private void MainDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {

    }

    private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
    {
        StringBuilder sb = new();


        var rows = dataGrid.SelectedRows.Cast<DataGridViewRow>().OrderBy(x => x.Index);
        foreach (var row in rows)
        {
            var text = string.Join(";", row.Cells.Cast<DataGridViewCell>().Where(y => y.OwningColumn != null && y.OwningColumn.Name != "DLC").Select(x => x.FormattedValue?.ToString()));
            sb.AppendLine(text);
        }
        

        Clipboard.SetText(sb.ToString());
    }
}
