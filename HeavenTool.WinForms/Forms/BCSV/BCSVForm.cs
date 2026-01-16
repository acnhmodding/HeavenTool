using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HeavenTool.Forms;
using System.Diagnostics;
using HeavenTool.Forms.Search;
using HeavenTool.Forms.Components;
using HeavenTool.IO.FileFormats.BCSV;
using ProgramAssociation = HeavenTool.Utility.ProgramAssociation;
using HeavenTool.IO;
using HeavenTool.Forms.BCSV.Controls;
using HeavenTool.Forms.BCSV.Templates;
using HeavenTool.Forms.BCSV;
using System.Text.RegularExpressions;
using HeavenTool.Forms.BCSV.Controls.Entries;

namespace HeavenTool;

public partial class BCSVForm : Form, ISearchable
{
    private static readonly string originalFormName = "Heaven Tool - BCSV Editor";
    private BinaryCSV LoadedFile { get; set; }


    public BCSVForm()
    {
        InitializeComponent();

        // Dock dragInfo (cause having it docked by default makes everything hard when editing the form)
        dragInfo.Dock = DockStyle.Fill;
        dragInfo.AutoSize = false;

        ReloadInfo();

        // smooth scrolling
        DrawingControl.SetDoubleBuffered(mainDataGridView);

        mainDataGridView.VirtualMode = true;
        mainDataGridView.CellValueNeeded += MainDataGridView_CellValueNeeded;
        mainDataGridView.CellFormatting += MainDataGridView_CellFormatting;
        mainDataGridView.CellValuePushed += MainDataGridView_CellValuePushed;

        mainDataGridView.SelectionChanged += MainDataGridView_SelectionChanged;

        mainDataGridView.ColumnHeaderMouseClick += MainDataGridView_ColumnHeaderMouseClick;
        mainDataGridView.ColumnStateChanged += MainDataGridView_ColumnStateChanged;
        mainDataGridView.RowPostPaint += MainDataGridView_RowPostPaint;
        mainDataGridView.RowHeadersWidth = 50;
        mainDataGridView.EditMode = DataGridViewEditMode.EditOnF2;

        versionNumberLabel.Text = Program.VERSION;
        Text = originalFormName;

        associateBcsvToolStripMenuItem.Checked = ProgramAssociation.GetAssociatedProgram(".bcsv") == Application.ExecutablePath;

        viewColumnsMenuItem.DropDownItemClicked += ViewColumnsMenuItem_DropDownItemClicked;
        viewColumnsMenuItem.DropDown.Closing += ViewColumnsMenuItem_DropDown_Closing;

        foreach (var dataType in Enum.GetValues<DataType>())
        {
            viewAsToolStripMenuItem.DropDown.Items.Add(new ToolStripMenuItem(dataType.ToString(), null, new EventHandler((_, _) =>
            {
                if (LoadedFile == null ||
                  lastSelectedColumn == -1 ||
                  lastSelectedColumn >= mainDataGridView.ColumnCount ||
                  mainDataGridView.Columns[lastSelectedColumn] is not IndexableDataGridColumn indexableDataGridColumn ||
                  indexableDataGridColumn.HeaderIndex >= LoadedFile.Fields.Length) return;

                var column = LoadedFile.Fields[indexableDataGridColumn.HeaderIndex];

                HashManager.AddOrEditForcedType(column.HEX, dataType);

                MessageBox.Show("Re-open your file to update values!");
            }))
            {
                ForeColor = Color.White
            });
        }
    }

