using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Utils;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public class DataGridNumericColumn(decimal minimum, decimal maximum) : HeavenDataGridColumn
{
    public decimal Minimum { get; set; } = minimum;
    public decimal Maximum { get; set; } = maximum;
    public decimal Increment { get; set; } = 1;

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        binding = null;

        var numericUpDown = new NumericUpDown()
        {
            Minimum = Minimum,
            Maximum = Maximum,
            Increment = Increment
        };

        if (Binding is ReflectionBinding currentBinding)
        {
            var newBinding = new ReflectionBinding(currentBinding.Path)
            {
                Mode = BindingMode.TwoWay,
            };

            binding = numericUpDown.Bind(NumericUpDown.ValueProperty, newBinding);
        }

        return numericUpDown;
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new TextBlock();

        if (Binding is ReflectionBinding binding)
            textBlock.Bind(TextBlock.TextProperty, binding);
        

        return textBlock;
    }

    protected override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        return null;
    }
}
