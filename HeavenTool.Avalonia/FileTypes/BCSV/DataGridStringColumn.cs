using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Utils;
using Avalonia.Input;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public class DataGridStringColumn : HeavenDataGridColumn
{
    public int MaxLength { get; set; }

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        binding = null;

        var textBox = new TextBox
        {
            MaxLength = MaxLength
        };

        if (Binding is ReflectionBinding currentBinding)
        {
            var newBinding = new ReflectionBinding(currentBinding.Path)
            {
                Mode = BindingMode.TwoWay,
            };

            binding = textBox.Bind(TextBox.TextProperty, newBinding);
        }

        return textBox;
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new TextBlock();

        if (Binding is ReflectionBinding binding)
            textBlock.Bind(TextBlock.TextProperty, binding);
        

        return textBlock;
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
