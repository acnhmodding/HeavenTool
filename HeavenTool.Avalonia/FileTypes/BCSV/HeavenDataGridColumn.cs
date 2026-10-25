using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using HeavenTool.Avalonia.FileTypes.BCSV.Converters;
using HeavenTool.IO.FileFormats.BCSV;
using System;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public abstract class HeavenDataGridColumn : DataGridColumn, IDisposable
{
    public int FieldIndex { get; set; }
    public DataType FieldType { get; set; }
    // used for string limit
    public int FieldSize { get; set; }
    private CompiledBinding? displayBinding;
    private CompiledBindingPath? bindingPath;
    private BindingExpressionBase? activeBinding;
    private Control? activeEditor;
    protected Control? ActiveEditor => activeEditor;

    protected virtual IValueConverter? DisplayConverter => null;
    protected virtual IValueConverter EditConverter => new FieldValueConverter(FieldType, FieldSize);

    private CompiledBindingPath BindingPath => bindingPath ??=
        new CompiledBindingPathBuilder().ArrayElement([FieldIndex], typeof(object)).Build();

    protected override Control GenerateElement(DataGridCell cell, object dataItem) =>
        new DeferredTextCell(displayBinding ??= new CompiledBinding(BindingPath)
        {
            Converter = DisplayConverter, Mode = BindingMode.OneWay
        });

    protected BindingExpressionBase BindEditor(Control editor, AvaloniaProperty property, object dataItem)
    {
        activeEditor = editor;
        var binding = new CompiledBinding(BindingPath)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            Converter = new EditValueConverter(EditConverter, ((object[])dataItem)[FieldIndex])
        };
        return activeBinding = editor.Bind(property, binding);
    }

    public virtual bool CommitPendingEdit()
    {
        activeBinding?.UpdateSource();
        return activeEditor == null || !DataValidationErrors.GetHasErrors(activeEditor);
    }

    public void Dispose()
    {
        displayBinding = null;
        bindingPath = null;
        activeBinding = null;
        activeEditor = null;
        GC.SuppressFinalize(this);
    }

    // DataGrid creates cells for every column; bind text only as a cell enters the viewport.
    private sealed class DeferredTextCell(BindingBase binding) : TextBlock
    {
        private bool bound;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (!bound)
            {
                bound = true;
                Bind(TextProperty, binding);
            }
            return base.MeasureOverride(availableSize);
        }
    }
}
