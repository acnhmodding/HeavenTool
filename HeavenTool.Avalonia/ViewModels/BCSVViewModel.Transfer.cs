using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.BCSV;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public partial class BCSVViewModel
{
    private static FilePickerFileType CsvFileType { get; } = new("CSV") { Patterns = ["*.csv"] };
    private static FilePickerFileType TextFileType { get; } = new("Text") { Patterns = ["*.txt"] };
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private string BaseName => Path.GetFileNameWithoutExtension(fileName);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task ImportRows()
    {
        try
        {
            if (!CommitEdits()) return;
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false, FileTypeFilter = [BcsvFileType], Title = "Import rows from a BCSV file..."
            });
            if (files.Count == 0) return;
            await ImportRowsAsync(await ReadFileAsync(files[0]), files[0].Name);
        }
        catch (Exception ex) { ErrorMessage = $"Could not import rows: {ex.Message}"; }
    }

    /// <summary>Appends another file's rows as one undoable step, matching fields by hash.</summary>
    public async Task<bool> ImportRowsAsync(byte[] bytes, string name)
    {
        if (loadedFile == null || IsBusy || !CommitEdits()) return false;
        BinaryCSV source;
        IsBusy = true;
        try
        {
            source = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, writable: false);
                return new BinaryCSV(stream);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not import {name}: {ex.Message}";
            return false;
        }
        finally { IsBusy = false; }

        var fields = loadedFile.Fields;
        var sourceIndexes = new Dictionary<uint, int>();
        for (int i = 0; i < source.Fields.Length; i++) sourceIndexes.TryAdd(source.Fields[i].Hash, i);
        int[] map = [.. fields.Select(field => sourceIndexes.GetValueOrDefault(field.Hash, -1))];
        if (map.All(index => index < 0))
        {
            ErrorMessage = $"{name} has no columns in common with this file.";
            return false;
        }
        if (source.Entries.Count == 0)
        {
            ErrorMessage = $"{name} has no rows to import.";
            return false;
        }

        // Rows without the unique ID get fresh ones; imported IDs are kept as they are.
        var nextId = CreateUniqueIdSource(out int idField);
        if (idField >= 0 && map[idField] >= 0) nextId = null;
        int freshIdField = nextId != null ? idField : -1;
        var rejected = new int[fields.Length];
        var entries = new List<object[]>(source.Entries.Count);
        foreach (var entry in source.Entries)
        {
            var values = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                if (map[i] >= 0 && TryAdapt(entry[map[i]], fields[i], out var value)) values[i] = value;
                else
                {
                    if (map[i] >= 0) rejected[i]++;
                    values[i] = fields[i].GetFieldDefaultValue();
                }
            }
            if (nextId?.Invoke() is { } id) values[idField] = id;
            entries.Add(values);
        }
        ErrorMessage = null;
        AddRows(entries, $"Imported {RowCount(entries.Count)} from {name}");

        var report = new StringBuilder();
        void Section(string title, IEnumerable<string> lines)
        {
            var list = lines.ToList();
            if (list.Count == 0) return;
            report.AppendLine(title);
            foreach (var line in list) report.AppendLine($"  • {line}");
            report.AppendLine();
        }
        var shared = map.Where(index => index >= 0).ToHashSet();
        Section($"Columns in {name} that this file doesn't have (not imported):",
            source.Fields.Where((_, index) => !shared.Contains(index)).Select(field => field.DisplayName));
        Section($"Columns missing from {name} (filled with defaults):",
            fields.Where((_, index) => map[index] < 0 && index != freshIdField).Select(field => field.DisplayName));
        Section("Values that don't fit this file's column type (replaced with defaults):",
            fields.Select((field, index) => (field, count: rejected[index])).Where(item => item.count > 0)
                .Select(item => $"{item.field.DisplayName}: {RowCount(item.count)}"));
        if (idField >= 0 && map[idField] >= 0)
        {
            var existing = Rows.Take(Rows.Count - entries.Count).Select(row => row[idField]).ToHashSet();
            int reused = entries.Count(values => existing.Contains(values[idField]));
            if (reused > 0)
                report.AppendLine($"{RowCount(reused)} reuse a {fields[idField].DisplayName} that this file already has.");
        }
        if (report.Length > 0) await window.ShowMessageAsync("Import completed with warnings", report.ToString().TrimEnd());
        return true;
    }

    // Matching fields usually share a type; otherwise numbers convert when they fit.
    private static bool TryAdapt(object value, Field field, out object result)
    {
        result = value;
        switch (field.DataType)
        {
            case DataType.BitField:
                if (value is not byte[] bits || bits.Length != field.Size) return false;
                result = bits.Clone();
                return true;
            case DataType.String:
                return value is string text && !text.Contains('\0') && Encoding.UTF8.GetByteCount(text) < field.Size;
            case DataType.Float32:
                if (value is string or byte[]) return false;
                result = Convert.ToSingle(value);
                return true;
            // The game's "-1" stays -1 between unsigned sizes.
            case DataType.UInt16 or DataType.UInt32 when value is ushort.MaxValue or uint.MaxValue:
                result = field.DataType == DataType.UInt16 ? ushort.MaxValue : uint.MaxValue;
                return true;
            default:
                if (value is string or byte[] or float) return false;
                try
                {
                    result = Convert.ChangeType(value, field.DataType.ToType());
                    return true;
                }
                catch (OverflowException) { return false; }
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task ExportCsv() => PickAndExportAsync("Export as CSV...", $"{BaseName}.csv", CsvFileType, ExportCsvAsync);

    /// <summary>Writes the visible columns of every row, as the grid shows and orders them.</summary>
    public Task<bool> ExportCsvAsync(string? path, IStorageFile? destination = null)
    {
        if (!CanExport()) return Task.FromResult(false);
        var columns = VisibleColumns();
        var rows = RowsInView();
        return ExportAsync(path, destination, () =>
        {
            var text = new StringBuilder();
            AppendCsvLine(text, columns.Select(column => column.Header?.ToString() ?? ""));
            foreach (var row in rows) AppendCsvLine(text, columns.Select(column => column.FormatValue(row[column.FieldIndex])));
            // The byte order mark lets spreadsheet apps read the text as UTF-8.
            return [.. Utf8WithBom.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
        }, $"Exported {RowCount(rows.Count)} to CSV");
    }

    private static void AppendCsvLine(StringBuilder text, IEnumerable<string> values)
    {
        text.AppendJoin(',', values.Select(value =>
            value.AsSpan().IndexOfAny(",\"\r\n") < 0 && value.Trim().Length == value.Length ? value
                : $"\"{value.Replace("\"", "\"\"")}\""));
        text.Append("\r\n");
    }

    [RelayCommand(CanExecute = nameof(CanDuplicateRows))]
    private Task ExportSelection() =>
        PickAndExportAsync("Export selected rows...", $"{BaseName}_selection.bcsv", BcsvFileType, ExportSelectionAsync);

    /// <summary>Saves the selected rows, in file order, to a new file with this file's fields.</summary>
    public Task<bool> ExportSelectionAsync(string? path, IStorageFile? destination = null)
    {
        if (!CanExport()) return Task.FromResult(false);
        var selection = BinaryCSV.CopyFileWithoutEntries(loadedFile!);
        selection.Entries = [.. SelectedRows().Select(item => item.Row.Values)];
        return ExportAsync(path, destination, selection.Save, $"Exported {RowCount(selection.Entries.Count)} to a new file");
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task ExportColumnValues(HeavenDataGridColumn? column)
    {
        column ??= DataGrid.CurrentColumn as HeavenDataGridColumn;
        if (column == null)
        {
            ErrorMessage = "Select a cell in the column to export.";
            return Task.CompletedTask;
        }
        return PickAndExportAsync($"Export {column.Header} values...", $"{column.Header}.txt", TextFileType,
            (path, destination) => ExportColumnValuesAsync(column, path, destination));
    }

    /// <summary>Writes one value per row in view order; hash columns list each label once, as a value list.</summary>
    public Task<bool> ExportColumnValuesAsync(HeavenDataGridColumn column, string? path, IStorageFile? destination = null)
    {
        if (!CanExport()) return Task.FromResult(false);
        var values = RowsInView().Select(row => column.FormatValue(row[column.FieldIndex]));
        if (column.FieldType is DataType.CRC32 or DataType.MMH3) values = values.Distinct();
        var lines = values.ToList();
        return ExportAsync(path, destination, () => Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine),
            $"Exported {lines.Count:N0} {column.Header} values");
    }

    private List<BCSVRow> RowsInView() => [.. ((System.Collections.IEnumerable?)DataGrid.CollectionView ?? Rows).OfType<BCSVRow>()];

    private async Task PickAndExportAsync(string title, string suggestedName, FilePickerFileType type,
        Func<string?, IStorageFile?, Task<bool>> export)
    {
        try
        {
            if (!CommitEdits()) return;
            var destination = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title, SuggestedFileName = suggestedName,
                DefaultExtension = type.Patterns![0][2..], FileTypeChoices = [type], ShowOverwritePrompt = true
            });
            if (destination != null) await export(destination.TryGetLocalPath(), destination);
        }
        catch (Exception ex) { ErrorMessage = $"Could not export: {ex.Message}"; }
    }

    // An open cell edit is committed first, so the export includes it.
    private bool CanExport() => CanSave() && CommitEdits();

    // Exports leave the document, its path and its unsaved state alone.
    private async Task<bool> ExportAsync(string? path, IStorageFile? destination, Func<byte[]> build, string status)
    {
        IsBusy = true;
        try
        {
            await WriteFileAsync(path, destination, await Task.Run(build));
            ErrorMessage = null;
            StatusText = status;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not export: {ex.Message}";
            return false;
        }
        finally { IsBusy = false; }
    }
}
