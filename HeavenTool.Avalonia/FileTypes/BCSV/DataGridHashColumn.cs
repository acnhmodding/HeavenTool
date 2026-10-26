using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Data;
using HeavenTool.Avalonia.FileTypes.BCSV.Converters;
using HeavenTool.IO;
using Avalonia.Layout;
using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public class DataGridHashColumn(uint hash, bool isMurmur = false) : HeavenDataGridColumn
{
    private static readonly CRC32Converter CrcConverter = new();
    private static readonly MMH3Converter MurmurConverter = new();
    protected override IValueConverter DisplayConverter => isMurmur ? MurmurConverter : CrcConverter;
    protected override IValueConverter EditConverter => DisplayConverter;

    public override bool IsUnknownValue(object? value) =>
        value is uint key && !(isMurmur ? HashManager.MMH3_Hashes : HashManager.CRC32_Hashes).ContainsKey(key);

    private string[]? possibleValues;
    private string[] PossibleValues => possibleValues ??=
        hash != 0 && (isMurmur ? HashManager.EnumListMMH3 : HashManager.EnumListCRC32).TryGetValue(hash, out var list)
            ? [.. list.Select(value => FormatValue(value))] : [];

    /// <summary>The field's known values, as the grid shows them.</summary>
    public IReadOnlyList<string> Labels => PossibleValues;

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        if (PossibleValues.Length == 0)
        {
            var text = new TextBox();
            binding = BindEditor(text, TextBox.TextProperty, dataItem);
            return text;
        }
        var current = FormatValue(((BCSVRow)dataItem)[FieldIndex]);
        var comboBox = new ComboBox
        {
            ItemsSource = PossibleValues.Append(current).Distinct().ToArray(),
        };

        binding = BindEditor(comboBox, ComboBox.SelectedItemProperty, dataItem);

        return comboBox;
    }

    protected override void CancelCellEdit(Control editingElement, object uneditedValue)
    {
        if (editingElement is ComboBox combo) combo.SelectedItem = uneditedValue;
        else if (editingElement is TextBox text) text.Text = (string)uneditedValue;
    }

    protected override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is TextBox text)
        {
            text.SelectAll();
            return text.Text ?? "";
        }

        if (editingElement is ComboBox comboBox)
        {
            comboBox.HorizontalAlignment = HorizontalAlignment.Left;
            comboBox.AutoScrollToSelectedItem = true;
            comboBox.IsDropDownOpen = true;

            return comboBox.SelectedItem;
        }
        return string.Empty;
    }
}
