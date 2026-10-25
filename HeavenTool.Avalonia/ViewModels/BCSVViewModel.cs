using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.BCSV;
using HeavenTool.Avalonia.Views.FileEditors;
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
    private BinaryCSV? loadedFile;
    private IStorageFile? sourceFile;
    private string? localPath;
    private string fileName = "BCSV Editor";
    public DataGrid DataGrid { get; }
    public BinaryCSV? LoadedFile => loadedFile;
    public bool IsFileLoaded => loadedFile != null;
    public AvaloniaList<ColumnVisibilityModel> Columns { get; } = [];

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
            if (e.EditAction == DataGridEditAction.Commit && e.Column is HeavenDataGridColumn column
                && !column.CommitPendingEdit()) e.Cancel = true;
        };
        DataGrid.CellEditEnded += (_, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit) HasUnsavedChanges = true;
        };
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    partial void OnHasUnsavedChangesChanged(bool value) => UpdateTitle();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    private void UpdateTitle() => window.Title = $"{fileName}{(HasUnsavedChanges ? " *" : "")} — Heaven Tool";
    private bool CanOpen() => !IsBusy;
    private bool CanSave() => IsFileLoaded && !IsBusy;
    private void RefreshCommands()
    {
        OpenFileCommand.NotifyCanExecuteChanged();
        SaveFileCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseFileCommand.NotifyCanExecuteChanged();
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

    private async Task<bool> LoadAsync(Func<Task<byte[]>> read, string name, IStorageFile? source)
    {
        if (IsBusy) return false;
        IsBusy = true;
        ErrorMessage = null;
        StatusText = $"Loading {name}…";
        var timer = Stopwatch.StartNew();
        try
        {
            var bytes = await read();
            var loaded = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, writable: false);
                return new BinaryCSV(stream);
            });
            // Leave the previous document intact if reading/parsing fails.
            ClearGrid();
            CreateDataGridColumns(loaded.Fields);
            loadedFile = loaded;
            sourceFile = source;
            localPath = source?.TryGetLocalPath();
            fileName = name;
            DataGrid.ItemsSource = loaded.Entries;
            HasUnsavedChanges = false;
            UpdateTitle();
            OnPropertyChanged(nameof(LoadedFile));
            OnPropertyChanged(nameof(IsFileLoaded));
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

    private void CreateDataGridColumns(Field[] fields)
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
                    DataType.UInt16 => new DataGridNumericColumn(ushort.MinValue, ushort.MaxValue),
                    DataType.Int16 => new DataGridNumericColumn(short.MinValue, short.MaxValue),
                    DataType.UInt32 => new DataGridNumericColumn(uint.MinValue, uint.MaxValue),
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
                column.SortMemberPath = $"[{index}]";
                column.IsReadOnly = field.DataType == DataType.BitField;
                // Fixed initial widths/heights keep offscreen cells out of the measurement pass.
                column.Width = new DataGridLength(Math.Clamp(field.DisplayName.Length * 7.5 + 28, 90, 240));
                var model = new ColumnVisibilityModel(index, field.DisplayName);
                model.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(ColumnVisibilityModel.IsVisible)) return;
                    column.IsVisible = model.IsVisible;
                    OnPropertyChanged(nameof(ToggleAll));
                };
                visibility.Add(model);
                DataGrid.Columns.Add(column);
            }
            Columns.AddRange(visibility);
        }
        finally { DataGrid.EndInit(); }
    }

    private bool CommitEdits()
    {
        if (DataGrid.CommitEdit(DataGridEditingUnit.Row, true)) return true;
        ErrorMessage = "Correct the current cell's value before saving or closing the file.";
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
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
            var bytes = await Task.Run(loadedFile!.Save);
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
            sourceFile = destination;
            localPath = path;
            fileName = name;
            HasUnsavedChanges = false;
            UpdateTitle();
            StatusText = $"Saved {name} · {LoadedFile!.Length:N0} rows";
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
        HasUnsavedChanges = false;
        UpdateTitle();
        OnPropertyChanged(nameof(LoadedFile));
        OnPropertyChanged(nameof(IsFileLoaded));
        OnPropertyChanged(nameof(ToggleAll));
        RefreshCommands();
        StatusText = "Open a BCSV file to begin.";
        ErrorMessage = null;
    }

    private void ClearGrid()
    {
        DataGrid.ItemsSource = null;
        foreach (var column in DataGrid.Columns.OfType<HeavenDataGridColumn>()) column.Dispose();
        DataGrid.Columns.Clear();
        Columns.Clear();
    }

    public void HideColumn(object source)
    {
        if (source is not Control control) return;
        while (control is not DataGridColumnHeader && control.Parent is Control parent) control = parent;
        if (control is DataGridColumnHeader header && DataGridColumn.GetColumnContainingElement(header)?.Tag is int index)
            Columns[index].IsVisible = false;
    }
}
