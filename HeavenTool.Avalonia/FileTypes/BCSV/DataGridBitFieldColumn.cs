using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

/// <summary>Flags packed into bytes; edited with a checklist, named when the flag list is known.</summary>
public class DataGridBitFieldColumn(string[]? names) : HeavenDataGridColumn
{
    private IValueConverter? displayConverter;
    protected override IValueConverter DisplayConverter => displayConverter ??= new FlagsConverter(this);
    public override bool EditsAsText => false;

    // A list longer than the field can't belong to it.
    private string[]? Names => names != null && names.Length <= FieldSize * 8 ? names : null;
    public bool HasNames => Names != null;
    public int FlagCount => Names?.Length ?? FieldSize * 8;
    public string FlagName(int bit) => Names is { } known && bit < known.Length ? known[bit] : $"Bit {bit}";

    public static bool IsSet(byte[] bits, int bit) => (bits[bit / 8] & (1 << (bit % 8))) != 0;

    // The grid opens the flag editor instead.
    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        binding = null;
        return new TextBlock();
    }

    protected override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs) => null;

    // Named flags read as their names; unnamed ones keep the raw bytes as hex.
    private sealed class FlagsConverter(DataGridBitFieldColumn column) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not byte[] bits) return value;
            if (!column.HasNames) return System.Convert.ToHexString(bits);
            var set = new List<string>();
            for (int bit = 0; bit < bits.Length * 8; bit++)
                if (IsSet(bits, bit)) set.Add(column.FlagName(bit));
            return set.Count == 0 ? "None" : string.Join(", ", set);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("Flags are edited with the flag editor.");
    }
}
