using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using HeavenTool.Avalonia.FileTypes.BCSV.Converters;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public abstract class HeavenDataGridColumn : DataGridColumn, IDisposable
{
    private static readonly CompiledBindingPath BufferPath =
        new CompiledBindingPathBuilder().ArrayElement([0], typeof(object)).Build();

    public int FieldIndex { get; set; }
    public DataType FieldType { get; set; }
    // used for string limit
    public int FieldSize { get; set; }
    private BindingExpressionBase? activeBinding;
    private Control? activeEditor;
    private BCSVRow? editRow;
    private object[]? editBuffer;
    protected Control? ActiveEditor => activeEditor;

    protected virtual IValueConverter? DisplayConverter => null;
    protected virtual IValueConverter EditConverter => new FieldValueConverter(FieldType, FieldSize);

    protected HeavenDataGridColumn()
    {
        CustomSortComparer = new FieldComparer(this);
        CanUserSort = true;
    }

    public string FormatValue(object? value)
    {
        if (DisplayConverter != null) value = DisplayConverter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);
        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem) => new FieldTextCell(this);

    protected BindingExpressionBase BindEditor(Control editor, AvaloniaProperty property, object dataItem)
    {
        editRow = (BCSVRow)dataItem;
        var original = editRow[FieldIndex];
        // The editor writes into a buffer; the view model applies it to the row as an undoable change.
        editBuffer = [original];
        activeEditor = editor;
        var binding = new CompiledBinding(BufferPath)
        {
            Source = editBuffer,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            Converter = new EditValueConverter(EditConverter, original)
        };
        return activeBinding = editor.Bind(property, binding);
    }

    public virtual bool CommitPendingEdit()
    {
        activeBinding?.UpdateSource();
        return activeEditor == null || !DataValidationErrors.GetHasErrors(activeEditor);
    }

    /// <summary>Hands over the committed editor value once, so a stale buffer can never reach another row.</summary>
    public bool TryTakePendingEdit([NotNullWhen(true)] out BCSVRow? row, [NotNullWhen(true)] out object? value)
    {
        row = editRow;
        value = editBuffer?[0];
        editRow = null;
        editBuffer = null;
        return row != null && value != null;
    }

    public void Dispose()
    {
        activeBinding = null;
        activeEditor = null;
        editRow = null;
        editBuffer = null;
        GC.SuppressFinalize(this);
    }

    // Hashes sort by their shown label; everything else by the stored value.
    private sealed class FieldComparer(HeavenDataGridColumn column) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not BCSVRow left || y is not BCSVRow right) return 0;
            object a = left[column.FieldIndex], b = right[column.FieldIndex];
            if (column.FieldType is DataType.CRC32 or DataType.MMH3)
                return StringComparer.OrdinalIgnoreCase.Compare(column.FormatValue(a), column.FormatValue(b));
            return StructuralComparisons.StructuralComparer.Compare(a, b);
        }
    }

    // DataGrid creates cells for every column; format text only as a cell enters the viewport.
    private sealed class FieldTextCell(HeavenDataGridColumn column) : TextBlock
    {
        private BCSVRow? row;
        private bool attached;
        private bool stale = true;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            Track();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            attached = true;
            Track();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            attached = false;
            Track();
        }

        // Only cells in the tree listen to their row, so recycled or removed cells don't leak.
        private void Track()
        {
            var next = attached ? DataContext as BCSVRow : null;
            if (ReferenceEquals(row, next)) return;
            if (row != null) row.PropertyChanged -= OnRowChanged;
            row = next;
            if (row != null) row.PropertyChanged += OnRowChanged;
            OnRowChanged(null, null);
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs? e)
        {
            stale = true;
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (stale && row != null)
            {
                stale = false;
                Text = column.FormatValue(row[column.FieldIndex]);
            }
            return base.MeasureOverride(availableSize);
        }
    }
}
