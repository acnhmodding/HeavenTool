using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Data.Converters;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public class DataGridStringColumn : HeavenDataGridColumn
{
    public int MaxLength { get; set; }
    protected override IValueConverter DisplayConverter => new Converters.FieldValueConverter(FieldType, FieldSize);

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        var textBox = new TextBox
        {
            MaxLength = MaxLength
        };

        binding = BindEditor(textBox, TextBox.TextProperty, dataItem);

        return textBox;
    }

    protected override void CancelCellEdit(Control editingElement, object uneditedValue)
    {
        if (editingElement is TextBox textBox) textBox.Text = (string)uneditedValue;
    }

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is TextBox textBox)
        {
            var uneditedText = textBox.Text ?? string.Empty;
            var len = uneditedText.Length;
            if (editingEventArgs is KeyEventArgs keyEventArgs && keyEventArgs.Key == Key.F2)
            {
                textBox.SelectionStart = len;
                textBox.SelectionEnd = len;
            }
            else
            {
                textBox.SelectionStart = 0;
                textBox.SelectionEnd = len;
                textBox.CaretIndex = len;
            }

            return uneditedText;
        }
        return string.Empty;
    }
}