    private void MainDataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
    {
        var grid = sender as DataGridView;
        var rowIdx = (e.RowIndex + 1).ToString();

        var centerFormat = new StringFormat()
        {
            // right alignment might actually make more sense for numbers
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
        e.Graphics.DrawString(rowIdx, this.Font, new SolidBrush(Color.FromArgb(110, 110, 110)), headerBounds, centerFormat);
    }

    public void ChangedRowCount(int count)
    {
        // reset ordering column (remove icon)
        ResetOrdering();

        reorderableIndexDictionary = [];

        for (int i = 0; i < count; i++)
            reorderableIndexDictionary.Add(i);

        mainDataGridView.RowCount = count;
        mainDataGridView.Invalidate();
    }

    private void MainDataGridView_SelectionChanged(object sender, EventArgs e)
    {
        if (mainDataGridView.SelectedRows.Count > 1)
            compareRowsToolStripMenuItem.Enabled = true;
        else
            compareRowsToolStripMenuItem.Enabled = false;

        ReloadInfo();
    }

    private void MainDataGridView_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
    {
        if (LoadedFile == null
            || e.ColumnIndex >= mainDataGridView.ColumnCount
            || e.RowIndex >= reorderableIndexDictionary.Count
            || mainDataGridView.Columns[e.ColumnIndex] is not IndexableDataGridColumn indexableColumn)
            return;

        var entryIndex = reorderableIndexDictionary[e.RowIndex];
        var fieldIndex = indexableColumn.HeaderIndex;

        if (entryIndex >= LoadedFile.Length || fieldIndex >= LoadedFile.Fields.Length) return;

        if (indexableColumn.CellTemplate is ColorDataGridCell colorCell)
        {
            
            if (e.Value is Color color)
                // TODO: May be smart to check if item.Value is out of bounds
                foreach (var item in colorCell.ColorFieldIndexes)
                    LoadedFile.Entries[entryIndex][item.Value] = item.Key switch
                    {
                        "R" => color.R / 255f,
                        "G" => color.G / 255f,
                        "B" => color.B / 255f,
                        _ => 0f,
                    };
        }
        else
        {
            var targetField = LoadedFile.Fields[fieldIndex];
            LoadedFile.Entries[entryIndex][fieldIndex] = e.Value;

            if (mainDataGridView.SelectionMode == DataGridViewSelectionMode.CellSelect)
            {
                // update entries from selected cells, if DataType matches the targetField
                var selectedCells = mainDataGridView.SelectedCells;
                if (selectedCells.Count > 0)
                {
                    foreach (DataGridViewCell selectedCell in selectedCells)
                    {
                        if (selectedCell.RowIndex == e.RowIndex && selectedCell.ColumnIndex == e.ColumnIndex) continue;

                        if (selectedCell.OwningColumn is not IndexableDataGridColumn cellColumn ||
                            cellColumn.HeaderIndex >= LoadedFile.Fields.Length) continue;

                        var selectedCellField = LoadedFile.Fields[cellColumn.HeaderIndex];

                        if (targetField.DataType != selectedCellField.DataType) continue;

                        if (selectedCell.RowIndex >= reorderableIndexDictionary.Count) return;
                        var selectedIndex = reorderableIndexDictionary[selectedCell.RowIndex];

                        LoadedFile[selectedIndex, selectedCellField] = e.Value;
                    }
                }
            }
            else if (mainDataGridView.SelectionMode == DataGridViewSelectionMode.FullRowSelect)
            {
                // update entries from the same column
                var selectedRows = mainDataGridView.SelectedRows;
                if (selectedRows.Count > 0)
                {
                    foreach (DataGridViewRow selectedRow in selectedRows)
                    {
                        if (selectedRow.Index == e.RowIndex) continue;

                        if (selectedRow.Index >= reorderableIndexDictionary.Count) return;
                        var selectedIndex = reorderableIndexDictionary[selectedRow.Index];

                        LoadedFile.Entries[selectedIndex][indexableColumn.HeaderIndex] = e.Value;
                    }
                }
            }
        }
    }

    internal static (string, bool) GetFormattedValue(object[] values, int index, Field field)
    {
        if (field == null || values == null || index < 0 || index >= values.Length)
            return ("Invalid", false);

        var val = values[index];

        switch (field.DataType)
        {
            case DataType.CRC32:
                {
                    if (val is not uint hashValue) return ("Invalid", false);

                    var hash = HashManager.GetHashTranslationOrNull(hashValue);
                    return (hash ?? hashValue.ToString("x"), hash != null);
                }

            case DataType.MMH3:
                {
                    if (val is not uint hashValue) return ("Invalid", false);

                    var hash = HashManager.GetMurmurHashTranslationOrNull(hashValue);
                    return (hash ?? hashValue.ToString("x"), hash != null);
                }

            case DataType.BitField:
                {
                    if (val is not byte[] bitField) return ("Invalid", false);

                    return (string.Join(" ", bitField), true);
                }

            default:
                return (val.ToString(), true);
        }
    }

    private void MainDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (LoadedFile == null || e.RowIndex >= reorderableIndexDictionary.Count || e.ColumnIndex >= mainDataGridView.ColumnCount) return;
        var entryIndex = reorderableIndexDictionary[e.RowIndex];

        if (entryIndex >= LoadedFile.Length || mainDataGridView.Columns[e.ColumnIndex] is not IndexableDataGridColumn column) return;

        var entry = LoadedFile.Entries[entryIndex];
        var field = LoadedFile.Fields[column.HeaderIndex];

        
        if (column.CellTemplate is ColorDataGridCell)
        {
            // If its a Color Cell, apply color to the button and get text in R, G, B format
            if (e.Value is Color color)
            {
                e.CellStyle.BackColor = color;
                e.Value = $"{color.R}, {color.G}, {color.B}";
            } else
            {
                e.CellStyle.BackColor = Color.Red;
                e.Value = "Invalid Color";
            }
        }
        else
        {
            var (formattedValue, isFormatted) = GetFormattedValue(entry, column.HeaderIndex, field);

            if (formattedValue == "Invalid" && !isFormatted)
                e.CellStyle.BackColor = Color.Red;
            else if (!isFormatted && (field.DataType == DataType.CRC32 || field.DataType == DataType.MMH3))
                e.CellStyle.BackColor = Color.DarkRed;

            e.Value = formattedValue;
           
        }

        e.FormattingApplied = true;
    }

    private List<int> reorderableIndexDictionary = [];
    public void OrderColumn(int columnIndex, SortOrder order)
    {
        if (LoadedFile == null) return;

        var column = mainDataGridView.Columns[columnIndex];
        if (column == null || column is not IndexableDataGridColumn indexableColumn) return;

        if (columnIndex < 0 || indexableColumn.HeaderIndex >= LoadedFile.Fields.Length) return;

        reorderableIndexDictionary.Sort(delegate (int x, int y)
        {
            if (order == SortOrder.None)
                return x.CompareTo(y);

            var entryX = LoadedFile.Entries[x][indexableColumn.HeaderIndex];
            var entryY = LoadedFile.Entries[y][indexableColumn.HeaderIndex];

            if (order == SortOrder.Ascending)
                return CompareObjects(entryX, entryY);
            else return CompareObjects(entryY, entryX);
        });
    }

