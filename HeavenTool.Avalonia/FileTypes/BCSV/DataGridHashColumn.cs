using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Data;
using HeavenTool.Avalonia.FileTypes.BCSV.Converters;
using Avalonia.Controls.Utils;
using System.Linq;
using System;
using HeavenTool.IO;
using Avalonia.Layout;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public class DataGridHashColumn(uint hash, bool isMurmur = false) : HeavenDataGridColumn
{
    public static CRC32Converter CRC32_Converter { get; } = new CRC32Converter();
    public static MMH3Converter MMH3_Converter { get; } = new MMH3Converter();

    public bool IsMurmur { get; set; } = isMurmur;
    public uint ColumnHash { get; set; } = hash;

    private string[]? _possibleValues = null;
    public string[] GetPossibleValues
    {
        get
        {

            if (ColumnHash == 0)
                return [];

            if (_possibleValues == null)
            {
                _possibleValues = HashManager.EnumListCRC32.TryGetValue(ColumnHash, out var list) ? [.. list.Select(x => x.GetHashTranslation())] : [];

                Console.WriteLine($"Possible values: {string.Join(',', _possibleValues)}");
            }

            return _possibleValues;

        }
    }

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        binding = null;

        var comboBox = new ComboBox
        {
            ItemsSource = GetPossibleValues,
        };


        if (Binding is ReflectionBinding currentBinding)
        {
            var newBinding = new ReflectionBinding(currentBinding.Path)
            {
                Mode = BindingMode.TwoWay,
                Converter = IsMurmur ? MMH3_Converter : CRC32_Converter,
            };

            binding = comboBox.Bind(ComboBox.SelectedItemProperty, newBinding);
        }

        return comboBox;
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new TextBlock();

        if (Binding is ReflectionBinding binding)
        {
            var newBinding = new ReflectionBinding(binding.Path)
            {
                Mode = binding.Mode,
                Converter = IsMurmur ? MMH3_Converter : CRC32_Converter,
            };

            textBlock.Bind(TextBlock.TextProperty, newBinding);
        }

        return textBlock;
    }

    protected override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {

        if (editingElement is ComboBox comboBox)
        {

            //if (editingEventArgs is KeyEventArgs keyEventArgs && keyEventArgs.Key == Key.F2)
            //{
            //    comboBox.IsDropDownOpen = true;
            //}
            comboBox.HorizontalAlignment = HorizontalAlignment.Left;
            comboBox.AutoScrollToSelectedItem = true;
            comboBox.IsDropDownOpen = true;

            return comboBox.SelectedItem;
        }
        return string.Empty;
    }
}
