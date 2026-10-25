using Avalonia.Controls;
using Avalonia.Data;
using System;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

// TODO: Add right click function to hide column or do other functions
public abstract class HeavenDataGridColumn : DataGridColumn, IDisposable
{
    public BindingBase? Binding { get; set; }

    public void Dispose()
    {
        Binding = null;
        GC.SuppressFinalize(this);
    }
}