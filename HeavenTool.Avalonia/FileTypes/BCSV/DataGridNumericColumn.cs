using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Data;
using System;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public class DataGridNumericColumn(decimal minimum, decimal maximum) : HeavenDataGridColumn
{
    public decimal Minimum { get; set; } = minimum;
    public decimal Maximum { get; set; } = maximum;
    public decimal Increment { get; set; } = 1;

    public override bool CommitPendingEdit()
    {
        if (ActiveEditor is NumericUpDown numeric)
        {
            // NumericUpDown retains its previous Value while invalid text is being typed.
            // Validate Text before committing so Save cannot silently write that old value.
            if (!decimal.TryParse(numeric.Text, numeric.ParsingNumberStyle, numeric.NumberFormat, out var value)
                || value != decimal.Truncate(value) || value < Minimum || value > Maximum)
            {
                DataValidationErrors.SetError(numeric, new FormatException($"Enter a whole number between {Minimum} and {Maximum}."));
                return false;
            }
            DataValidationErrors.SetError(numeric, null);
            numeric.Value = value;
        }
        return base.CommitPendingEdit();
    }

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        var numericUpDown = new NumericUpDown
        {
            Minimum = Minimum,
            Maximum = Maximum,
            Increment = Increment
        };

        binding = BindEditor(numericUpDown, NumericUpDown.ValueProperty, dataItem);

        return numericUpDown;
    }

    protected override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs) =>
        ((NumericUpDown)editingElement).Value;

    protected override void CancelCellEdit(Control editingElement, object uneditedValue) =>
        ((NumericUpDown)editingElement).Value = (decimal?)uneditedValue;
}
