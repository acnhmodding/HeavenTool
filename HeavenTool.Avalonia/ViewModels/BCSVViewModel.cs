using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.Controls;
using HeavenTool.Avalonia.FileTypes.BCSV;
using HeavenTool.Avalonia.Views.FileEditors;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public partial class BCSVViewModel(BCSVWindow window) : ObservableObject
{
    public DataGrid? DataGrid { get; set; } = window.FindControl<DataGrid>("MainDataGrid");
    public Grid? ContentGrid { get; set; } = window.FindControl<Grid>("ContentGrid");

    //private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
    public ObservableCollection<ColumnVisibilityModel> Columns { get; } = [];

    //public List<object[]> DataRows { get; set; } = [];
    public IEnumerable<EnumItemTuple> DataTypes { get; set; } = Enum.GetValues<DataType>().Select(x =>
        new EnumItemTuple()
        {
            EnumName = x.ToString(),
            Value = x
        }
    );

    public void SelectDataType(object obj)
    {
        if (obj == null || IsFileLoaded || LoadedFile == null) return;

        if (obj is ReadOnlyCollection<object> objArray && objArray.Count > 1 && 
            objArray[0] is DataType selectedType && objArray[1] is DataGridColumnHeader header)
        {
            Console.WriteLine("[Array] Selected type: {0}", selectedType);
            Console.WriteLine("[Array] Selected header: {0}", header);

            var column = DataGridColumn.GetColumnContainingElement(header);
            if (column != null && column.Tag is int fieldIndex)
            {
                var selectedField = LoadedFile.Fields[fieldIndex];
                Console.WriteLine("[Array] Selected field: {0}", selectedField.DisplayName);

                if (selectedField.IsMissingHash)
                {
                    //TODO: Create this function that adds to KnownHashes property and save to the text file 
                    //HashManager.AddKnownType(selectedField.Hash, selectedType);
                }

                // TODO: Disable this button on Fields that already have a translated name
            }


        }
    }


    public bool? ToggleAll
    {
        get
        {
            if (Columns.Count == 0)
                return false;

            bool first = Columns[0].IsVisible;

            if (Columns.All(c => c.IsVisible == first))
                return first;

            return null;
        }

        set
        {
            if (value == null) return;

            foreach (var col in Columns)
                col.IsVisible = value.Value;

            OnPropertyChanged();
        }
    }

    partial void OnLoadedFileChanged(BinaryCSV? value)
    {
        OnPropertyChanged(nameof(IsFileLoaded));
    }

    public void UpdateToggleState()
    {
        OnPropertyChanged(nameof(ToggleAll));
    }

    [ObservableProperty]
    private BinaryCSV? loadedFile;

    public bool IsFileLoaded => LoadedFile != null;

    public async void LoadDataFromFile(byte[] bytes)
    {
        if (LoadedFile != null)
        {
            CloseFile();
            await Task.Yield();
        }

        Console.WriteLine("[BCSV] Loading data...");

        using var stream = new MemoryStream(bytes);
        LoadedFile = new BinaryCSV(stream);


        CreateDataGridColumns(LoadedFile.Fields);
        UpdateToggleState();

        //for (uint entryId = 0; entryId < LoadedFile.Length; entryId++)
        //{
        //    //var row = new DynamicRow();
        //    var row = new object[LoadedFile.Fields.Length];
        //    for (ushort fieldId = 0; fieldId < LoadedFile.Fields.Length; fieldId++)
        //        row[fieldId] = LoadedFile.Entries[entryId, fieldId];

        //    DataRows.Add(row);
        //}

        //OnPropertyChanged(nameof(DataRows));
    }

    public void CreateDataGridColumns(Field[] fields)
    {
        if (DataGrid == null) return;

        DataGrid.Columns.Clear();
        Columns.Clear();

        for (int fieldId = 0; fieldId < fields.Length; fieldId++)
        {
            Field field = fields[fieldId];

            var columnModel = new ColumnVisibilityModel(fieldId, field.DisplayName);

            columnModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ColumnVisibilityModel.IsVisible))
                {
                    DataGrid?.Columns[columnModel.Index].IsVisible = columnModel.IsVisible;

                    OnPropertyChanged(nameof(ToggleAll));
                }
            };

            Columns.Add(columnModel);

            HeavenDataGridColumn column = field.DataType switch
            {
                DataType.String => new DataGridStringColumn()
                {
                    MaxLength = field.Size
                },

                DataType.CRC32 => new DataGridHashColumn(field.Hash),
                DataType.MMH3 => new DataGridHashColumn(field.Hash, true),

                DataType.UInt16 => new DataGridNumericColumn(ushort.MinValue, ushort.MaxValue),
                DataType.Int16 => new DataGridNumericColumn(short.MinValue, short.MaxValue),

                DataType.UInt32 => new DataGridNumericColumn(uint.MinValue, uint.MaxValue),
                DataType.Int32 => new DataGridNumericColumn(int.MinValue, int.MaxValue),

                DataType.S8 => new DataGridNumericColumn(sbyte.MinValue, sbyte.MaxValue),
                DataType.U8 => new DataGridNumericColumn(byte.MinValue, byte.MaxValue),

                //DataType.Float32 => throw new NotImplementedException(),
                //DataType.Float64 => throw new NotImplementedException(),
                //DataType.MMH3 => throw new NotImplementedException(),
                //DataType.U8Array => throw new NotImplementedException(),
                //DataType.S8Array => throw new NotImplementedException(),

                _ => new DataGridStringColumn(),
            };

            column.Width = DataGridLength.SizeToHeader;
            column.Header = field.DisplayName;
            column.Binding = new ReflectionBinding($"[{fieldId}]");
            column.Tag = fieldId;

            DataGrid.Columns.Add(column);
            DataGrid.CanUserResizeColumns = true;
        }
    }

    public static FilePickerFileType BCSV { get; } = new("BCSV")
    {
        Patterns = ["*.bcsv"]
    };

    public static DataGridColumnHeader? GetColumnHeaderFromControl(object obj)
    {
        if (obj is DataGridColumnHeader header) return header;
        if (obj == null || obj is not Control control || control.Parent == null) return null;

        return GetColumnHeaderFromControl(control.Parent);
    }

    public void HideColumn(object obj)
    {
        var header = GetColumnHeaderFromControl(obj);

        if (header == null) return;

        var column = DataGridColumn.GetColumnContainingElement(header);

        if (column != null)
        {
            column.IsVisible = false;
            UpdateToggleState();
        }
    }


    public void CloseFile()
    {
        //DataRows.Clear();
        //OnPropertyChanged(nameof(DataRows));

        if (DataGrid != null)
        {
            foreach (DataGridColumn column in DataGrid.Columns)
                if (column is HeavenDataGridColumn c)
                    c.Dispose();

            
            DataGrid.Columns.Clear();
        }

        LoadedFile = null;
    }

    [RelayCommand]
    public async Task OpenFile()
    {
        var result = await new Window().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = [BCSV],
            Title = "Open a BCSV file..."
        });

        if (result != null && result.Count >= 1)
        {
            Console.WriteLine("[BCSV] Opening file: {0}", result[0].Name);
            await using var stream = await result[0].OpenReadAsync();
            using MemoryStream ms = new();
            stream.CopyTo(ms);

            LoadDataFromFile(ms.ToArray());
        }
    }

    public void LoadBCSVHashes() => HashManager.InitializeHashes();
}