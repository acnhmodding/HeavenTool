using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.BCSV;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HeavenTool.Avalonia.ViewModels;

public partial class BCSVViewModel
{
    // While a cell is being edited, its text box owns Ctrl+C.
    private bool CanCopy() => CanSave() && !isEditing && DataGrid.SelectedItems.Count > 0;

    private void InitializeClipboard() =>
        DataGrid.AddHandler(Control.ContextRequestedEvent, OnGridContextRequested);

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private Task Copy() => CopyRowsAsync(withHeaders: false);

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private Task CopyWithHeaders() => CopyRowsAsync(withHeaders: true);

    /// <summary>Copies one field of every selected row; the shortcut uses the current column.</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyCell(HeavenDataGridColumn? column)
    {
        column ??= DataGrid.CurrentColumn as HeavenDataGridColumn;
        if (column == null) return;
        var rows = SelectedRowsInView();
        await SetClipboardTextAsync(string.Join(Environment.NewLine, rows.Select(row => column.FormatValue(row[column.FieldIndex]))),
            rows.Count == 1 ? $"Copied {column.Header}" : $"Copied {column.Header} of {RowCount(rows.Count)}");
    }

    private async Task CopyRowsAsync(bool withHeaders)
    {
        var rows = SelectedRowsInView();
        var columns = VisibleColumns();
        var text = new StringBuilder();
        if (withHeaders) AppendLine(text, columns.Select(column => column.Header?.ToString() ?? ""));
        foreach (var row in rows) AppendLine(text, columns.Select(column => column.FormatValue(row[column.FieldIndex])));
        await SetClipboardTextAsync(text.ToString(), $"Copied {RowCount(rows.Count)}");
    }