    private static int CompareObjects(object entryX, object entryY)
    {
        // Checking both may be useless
        if (entryX is uint xUint && entryY is uint yUint)
            return xUint.CompareTo(yUint);

        else if (entryX is int xInt && entryY is int yInt)
            return xInt.CompareTo(yInt);

        else if (entryX is short xShort && entryY is short yShort)
            return xShort.CompareTo(yShort);

        else if (entryX is ushort xUshort && entryY is ushort yUshort)
            return xUshort.CompareTo(yUshort);

        else if (entryX is string xString && entryY is string yString)
            return xString.CompareTo(yString);

        else if (entryX is float xFloat && entryY is float yFloat)
            return xFloat.CompareTo(yFloat);

        else if (entryX is byte xByte && entryY is byte yByte)
            return xByte.CompareTo(yByte);

        else if (entryX is sbyte xSbyte && entryY is sbyte ySbyte)
            return xSbyte.CompareTo(ySbyte);

        else return entryX.ToString().CompareTo(entryY.ToString());
    }

    private void MainDataGridView_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= LoadedFile.Length) return;

        if (reorderableIndexDictionary.Count == 0 || e.RowIndex >= reorderableIndexDictionary.Count) return;

        var column = mainDataGridView.Columns[e.ColumnIndex];
        var originalIndex = reorderableIndexDictionary[e.RowIndex];

        if (column == null || column is not IndexableDataGridColumn indexableColumn) return;

        var entry = LoadedFile.Entries[originalIndex];
        
        if (indexableColumn.CellTemplate is ColorDataGridCell dataGridCell)
        {
            void TryGetColorValue(string colorId, out float color)
            {
                color = 0f;
                if (dataGridCell.ColorFieldIndexes.TryGetValue(colorId, out var colorField))
                {
                    if (entry[colorField] is float colorVal)
                        color = colorVal;
                }
            }

            TryGetColorValue("R", out float r);
            TryGetColorValue("G", out float g);
            TryGetColorValue("B", out float b);

            e.Value = dataGridCell.GetColor(r, g, b);
        } 
        else
        {
            e.Value = entry[indexableColumn.HeaderIndex];
        }
    }

    private void ReloadInfo()
    {
        if (LoadedFile == null)
        {
            dragInfo.Visible = true;
            statusStripMenu.Visible = false;
            editToolStripMenuItem.Enabled = false;
            saveAsToolStripMenuItem.Enabled = false;
            unloadFileToolStripMenuItem.Enabled = false;
            exportToCSVFileToolStripMenuItem.Enabled = false;
            importFromFileToolStripMenuItem.Enabled = false;
            exportSelectionToolStripMenuItem.Enabled = false;
            return;
        }

        var infos = new List<string> {
            $"CRC32 Hashes: {HashManager.CRC32_Hashes.Count} | Murmur Hashes: {HashManager.MMH3_Hashes.Count}"
        };

        if (mainDataGridView.Columns.Count > 0)
            infos.Add("Columns: " + mainDataGridView.Columns.Count);

        if (mainDataGridView.Columns.Count > 0)
            infos.Add("Rows: " + mainDataGridView.Rows.Count);

        if (mainDataGridView.CurrentCell != null)
            infos.Add("Selected RowId: " + mainDataGridView.CurrentCell.RowIndex);

        infoLabel.Text = string.Join(" | ", infos);

        dragInfo.Visible = false;
        statusStripMenu.Visible = true;
        editToolStripMenuItem.Enabled = true;
        saveAsToolStripMenuItem.Enabled = true;
        unloadFileToolStripMenuItem.Enabled = true;
        exportToCSVFileToolStripMenuItem.Enabled = true;
        importFromFileToolStripMenuItem.Enabled = true;
        exportSelectionToolStripMenuItem.Enabled = true;
    }

    private void ClearDataGrid()
    {
        ClearSearchCache();
        Text = originalFormName;
        mainDataGridView.ClearSelection();
        mainDataGridView.Columns.Clear();
        ChangedRowCount(0);
        mainDataGridView.Refresh();

        ReloadInfo();
    }

    private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog()
        {
            Title = "Select a BCSV to import",
            Filter = "BCSV|*.bcsv",
            DefaultExt = "*.bcsv"
        };

        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            var path = openFileDialog.FileName;

            if (!File.Exists(path)) return;

            LoadBCSVFile(path);
        }
    }

    /// <summary>
    /// Handle path as an input; e.g. User double-clicked a .bcsv file or opened it with the editor.
    /// </summary>
    /// <param name="path">File path</param>
    public void HandleInput(string path)
    {
        var extension = Path.GetExtension(path);
        switch (extension)
        {
            case ".bcsv":
                LoadBCSVFile(path);
                break;
        }
    }

    [GeneratedRegex(@"^(.*?)(?:Color([RGB]))?\s+f32$")]
    private static partial Regex ColorRegex();

    //private bool isUniqueColumnAlreadyFound = false;
    internal void LoadBCSVFile(string path)
    {
        ClearDataGrid();

        using (var reader = File.OpenRead(path))
            LoadedFile = new BinaryCSV(reader.ToArray());

        if (LoadedFile == null) return;

        Text = $"{originalFormName}: {Path.GetFileName(path)}";

        viewColumnsMenuItem.DropDownItems.Clear();

        var uniqueHeader = BinaryCSV.UniqueHashes.FirstOrNull(x => LoadedFile.Fields.Any(field => field.Hash == x));
        var colorFields = new Dictionary<string, Dictionary<string, int>>();
        //  {
        //      Color:
        //          R: 2,  # Where 2, 3, 4 is the fieldIndex
        //          G: 3,
        //          B: 4
        //  }

        for (int fieldIndex = 0; fieldIndex < LoadedFile.Fields.Length; fieldIndex++)
        {
            var fieldHeader = LoadedFile.Fields[fieldIndex];
            var translatedName = fieldHeader.GetTranslatedNameOrNull();
            string colorColumn = null;

            if (translatedName != null)
            {
                var match = ColorRegex().Match(translatedName);
                if (match.Success && match.Groups[1].Success && match.Groups[2].Success)
                {
                    //Console.WriteLine($"{match.Groups[0].Value} | {match.Groups[1].Value} | {match.Groups[2].Value}");
                    colorColumn = match.Groups[1].Value;
                    var colorKey = match.Groups[2].Value;

                    if (colorFields.TryGetValue(colorColumn, out var color))
                    {
                        color.TryAdd(colorKey, fieldIndex);
                    } 
                    else
                    {
                        colorFields.Add(colorColumn, new Dictionary<string, int>() 
                        {
                            { 
                                colorKey, fieldIndex 
                            }
                        });
                    }

                }
            }

            DataGridViewCell cellTemplate = fieldHeader.DataType switch
            {
                DataType.CRC32 => new CRC32DataGridComboCell(),
                DataType.MMH3 => new MMH3DataGridCell(),
                DataType.BitField => new BitFieldDataGridCell()
                {
                    FieldLength = fieldHeader.Size
                },
                _ => new DataGridViewTextBoxCell(),
            };

            if (colorColumn != null)
            {
                if (colorFields[colorColumn].Count == 3)
                {
                    cellTemplate = new ColorDataGridCell
                    {
                        ColorFieldIndexes = colorFields[colorColumn]
                    };                
                }
                else continue; // Don't add a new column before we get the 3 needed columns
            }

            string tooltip = (translatedName != null ? $"Name: {translatedName}\n" : "") +
                $"Hash: {fieldHeader.HEX}\n" +
                $"Type: {fieldHeader.DataType}\n" +
                $"Size: {fieldHeader.Size}\n" +
                $"Index: {fieldIndex}";

            int columnId = mainDataGridView.Columns.Add(new IndexableDataGridColumn(fieldIndex)
            {
                Name = fieldHeader.HEX,
                HeaderText = fieldHeader.DisplayName,
                ValueType = fieldHeader.DataType.ToType(),
                CellTemplate = cellTemplate,
                SortMode = DataGridViewColumnSortMode.Automatic,
                ToolTipText = tooltip,
            });

            var column = mainDataGridView.Columns[columnId];

            if (colorColumn != null)
                column.HeaderText = $"{colorColumn} Color";

            if (fieldHeader.DataType == DataType.CRC32 || fieldHeader.DataType == DataType.MMH3)
                column.DefaultCellStyle.Font = new Font(mainDataGridView.Font, FontStyle.Bold);


            viewColumnsMenuItem.DropDownItems.Add(new ToolStripMenuItem()
            {
                Name = $"ViewColumn_{fieldHeader.HEX}",
                Text = fieldHeader.DisplayName,
                ForeColor = Color.White,
                CommandParameter = column.Name,
                Checked = column.Visible
            });

            if (fieldHeader.IsMissingHash)
            {
                if (fieldHeader.Size == 4)
                    column.HeaderCell.Style.BackColor = Color.PaleVioletRed;
                else
                    column.HeaderCell.Style.BackColor = Color.Orange;
            }

            if (uniqueHeader != null && uniqueHeader == fieldHeader.Hash)
            {
                column.DisplayIndex = 0;
                column.HeaderCell.Style.BackColor = Color.DarkGreen;
                column.HeaderCell.Style.Font = new Font(mainDataGridView.Font, FontStyle.Bold);
            }
        }

        ChangedRowCount(LoadedFile.Length);

        ReloadInfo();
    }

    public void UnloadFile()
    {
        ClearDataGrid();
        LoadedFile.Dispose();
        LoadedFile = null;

        ReloadInfo();

        lastSelectedColumn = -1;

        // force gc to collect garbage
        GC.Collect();
    }

    BCSVDirectorySearch directorySearchWindow = null;
    private void SearchOnFilesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        // Create directory search window if needed
        if (directorySearchWindow == null || directorySearchWindow.IsDisposed)
            directorySearchWindow = new BCSVDirectorySearch();

        directorySearchWindow.Show();
        directorySearchWindow.BringToFront();
    }

    private void UnloadFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show("Do you really want to close this file?\nUnsaved changes will be lost!", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            UnloadFile();
        }
    }

    private void NewEntryToolStripMenuItem_Click(object sender, EventArgs e)
    {

        if (LoadedFile == null) return;

        // for some weird reason I can't use directly a object[], "newValues[fieldIndex] = newValue" causes a OutOfRange exception 
        var newValues = new Dictionary<Field, object>(LoadedFile.Fields.Length);

        if (LoadedFile.Length > 0)
        {
            var lastEntry = LoadedFile.Entries.Last();

            if (mainDataGridView.SelectedRows.Count > 0)
            {
                var selectedRow = mainDataGridView.SelectedRows[0];
                if (selectedRow.Index < LoadedFile.Length && selectedRow.Index < reorderableIndexDictionary.Count)
                {
                    var atualIndex = reorderableIndexDictionary[selectedRow.Index];
                    lastEntry = LoadedFile.Entries[atualIndex];
                }
            }

            if (lastEntry != null && lastEntry.Length == LoadedFile.Fields.Length)
            {
                for (int i = 0; i < LoadedFile.Fields.Length; i++)
                {
                    Field field = LoadedFile.Fields[i];
                    newValues[field] = lastEntry[i];
                }
            }
        }

        var newEntryWindow = new BCSVEntryEditor(() =>
        {
            if (newValues.Count < LoadedFile.Fields.Length)
                throw new Exception("Values Length is less than the Fields Length");

            var newEntry = new object[LoadedFile.Fields.Length];

            for (int i = 0; i < LoadedFile.Fields.Length; i++)
            {
                Field field = LoadedFile.Fields[i];

                if (!newValues.TryGetValue(field, out object val))
                    throw new Exception("Field value not found in list");

                newEntry[i] = val;
            }

            LoadedFile.Entries.Add(newEntry);

            ChangedRowCount(LoadedFile.Length);
            mainDataGridView.FirstDisplayedScrollingRowIndex = LoadedFile.Length - 1;

            ReloadInfo();
        });

        for (int fieldIndex = 0; fieldIndex < LoadedFile.Fields.Length; fieldIndex++)
        {
            Field field = LoadedFile.Fields[fieldIndex];
            var defaultValue = newValues.TryGetValue(field, out object val) ? val : field.GetFieldDefaultValue();

            Control contentControl = field.DataType switch
            {
                DataType.U8 => new ByteEntry(defaultValue),
                DataType.S8 => new SbyteEntry(defaultValue),
                DataType.Int16 => new Int16Entry(defaultValue),
                DataType.UInt16 => new UInt16Entry(defaultValue),
                DataType.Int32 => new Int32Entry(defaultValue),
                DataType.UInt32 => new UInt32Entry(defaultValue),
                DataType.Float32 => new Float32Entry(defaultValue),
                DataType.Float64 => new Float64Entry(defaultValue),
                DataType.String => new StringEntry(defaultValue, field.Size),

                DataType.BitField => new BitFieldEntry(field.Size, defaultValue, HashManager.BitField.TryGetValue(field.Hash, out string[] bitfields) ? bitfields : null),
                _ => null
            };

            if (contentControl is IBCSVEntry entry)
            {
                entry.SetCallback(val => newValues[field] = val);
                entry.SetPropertyName(field.DisplayName);

                newEntryWindow.AddContent(contentControl);

                if (field == LoadedFile.UniqueField)
                {
                    entry.SetUniqueIdentifier();
                    newEntryWindow.MoveContent(contentControl, 0);
                }
            }
            else if (contentControl != null) throw new Exception($"{contentControl.GetType().Name} does not implement {nameof(IBCSVEntry)}");
        }

        newEntryWindow.ShowDialog();

    }

    private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile != null)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Filter = "BCSV (*.bcsv)|*.bcsv",
                FilterIndex = 1,
                RestoreDirectory = true,
                OverwritePrompt = true
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                File.WriteAllBytes(saveFileDialog.FileName, LoadedFile.Save());
        }
    }

    private void EditToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
    {
        if (LoadedFile == null)
        {
            newEntryToolStripMenuItem.Enabled = false;
            duplicateRowToolStripMenuItem.Enabled = false;
        }

        duplicateRowToolStripMenuItem.Enabled = mainDataGridView.SelectedRows.Count > 0;
        deleteRowsToolStripMenuItem.Enabled = mainDataGridView.SelectedRows.Count > 0;
    }

    private void DuplicateRowToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null) return;

        foreach (DataGridViewRow selectedRow in mainDataGridView.SelectedRows)
        {
            if (selectedRow.Index >= LoadedFile.Length || selectedRow.Index >= reorderableIndexDictionary.Count) continue;

            var atualIndex = reorderableIndexDictionary[selectedRow.Index];
            var selectedEntry = LoadedFile.Entries[atualIndex];
            var newEntry = new object[selectedEntry.Length];

            for (var i = 0; i < selectedEntry.Length; i++)
                newEntry[i] = selectedEntry[i];

            // TODO: Check UniqueId and assign one?
            LoadedFile.Entries.Add(newEntry);

            ChangedRowCount(LoadedFile.Length);
        }

        ReloadInfo();
    }

    private void DeleteRowsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var count = mainDataGridView.SelectedRows.Count;
        var text = count > 1 ? $"these {count} entries" : "this entry";
        var result = MessageBox.Show($"Do you really want to delete {text}?\nThis action can't be un-done!", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            var indexesToRemove = new List<int>();
            foreach (DataGridViewRow item in mainDataGridView.SelectedRows)
            {
                if (item.Index >= LoadedFile.Length || item.Index >= reorderableIndexDictionary.Count) continue;

                var atualIndex = reorderableIndexDictionary[item.Index];
                indexesToRemove.Add(atualIndex);
            }

            foreach (var index in indexesToRemove.OrderByDescending(x => x))
                LoadedFile.Entries.RemoveAt(index);

            ChangedRowCount(LoadedFile.Length);
        }

        ReloadInfo();
    }

    private int lastSelectedColumn = -1;

    private string sortColumn = null;
    private SortOrder sortOrder = SortOrder.None;
    public void ResetOrdering()
    {
        if (sortColumn != null && mainDataGridView.Columns.Contains(sortColumn))
            mainDataGridView.Columns[sortColumn].HeaderCell.SortGlyphDirection = SortOrder.None;

        sortColumn = null;
        sortOrder = SortOrder.None;
    }

    private void MainDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        if (LoadedFile == null || LoadedFile.Fields.Length <= e.ColumnIndex) return;

        if (e.Button == MouseButtons.Left)
        {
            string clickedColumn = mainDataGridView.Columns[e.ColumnIndex].Name;

            if (sortColumn != null && sortColumn != clickedColumn && mainDataGridView.Columns.Contains(sortColumn))
                mainDataGridView.Columns[sortColumn].HeaderCell.SortGlyphDirection = SortOrder.None;

            if (sortColumn == clickedColumn)
            {
                switch (sortOrder)
                {
                    case SortOrder.None:
                        sortOrder = SortOrder.Descending;
                        break;
                    case SortOrder.Descending:
                        sortOrder = SortOrder.Ascending;
                        break;
                    case SortOrder.Ascending:
                        sortOrder = SortOrder.None;
                        break;
                }
            }
            else
            {
                // A new column was clicked, set ascending order by default.
                sortColumn = clickedColumn;
                sortOrder = SortOrder.Descending;
            }

            mainDataGridView.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = sortOrder;
            OrderColumn(e.ColumnIndex, sortOrder);

            mainDataGridView.Invalidate();
        }

        if (e.Button == MouseButtons.Right)
        {
            lastSelectedColumn = e.ColumnIndex;
            validHeaderContextMenu.Show(Cursor.Position);
        }
    }

    private void MainFrm_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(x => Path.GetExtension(x) == ".bcsv"))
            e.Effect = DragDropEffects.Move;

        else
            e.Effect = DragDropEffects.None;
    }

    private void MainFrm_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var fileList = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string filePath in fileList)
            {
                if (Path.GetExtension(filePath) == ".bcsv")
                {
                    LoadBCSVFile(filePath);
                    break;
                }
            }
        }
    }

    private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (LoadedFile != null)
        {
            var result = MessageBox.Show("Do you really want to close this file?\nUnsaved changes will be lost!", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
                UnloadFile();
            else e.Cancel = true;
        }
    }

    private void AssociateBCSVToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var exePath = Application.ExecutablePath;
        var arguments = $"--{(associateBcsvToolStripMenuItem.Checked ? "disassociate" : "associate")} bcsv";

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            Verb = "runas",
            UseShellExecute = true
        });

        process.WaitForExit();
        associateBcsvToolStripMenuItem.Checked = ProgramAssociation.GetAssociatedProgram(".bcsv") == Application.ExecutablePath;
    }


    private void ExportToCSVFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (mainDataGridView.Columns.Count == 0) return;

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FilterIndex = 1,
            RestoreDirectory = true,
            OverwritePrompt = true
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            var sb = new StringBuilder();

            var headers = mainDataGridView.Columns.Cast<DataGridViewColumn>();
            sb.AppendLine(string.Join(",", [.. headers.Select(column => $"\"{column.HeaderText}\"")]));

            for (int i = 0; i < LoadedFile.Entries.Count; i++)
            {
                var row = LoadedFile.Entries[i];
                //var cells = row.Cells.Cast<DataGridViewCell>();
                //sb.AppendLine(string.Join(",", cells.Select(cell => $"\"{cell.Value}\"").ToArray()));
                string getHashTranslationForCell(object cell)
                {
                    var parsedValue = $"\"{cell}\"";
                    var field = LoadedFile.Fields[i];

                    if (field == null) return parsedValue;

                    switch (field.DataType)
                    {
                        case DataType.CRC32:
                            {
                                if (cell is uint hashValue && HashManager.GetHashTranslationOrNull(hashValue) is string value)
                                    return value;

                                break;
                            }

                        case DataType.MMH3:
                            {
                                if (cell is uint hashValue && HashManager.GetMurmurHashTranslationOrNull(hashValue) is string value)
                                    return value;

                                break;
                            }
                    }


                    return parsedValue;
                }

                sb.AppendLine(string.Join(",", [.. row.Select(getHashTranslationForCell)]));

            }

            File.WriteAllText(saveFileDialog.FileName, sb.ToString());
        }
    }

    private void ExportValuesAstxtFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null ||
            lastSelectedColumn == -1 ||
            lastSelectedColumn >= mainDataGridView.ColumnCount ||
            mainDataGridView.Columns[lastSelectedColumn] is not IndexableDataGridColumn indexableDataGridColumn ||
            indexableDataGridColumn.HeaderIndex >= LoadedFile.Fields.Length) return;

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt",
            FilterIndex = 1,
            RestoreDirectory = true,
            OverwritePrompt = true
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {

            var field = LoadedFile.Fields[indexableDataGridColumn.HeaderIndex];

            var exportedValues = new List<string>();
            foreach (var row in LoadedFile.Entries)
            {
                var cell = row[indexableDataGridColumn.HeaderIndex];
                if (field.DataType == DataType.CRC32)
                {
                    if (cell is uint hashValue)
                    {
                        var containsKey = HashManager.GetHashTranslation(hashValue);
                        exportedValues.AddIfNotExist(containsKey);

                        //if (containsKey)
                        //    exportedValues.AddIfNotExist(HashManager.CRC32_Hashes[hashValue]);
                        //else
                        //    exportedValues.AddIfNotExist(hashValue.ToString("x"));

                        continue;
                    }
                }
                else if (field.DataType == DataType.MMH3)
                {
                    if (cell is uint hashValue)
                    {
                        var containsKey = HashManager.GetMurmurHashTranslationOrNull(hashValue);
                        exportedValues.AddIfNotExist(containsKey ?? hashValue.ToString("X"));
                        //if (containsKey)
                        //    exportedValues.AddIfNotExist(HashManager.MMH3_Hashes[hashValue]);
                        //else
                        //    exportedValues.AddIfNotExist(hashValue.ToString("x"));

                        continue;
                    }
                }

                exportedValues.Add(cell.ToString());
            }

            File.WriteAllLines(saveFileDialog.FileName, exportedValues);
        }
    }


    private void CompareRowsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var compareWindow = new BCSVCompareWindow();
        compareWindow.compareDataGrid.Columns.Clear();

        foreach (DataGridViewColumn column in mainDataGridView.Columns)
        {
            var columnId = compareWindow.compareDataGrid.Columns.Add(column.Name, column.HeaderText);
            compareWindow.compareDataGrid.Columns[columnId].HeaderCell.Style = column.HeaderCell.Style;

        }

        compareWindow.compareDataGrid.Rows.Clear();
        foreach (DataGridViewRow row in mainDataGridView.SelectedRows)
        {
            var values = new List<object>();
            foreach (DataGridViewCell entry in row.Cells)
                values.Add(entry.FormattedValue);

            compareWindow.compareDataGrid.Rows.Add([.. values]);
        }

        compareWindow.ShowDialog();
    }


    private SearchBox searchBox;
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
    private DataGridViewCell[] searchCache;
    private DataGridViewCell lastSearchCell;
    private Dictionary<DataGridViewCell, Color> oldColorCache = null;

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

    internal void ClearSearchCache()
    {
        if (searchCache != null && searchCache.Length > 0)
        {
            SearchClosing();

            lastSearchCell = null;
            searchCache = null;
        }
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

            if (!caseSensitive) search = search.ToLower();

            List<DataGridViewCell> cells = [];
            for (int columnIndex = 0; columnIndex < mainDataGridView.Columns.Count; columnIndex++)
            {
                if (mainDataGridView.Columns[columnIndex] is not IndexableDataGridColumn indexableDataGridColumn) continue;
                if (indexableDataGridColumn.HeaderIndex >= LoadedFile.Fields.Length) continue;

                var field = LoadedFile.Fields[indexableDataGridColumn.HeaderIndex];

                for (int rowIndex = 0; rowIndex < mainDataGridView.Rows.Count; rowIndex++)
                {
                    var actualIndex = reorderableIndexDictionary[rowIndex];
                    var (formattedValue, isFormatted) = GetFormattedValue(LoadedFile.Entries[actualIndex], indexableDataGridColumn.HeaderIndex, field);

                    if (!isFormatted) continue;

                    if (!caseSensitive) formattedValue = formattedValue.ToLower();

                    if ((searchType == SearchType.Contains && formattedValue.Contains(search)) ||
                        (searchType == SearchType.Exactly && formattedValue == search))
                        cells.Add(mainDataGridView[columnIndex, rowIndex]);


                }
            }

            searchCache = [.. cells];

            if (searchCache.Length > 1 && searchBackwards)
                currentSearchIndex = searchCache.Length - 1;
            else
                currentSearchIndex = 0;

            searchBox.UpdateMatchesFound(searchCache.Length, currentSearchIndex);

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

            searchBox.UpdateMatchesFound(searchCache.Length, currentSearchIndex);

            var current = searchCache[currentSearchIndex];

            mainDataGridView.FirstDisplayedScrollingColumnIndex = current.ColumnIndex;
            mainDataGridView.FirstDisplayedScrollingRowIndex = current.RowIndex;

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

    private void ExportSelectionToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null) return;

        var newFile = BinaryCSV.CopyFileWithoutEntries(LoadedFile);

        var selectedRows = new DataGridViewRow[mainDataGridView.SelectedRows.Count];
        mainDataGridView.SelectedRows.CopyTo(selectedRows, 0);

        newFile.Entries = [.. LoadedFile.Entries
            .Where((_, index) => selectedRows.Any(y => y.Index < reorderableIndexDictionary.Count && reorderableIndexDictionary[y.Index] == index
        ))];

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "BCSV (*.bcsv)|*.bcsv",
            FilterIndex = 1,
            RestoreDirectory = true,
            OverwritePrompt = true
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
            File.WriteAllBytes(saveFileDialog.FileName, newFile.Save());
    }

    private void ImportFromFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog()
        {
            Title = "Select a BCSV to import",
            Filter = "BCSV|*.bcsv",
            DefaultExt = "*.bcsv"
        };

        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            var file = openFileDialog.FileName;

            using var reader = File.OpenRead(file);
            var bcsvToCopy = new BinaryCSV(reader.ToArray());

            var hasAnyField = LoadedFile.Fields.Any(x => bcsvToCopy.Fields.Contains(x));

            if (!hasAnyField)
            {
                MessageBox.Show("The file you're trying to import don't have any compatible headers.", "Failed to import", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            List<Field> missingFieldsInLoadedFile = [.. LoadedFile.Fields.Where(x => !bcsvToCopy.Fields.Contains(x))];
            List<Field> missingFieldsInSource = [.. bcsvToCopy.Fields.Where(x => !LoadedFile.Fields.Contains(x))];


            foreach (var entry in bcsvToCopy.Entries)
            {
                var newEntry = new object[LoadedFile.Fields.Length];

                foreach (var field in LoadedFile.Fields)
                {
                    if (bcsvToCopy.Fields.Contains(field))
                    {
                        // The file to copy have this field, but we need to make sure the order is correct
                        var sourceFieldIndex = Array.IndexOf(bcsvToCopy.Fields, field);
                        var targetFieldIndex = Array.IndexOf(LoadedFile.Fields, field);

                        newEntry[targetFieldIndex] = entry[sourceFieldIndex];  
                    }
                    else
                    {
                        // Use default value instead
                        var fieldIndex = Array.IndexOf(LoadedFile.Fields, field);
                        var defaultValue = field.GetFieldDefaultValue();

                        newEntry[fieldIndex] = defaultValue;
                        Console.WriteLine($"Default value set in {field.DisplayName}: {defaultValue}");

                        //if (!missingFieldsInLoadedFile.Contains(field))
                        //    missingFieldsInLoadedFile.Add(field);
                    }
                }

                LoadedFile.Entries.Add(newEntry);
                ChangedRowCount(LoadedFile.Length);
            }

            mainDataGridView.FirstDisplayedScrollingRowIndex = LoadedFile.Length - 1;

            if (missingFieldsInSource.Count != 0 || missingFieldsInLoadedFile.Count != 0)
            {
                var message = "Import completed with warnings:\n";
                if (missingFieldsInSource.Count != 0)
                {
                    message += "\nThe following columns were present in the source file but missing in the loaded file:\n";
                    foreach (var field in missingFieldsInSource)
                        message += $"- {field.DisplayName}\n";
                }

                if (missingFieldsInLoadedFile.Count != 0)
                {
                    message += "\nThe following fields were present in the current file but missing in the source file. Default values were used instead:\n";
                    foreach (var field in missingFieldsInLoadedFile.Distinct())
                        message += $"- {field.DisplayName}\n";
                }

                MessageBox.Show(message, "Import completed with warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void HeaderNameToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null ||
                   lastSelectedColumn == -1 ||
                   lastSelectedColumn >= mainDataGridView.ColumnCount ||
                   mainDataGridView.Columns[lastSelectedColumn] is not IndexableDataGridColumn indexableDataGridColumn ||
                   indexableDataGridColumn.HeaderIndex >= LoadedFile.Fields.Length) return;

        var field = LoadedFile.Fields[indexableDataGridColumn.HeaderIndex];
        Clipboard.SetText(HashManager.GetHashTranslation(field.Hash));
    }

    private void HeaderHashToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (LoadedFile == null ||
                  lastSelectedColumn == -1 ||
                  lastSelectedColumn >= mainDataGridView.ColumnCount ||
                  mainDataGridView.Columns[lastSelectedColumn] is not IndexableDataGridColumn indexableDataGridColumn ||
                  indexableDataGridColumn.HeaderIndex >= LoadedFile.Fields.Length) return;

        var field = LoadedFile.Fields[indexableDataGridColumn.HeaderIndex];

        Clipboard.SetText($"0x{field.Hash:x}");
    }

    #region column visibility
    private void HideColumnToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (lastSelectedColumn == -1 ||
            lastSelectedColumn >= mainDataGridView.ColumnCount) return;

        mainDataGridView.Columns[lastSelectedColumn].Visible = false;
    }

    private void MainDataGridView_ColumnStateChanged(object sender, DataGridViewColumnStateChangedEventArgs e)
    {
        if (e.StateChanged == DataGridViewElementStates.Visible)
        {
            string key = $"ViewColumn_{e.Column.Name}";

            if (viewColumnsMenuItem.DropDownItems.ContainsKey(key) && viewColumnsMenuItem.DropDownItems[key] is ToolStripMenuItem item)
                item.Checked = e.Column.Visible;
        }
    }

    private void ViewColumnsMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem == null || e.ClickedItem is not ToolStripMenuItem menuItem) return;
        if (menuItem.CommandParameter == null || menuItem.CommandParameter is not string columnName) return;

        if (mainDataGridView.Columns.Contains(columnName))
            mainDataGridView.Columns[columnName].Visible = !mainDataGridView.Columns[columnName].Visible;
    }

    private void ViewColumnsMenuItem_DropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
    {
        if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            e.Cancel = true;
    }
    #endregion

    private void ChangeSelectionMode(DataGridViewSelectionMode selectionMode)
    {
        mainDataGridView.SelectionMode = selectionMode;
        rowSelectToolStripMenuItem.Checked = selectionMode == DataGridViewSelectionMode.FullRowSelect;
        cellSelectToolStripMenuItem.Checked = selectionMode == DataGridViewSelectionMode.CellSelect;
    }

    private void RowSelectToolStripMenuItem_Click(object sender, EventArgs e) => ChangeSelectionMode(DataGridViewSelectionMode.FullRowSelect);
    

    private void CellSelectToolStripMenuItem_Click(object sender, EventArgs e) => ChangeSelectionMode(DataGridViewSelectionMode.CellSelect);
    

    private void HashFinderToolStripMenuItem_Click(object sender, EventArgs e)
    {
        new HashFinderForm().Show();
    }

    private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
    {
        StringBuilder sb = new();

        if (mainDataGridView.SelectionMode == DataGridViewSelectionMode.CellSelect)
        {

            var groups = mainDataGridView.SelectedCells.Cast<DataGridViewCell>().GroupBy(y => y.OwningRow).OrderBy(x => x.Key.Index);

            foreach (var group in groups)
            {
                var text = string.Join(";", group.Select(x => x.FormattedValue?.ToString()));

                sb.AppendLine(text);
            }
        }
        else
        {
            var rows = mainDataGridView.SelectedRows.Cast<DataGridViewRow>().OrderBy(x => x.Index);
            foreach (var row in rows)
            {
                var text = string.Join(";", row.Cells.Cast<DataGridViewCell>().Select(x => x.FormattedValue?.ToString()));
                sb.AppendLine(text);
            }
        }

        Clipboard.SetText(sb.ToString());
    }
}