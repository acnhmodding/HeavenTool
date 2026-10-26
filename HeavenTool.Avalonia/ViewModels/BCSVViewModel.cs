using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.BCSV;
using HeavenTool.IO.UndoSystem;
using HeavenTool.Avalonia.Views.FileEditors;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public partial class BCSVViewModel : ObservableObject
{
    private readonly BCSVWindow window;
    private readonly UndoManager undoManager = new();
    private BinaryCSV? loadedFile;
    private IStorageFile? sourceFile;
    private string? localPath;
    private string fileName = "BCSV Editor";
    private bool isEditing;
    public DataGrid DataGrid { get; }
    public BinaryCSV? LoadedFile => loadedFile;
    public bool IsFileLoaded => loadedFile != null;
    public AvaloniaList<ColumnVisibilityModel> Columns { get; } = [];
    /// <summary>The document's rows in file order; <see cref="LoadedFile"/> receives them when saving.</summary>
    public AvaloniaList<BCSVRow> Rows { get; private set; } = [];

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasUnsavedChanges;
    [ObservableProperty] private string statusText = "Open a BCSV file to begin.";
    [ObservableProperty] private string? errorMessage;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public BCSVViewModel(BCSVWindow window)
    {
        this.window = window;
        DataGrid = window.FindControl<DataGrid>("MainDataGrid")!;
        DataGrid.CellEditEnding += (_, e) =>
        {
            if (e.Column is not HeavenDataGridColumn column) return;
            if (e.EditAction == DataGridEditAction.Commit && !column.CommitPendingEdit())
            {
                e.Cancel = true;
                return;
            }
            if (column.TryTakePendingEdit(out var row, out var value) && e.EditAction == DataGridEditAction.Commit)
                ApplyEdit(row, column, value);
        };
        DataGrid.PreparingCellForEdit += (_, _) => SetEditing(true);
        DataGrid.CellEditEnded += (_, _) => SetEditing(false);
        DataGrid.SelectionChanged += (_, _) => RefreshCommands();
        undoManager.Changed += (_, _) =>
        {
            HasUnsavedChanges = !undoManager.IsAtSavePoint;
            RefreshCommands();
        };
        InitializeSearch();
        InitializeClipboard();
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    partial void OnHasUnsavedChangesChanged(bool value)
    {
        UpdateTitle();
        SaveFileCommand.NotifyCanExecuteChanged();
    }
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    private void UpdateTitle() => window.Title = $"{fileName}{(HasUnsavedChanges ? " *" : "")} — Heaven Tool";
    private bool CanOpen() => !IsBusy;
    private bool CanSave() => IsFileLoaded && !IsBusy;
    // An open cell edit counts: saving commits it first.
    private bool CanSaveFile() => CanSave() && (HasUnsavedChanges || isEditing);
    private bool CanUndo() => CanSave() && !isEditing && undoManager.CanUndo;
    private bool CanRedo() => CanSave() && !isEditing && undoManager.CanRedo;
    private bool CanDuplicateRows() => CanSave() && DataGrid.SelectedItems.Count > 0;
    // Delete belongs to the cell's text box while editing.
    private bool CanDeleteRows() => CanDuplicateRows() && !isEditing;

    private void RefreshCommands()
    {
        OpenFileCommand.NotifyCanExecuteChanged();
        SaveFileCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseFileCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        NewRowCommand.NotifyCanExecuteChanged();
        DuplicateRowsCommand.NotifyCanExecuteChanged();
        DeleteRowsCommand.NotifyCanExecuteChanged();
        OpenSearchCommand.NotifyCanExecuteChanged();
        FindNextCommand.NotifyCanExecuteChanged();
        FindPreviousCommand.NotifyCanExecuteChanged();
        OpenReplaceCommand.NotifyCanExecuteChanged();
        ReplaceNextCommand.NotifyCanExecuteChanged();
        ReplaceAllCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CopyWithHeadersCommand.NotifyCanExecuteChanged();
        CopyCellCommand.NotifyCanExecuteChanged();
        ImportRowsCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportSelectionCommand.NotifyCanExecuteChanged();
        ExportColumnValuesCommand.NotifyCanExecuteChanged();
    }

    private void SetEditing(bool value)
    {
        isEditing = value;
        RefreshCommands();
    }

    public bool? ToggleAll
    {
        get => Columns.Count == 0 ? false : Columns.All(c => c.IsVisible) ? true : Columns.All(c => !c.IsVisible) ? false : null;
        set
        {
            if (value == null) return;
            foreach (var column in Columns) column.IsVisible = value.Value;
            OnPropertyChanged();
        }
    }

    private static FilePickerFileType BcsvFileType { get; } = new("BCSV") { Patterns = ["*.bcsv"] };

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenFile()
    {
        try
        {
            if (!await ConfirmChangesAsync()) return;
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false, FileTypeFilter = [BcsvFileType], Title = "Open a BCSV file..."
            });
            if (files.Count == 0) return;
            var selected = files[0];
            await LoadAsync(() => ReadFileAsync(selected), selected.Name, selected);
        }
        catch (Exception ex) { ErrorMessage = $"Could not open file: {ex.Message}"; }
    }

    public Task<bool> LoadDataFromFileAsync(byte[] bytes, string name = "Untitled.bcsv") =>
        LoadAsync(() => Task.FromResult(bytes), name, null);

    private static async Task<byte[]> ReadFileAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        if (stream.CanSeek)
        {
            var bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes);
            return bytes;
        }
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private async Task<bool> LoadAsync(Func<Task<byte[]>> read, string name, IStorageFile? source, bool unsaved = false)
    {
        if (IsBusy) return false;
        IsBusy = true;
        ErrorMessage = null;
        StatusText = $"Loading {name}…";
        var timer = Stopwatch.StartNew();
        try
        {
            var bytes = await read();
            var (loaded, rows) = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, writable: false);
                var file = new BinaryCSV(stream);
                return (file, new AvaloniaList<BCSVRow>(file.Entries.Select(values => new BCSVRow(values))));
            });
            // Leave the previous document intact if reading/parsing fails.
            ClearGrid();
            CreateDataGridColumns(loaded.Fields, loaded.UniqueField);
            loadedFile = loaded;
            sourceFile = source;
            localPath = source?.TryGetLocalPath();
            fileName = name;
            Rows = rows;
            DataGrid.ItemsSource = rowsView = new DataGridCollectionView(rows);
            hiddenRows.Clear();
            filterKey = null;
            undoManager.Clear(atSavePoint: !unsaved);
            currentMatch = null;
            InvalidateSearch();
            UpdateTitle();
            OnPropertyChanged(nameof(LoadedFile));
            OnPropertyChanged(nameof(IsFileLoaded));
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(ToggleAll));
            StatusText = $"{loaded.Length:N0} rows · {loaded.Fields.Length:N0} columns · loaded in {timer.Elapsed.TotalMilliseconds:N0} ms";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load {name}: {ex.Message}";
            StatusText = "Loading failed.";
            return false;
        }
        finally { IsBusy = false; }
    }

    private void CreateDataGridColumns(Field[] fields, Field? uniqueField)
    {
        var visibility = new List<ColumnVisibilityModel>(fields.Length);
        DataGrid.BeginInit();
        try
        {
            for (int index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                HeavenDataGridColumn column = field.DataType switch
                {
                    // -1 stands for the unsigned maximum, as the game writes it.
                    DataType.UInt16 => new DataGridNumericColumn(-1, ushort.MaxValue),
                    DataType.Int16 => new DataGridNumericColumn(short.MinValue, short.MaxValue),
                    DataType.UInt32 => new DataGridNumericColumn(-1, uint.MaxValue),
                    DataType.Int32 => new DataGridNumericColumn(int.MinValue, int.MaxValue),
                    DataType.S8 => new DataGridNumericColumn(sbyte.MinValue, sbyte.MaxValue),
                    DataType.U8 => new DataGridNumericColumn(byte.MinValue, byte.MaxValue),
                    DataType.CRC32 or DataType.MMH3
                        => new DataGridHashColumn(field.Hash, field.DataType == DataType.MMH3),
                    _ => new DataGridStringColumn { MaxLength = field.DataType == DataType.String ? Math.Max(0, field.Size - 1) : 0 }
                };
                column.FieldIndex = index;
                column.FieldType = field.DataType;
                column.FieldSize = field.Size;
                column.Header = field.DisplayName;
                column.Tag = index;
                column.IsReadOnly = field.DataType == DataType.BitField;
                column.Highlights = highlights;
                column.HeaderKind = field == uniqueField ? FieldHeaderKind.UniqueId
                    : !field.IsMissingHash ? FieldHeaderKind.Normal
                    : field.Size == 4 ? FieldHeaderKind.UnknownName : FieldHeaderKind.UnknownShortName;
                column.HeaderTip = (field.GetTranslatedNameOrNull() is { } translated ? $"Name: {translated}\n" : "")
                    + $"Hash: 0x{field.Hash:x8}\nType: {field.DataType}\nSize: {field.Size}\nIndex: {index}";
                // Fixed initial widths/heights keep offscreen cells out of the measurement pass.
                column.Width = new DataGridLength(Math.Clamp(field.DisplayName.Length * 7.5 + 28, 90, 240));
                var model = new ColumnVisibilityModel(index, field.DisplayName);
                model.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(ColumnVisibilityModel.IsVisible)) return;
                    column.IsVisible = model.IsVisible;
                    OnPropertyChanged(nameof(ToggleAll));
                    InvalidateSearch();
                };
                visibility.Add(model);
                DataGrid.Columns.Add(column);
            }
            Columns.AddRange(visibility);
            // The unique ID leads, as in the WinForms editor.
            if (uniqueField != null) DataGrid.Columns[Array.IndexOf(fields, uniqueField)].DisplayIndex = 0;
        }
        finally { DataGrid.EndInit(); }
    }

    private bool CommitEdits()
    {
        if (DataGrid.CommitEdit(DataGridEditingUnit.Row, true)) return true;
        ErrorMessage = "Correct the current cell's value before saving or closing the file.";
        return false;
    }

    private static string RowCount(int count) => count == 1 ? "1 row" : $"{count:N0} rows";

    private void ApplyEdit(BCSVRow row, HeavenDataGridColumn column, object value)
    {
        // Editing a cell inside a multi-row selection sets that field on every selected row.
        IEnumerable<BCSVRow> targets = DataGrid.SelectedItems.Count > 1 && DataGrid.SelectedItems.Contains(row)
            ? DataGrid.SelectedItems.OfType<BCSVRow>() : [row];
        int field = column.FieldIndex;
        var changes = targets.Where(target => !Equals(target[field], value))
            .Select(target => new CellChange(target, field, target[field], value)).ToList();
        if (changes.Count == 0) return;
        ErrorMessage = null;
        undoManager.Execute(new SetCellsCommand(changes));
        StatusText = changes.Count == 1 ? $"Edited {column.Header}" : $"Set {column.Header} on {RowCount(changes.Count)}";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => StepHistory(undo: true);

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => StepHistory(undo: false);

    private void StepHistory(bool undo)
    {
        // The grid updates its selection per removed row, so drop it before rows can leave.
        DataGrid.SelectedItems.Clear();
        var command = undo ? undoManager.Undo() : undoManager.Redo();
        if (command is not IBCSVCommand step) return;
        SelectRows([.. step.RowsToSelect(undo)]);
        StatusText = $"{(undo ? "Undone" : "Redone")} · {RowCount(Rows.Count)}";
    }

    // Each SelectedItems.Add rescans the whole selection, so a large partial selection keeps only its first row.
    private const int MaxReselectedRows = 1000;

    private void SelectRows(List<BCSVRow> rows)
    {
        if (rows.Count == 0) return;
        ShowRows(rows);
        DataGrid.SelectedItems.Clear();
        if (rows.Count == Rows.Count) DataGrid.SelectAll();
        else foreach (var row in rows.Count <= MaxReselectedRows ? rows : rows[..1]) DataGrid.SelectedItems.Add(row);
        DataGrid.ScrollIntoView(rows[0], DataGrid.CurrentColumn);
    }

    /// <summary>Selected rows with their file positions, in file order.</summary>
    private List<(int Index, BCSVRow Row)> SelectedRows()
    {
        var selected = DataGrid.SelectedItems.OfType<BCSVRow>().ToHashSet();
        var result = new List<(int Index, BCSVRow Row)>(selected.Count);
        for (int i = 0; i < Rows.Count && result.Count < selected.Count; i++)
            if (selected.Contains(Rows[i])) result.Add((i, Rows[i]));
        return result;
    }

    // Numeric unique IDs continue after the highest one in use. An unsigned field's MaxValue is
    // the game's "-1", so it is neither counted nor handed out.
    private Func<object?>? CreateUniqueIdSource(out int fieldIndex)
    {
        fieldIndex = loadedFile?.UniqueField is { } unique ? Array.IndexOf(loadedFile.Fields, unique) : -1;
        if (fieldIndex < 0) return null;
        var type = loadedFile!.Fields[fieldIndex].DataType;
        long max = type switch
        {
            DataType.S8 => sbyte.MaxValue,
            DataType.U8 => byte.MaxValue - 1,
            DataType.Int16 => short.MaxValue,
            DataType.UInt16 => ushort.MaxValue - 1,
            DataType.Int32 => int.MaxValue,
            DataType.UInt32 => uint.MaxValue - 1,
            _ => -1
        };
        if (max < 0) return null;
        int index = fieldIndex;
        long next = Rows.Select(row => Convert.ToInt64(row[index])).Where(id => id <= max).DefaultIfEmpty(-1).Max() + 1;
        return () => next > max ? null : Convert.ChangeType(next++, type.ToType());
    }

    private void AddRows(List<object[]> entries, string action)
    {
        var items = entries.Select((values, offset) => (Rows.Count + offset, new BCSVRow(values))).ToList();
        undoManager.Execute(new ChangeRowsCommand(Rows, items, remove: false));
        SelectRows([.. items.Select(item => item.Item2)]);
        StatusText = $"{action} · {RowCount(Rows.Count)}";
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void NewRow()
    {
        if (!CommitEdits()) return;
        var values = loadedFile!.Fields.Select(field => field.GetFieldDefaultValue()).ToArray();
        if (CreateUniqueIdSource(out int idField)?.Invoke() is { } id) values[idField] = id;
        AddRows([values], "Added a row");
    }

    [RelayCommand(CanExecute = nameof(CanDuplicateRows))]
    private void DuplicateRows()
    {
        if (!CommitEdits()) return;
        var source = SelectedRows();
        if (source.Count == 0) return;
        var nextId = CreateUniqueIdSource(out int idField);
        var copies = source.Select(item =>
        {
            var values = item.Row.Values.Select(value => value is byte[] bytes ? bytes.Clone() : value).ToArray();
            if (nextId?.Invoke() is { } id) values[idField] = id;
            return values;
        }).ToList();
        AddRows(copies, $"Duplicated {RowCount(copies.Count)}");
    }

    [RelayCommand(CanExecute = nameof(CanDeleteRows))]
    private void DeleteRows()
    {
        var items = SelectedRows();
        if (items.Count == 0) return;
        DataGrid.SelectedItems.Clear();
        undoManager.Execute(new ChangeRowsCommand(Rows, items, remove: true));
        StatusText = $"Deleted {RowCount(items.Count)} · Ctrl+Z to undo";
    }

    [RelayCommand(CanExecute = nameof(CanSaveFile))]
    private async Task SaveFile()
    {
        if (localPath == null && sourceFile == null) await SaveAs();
        else await SaveAsync(localPath, sourceFile);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAs()
    {
        try
        {
            if (!CommitEdits()) return;
            var destination = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save BCSV as...", SuggestedFileName = fileName,
                DefaultExtension = "bcsv", FileTypeChoices = [BcsvFileType], ShowOverwritePrompt = true
            });
            if (destination != null) await SaveAsync(destination.TryGetLocalPath(), destination);
        }
        catch (Exception ex) { ErrorMessage = $"Could not save file: {ex.Message}"; }
    }

    public Task<bool> SaveToFileAsync(string path) => SaveAsync(path, null);

    private async Task<bool> SaveAsync(string? path, IStorageFile? destination)
    {
        if (!CanSave() || !CommitEdits()) return false;
        string name = path != null ? Path.GetFileName(path) : destination!.Name;
        IsBusy = true;
        ErrorMessage = null;
        StatusText = $"Saving {name}…";
        try
        {
            loadedFile!.Entries = [.. Rows.Select(row => row.Values)];
            var bytes = await Task.Run(loadedFile.Save);
            if (path != null) path = Path.GetFullPath(path);
            await WriteFileAsync(path, destination, bytes);
            sourceFile = destination;
            localPath = path;
            fileName = name;
            undoManager.MarkSaved();
            HasUnsavedChanges = false;
            UpdateTitle();
            StatusText = $"Saved {name} · {RowCount(Rows.Count)}";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not save {name}: {ex.Message}";
            StatusText = "Saving failed; your edits are still open.";
            return false;
        }
        finally { IsBusy = false; }
    }

    /// <summary>Writes a local file through a temporary copy, so a failed write leaves the old one intact.</summary>
    private static async Task WriteFileAsync(string? path, IStorageFile? destination, byte[] bytes)
    {
        if (path != null)
        {
            path = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        else
        {
            await using var output = await destination!.OpenWriteAsync();
            output.SetLength(0);
            await output.WriteAsync(bytes);
            await output.FlushAsync();
        }
    }

    public async Task<bool> ConfirmChangesAsync()
    {
        if (IsBusy || !CommitEdits()) return false;
        if (!HasUnsavedChanges) return true;
        bool? choice = await window.ConfirmSaveChangesAsync();
        if (choice == null) return false;
        if (!choice.Value) return true;
        await SaveFile();
        return !HasUnsavedChanges;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task CloseFile()
    {
        if (!await ConfirmChangesAsync()) return;
        ClearGrid();
        loadedFile = null;
        sourceFile = null;
        localPath = null;
        fileName = "BCSV Editor";
        Rows = [];
        undoManager.Clear();
        IsSearchOpen = false;
        UpdateTitle();
        OnPropertyChanged(nameof(LoadedFile));
        OnPropertyChanged(nameof(IsFileLoaded));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(ToggleAll));
        RefreshCommands();
        StatusText = "Open a BCSV file to begin.";
        ErrorMessage = null;
    }

    private void ClearGrid()
    {
        DataGrid.ItemsSource = rowsView = null;
        foreach (var column in DataGrid.Columns.OfType<HeavenDataGridColumn>()) column.Dispose();
        DataGrid.Columns.Clear();
        Columns.Clear();
    }
}