    // Tab-separated, so the rows paste into a spreadsheet as cells.
    private static void AppendLine(StringBuilder text, IEnumerable<string> values)
    {
        text.AppendJoin('\t', values.Select(value => value.AsSpan().IndexOfAny("\t\r\n\"") < 0 ? value
            : $"\"{value.Replace("\"", "\"\"")}\""));
        text.Append(Environment.NewLine);
    }

    /// <summary>Selected rows in the order the grid shows them.</summary>
    private List<BCSVRow> SelectedRowsInView()
    {
        var selected = DataGrid.SelectedItems.OfType<BCSVRow>().ToHashSet();
        var result = new List<BCSVRow>(selected.Count);
        foreach (var row in ((IEnumerable?)DataGrid.CollectionView ?? Rows).OfType<BCSVRow>())
        {
            if (!selected.Contains(row)) continue;
            result.Add(row);
            if (result.Count == selected.Count) break;
        }
        return result;
    }

    private List<HeavenDataGridColumn> VisibleColumns() =>
        [.. DataGrid.Columns.OfType<HeavenDataGridColumn>().Where(column => column.IsVisible).OrderBy(column => column.DisplayIndex)];

    private async Task SetClipboardTextAsync(string text, string status)
    {
        if (window.Clipboard is not { } clipboard) return;
        try
        {
            await clipboard.SetTextAsync(text);
            StatusText = status;
        }
        catch (Exception ex) { ErrorMessage = $"Could not copy: {ex.Message}"; }
    }

    private void OnGridContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || !CanSave() || isEditing || e.Source is not Control source) return;
        ContextMenu? menu = null;
        Control? target = null;
        if (source.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is { } header
            && DataGridColumn.GetColumnContainingElement(header) is HeavenDataGridColumn headerColumn)
            (menu, target) = (CreateHeaderMenu(headerColumn), header);
        else if (source.FindAncestorOfType<DataGridCell>(includeSelf: true) is { DataContext: BCSVRow row } cell
            && DataGridColumn.GetColumnContainingElement(cell) is HeavenDataGridColumn cellColumn)
        {
            // Right-clicking outside the selection acts on the clicked row instead.
            if (!DataGrid.SelectedItems.Contains(row))
            {
                DataGrid.SelectedItems.Clear();
                DataGrid.SelectedItem = row;
            }
            (menu, target) = (CreateCellMenu(cellColumn), cell);
        }
        if (menu == null) return;
        e.Handled = true;
        menu.Open(target);
    }

    private ContextMenu CreateCellMenu(HeavenDataGridColumn column) => new()
    {
        Items =
        {
            MenuEntry("_Copy", CopyCommand, gesture: "Ctrl+C"),
            MenuEntry("Copy with _Headers", CopyWithHeadersCommand),
            MenuEntry($"Copy {column.Header}", CopyCellCommand, column, "Ctrl+Shift+C"),
            MenuEntry("_Export Selected Rows...", ExportSelectionCommand),
            new Separator(),
            MenuEntry("_Duplicate Rows", DuplicateRowsCommand, gesture: "Ctrl+D", icon: "IconCopy"),
            MenuEntry("De_lete Rows", DeleteRowsCommand, gesture: "Delete", icon: "IconTrash")
        }
    };

    private ContextMenu CreateHeaderMenu(HeavenDataGridColumn column)
    {
        var field = loadedFile!.Fields[column.FieldIndex];
        var viewAs = new MenuItem { Header = "_View as" };
        foreach (var type in ViewableTypes(field.Size))
        {
            var item = MenuEntry(type.ToString(), new AsyncRelayCommand(() => ViewColumnAsAsync(column.FieldIndex, type)));
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = type == field.DataType;
            viewAs.Items.Add(item);
        }
        return new ContextMenu
        {
            Items =
            {
                new MenuItem
                {
                    Header = "_Copy",
                    Items =
                    {
                        MenuEntry("Header _Name", new AsyncRelayCommand(() =>
                            SetClipboardTextAsync(field.GetTranslatedNameOrHash(), $"Copied the name of {column.Header}"))),
                        MenuEntry("Header _Hash", new AsyncRelayCommand(() =>
                            SetClipboardTextAsync($"0x{field.Hash:x}", $"Copied the hash of {column.Header}")))
                    }
                },
                viewAs,
                MenuEntry("_Export Values...", ExportColumnValuesCommand, column),
                new Separator(),
                MenuEntry("_Hide Column", new RelayCommand(() => Columns[column.FieldIndex].IsVisible = false))
            }
        };
    }

    private MenuItem MenuEntry(string header, ICommand command, object? parameter = null, string? gesture = null, string? icon = null) => new()
    {
        Header = header,
        Command = command,
        CommandParameter = parameter,
        InputGesture = gesture == null ? null : KeyGesture.Parse(gesture),
        Icon = icon == null ? null : Icon(icon)
    };

    private PathIcon? Icon(string key) => window.TryFindResource(key, out var data) && data is Geometry geometry
        ? new PathIcon { Data = geometry, Width = 14, Height = 14 } : null;

    // Saving writes each value as its type, so only types that fill the field exactly keep every byte.
    private static IEnumerable<DataType> ViewableTypes(int size)
    {
        DataType[] sized = size switch
        {
            1 => [DataType.S8, DataType.U8],
            2 => [DataType.Int16, DataType.UInt16],
            4 => [DataType.Int32, DataType.UInt32, DataType.Float32, DataType.CRC32, DataType.MMH3],
            _ => []
        };
        return [.. sized, DataType.String, DataType.BitField];
    }

    /// <summary>Re-reads the document with a field shown as another type, keeping its unsaved edits.</summary>
    private async Task ViewColumnAsAsync(int fieldIndex, DataType type)
    {
        if (loadedFile == null || IsBusy || !CommitEdits()) return;
        var field = loadedFile.Fields[fieldIndex];
        if (field.DataType == type) return;
        string name = field.DisplayName;
        byte[] bytes;
        bool lossless;
        IsBusy = true;
        try
        {
            loadedFile.Entries = [.. Rows.Select(row => row.Values)];
            (bytes, lossless) = await Task.Run(() =>
            {
                var current = loadedFile.Save();
                // Try the type before remembering it: a string stops at its first zero byte, for example.
                bool known = HashManager.KnownTypes.TryGetValue(field.HEX, out var previous);
                HashManager.KnownTypes[field.HEX] = type;
                try
                {
                    using var stream = new MemoryStream(current, writable: false);
                    return (current, new BinaryCSV(stream).Save().AsSpan().SequenceEqual(current));
                }
                finally
                {
                    if (known) HashManager.KnownTypes[field.HEX] = previous;
                    else HashManager.KnownTypes.Remove(field.HEX);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not show {name} as {type}: {ex.Message}";
            return;
        }
        finally { IsBusy = false; }
        if (!lossless)
        {
            ErrorMessage = $"{name} can't be shown as {type}: saving would change its data.";
            return;
        }

        HashManager.AddOrEditForcedType(field.HEX, type);
        var layout = DataGrid.Columns.OfType<HeavenDataGridColumn>()
            .Select(column => (column.Width, column.DisplayIndex, Columns[column.FieldIndex].IsVisible)).ToArray();
        var selection = SelectedRows().Select(item => item.Index).ToArray();
        bool unsaved = HasUnsavedChanges;
        string? path = localPath;
        if (!await LoadAsync(() => Task.FromResult(bytes), fileName, sourceFile, unsaved)) return;
        localPath = path;

        var columns = DataGrid.Columns.OfType<HeavenDataGridColumn>().ToArray();
        // Display indexes are applied in order so each column lands where it was.
        foreach (var (column, saved) in columns.Zip(layout).OrderBy(pair => pair.Second.DisplayIndex))
        {
            column.Width = saved.Width;
            column.DisplayIndex = saved.DisplayIndex;
        }
        for (int i = 0; i < columns.Length; i++) Columns[i].IsVisible = layout[i].IsVisible;
        SelectRows([.. selection.Select(index => Rows[index])]);
        StatusText = $"Showing {name} as {type} · the undo history was cleared";
    }
}
