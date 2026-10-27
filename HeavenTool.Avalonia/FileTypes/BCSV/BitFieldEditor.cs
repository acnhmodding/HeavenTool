using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Linq;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

/// <summary>A checklist over a bitfield's flags; <see cref="Value"/> is a copy, so the row is untouched until applied.</summary>
public sealed class BitFieldEditor : UserControl
{
    private readonly CheckBox[] flags;
    public byte[] Value { get; }
    public event EventHandler? ValueChanged;

    public BitFieldEditor(DataGridBitFieldColumn column, byte[] value)
    {
        Value = (byte[])value.Clone();
        flags = [.. Enumerable.Range(0, column.FlagCount).Select(bit =>
        {
            var flag = new CheckBox { Content = column.FlagName(bit), IsChecked = DataGridBitFieldColumn.IsSet(Value, bit) };
            flag.IsCheckedChanged += (_, _) => Set(bit, flag.IsChecked == true);
            return flag;
        })];
        var clear = new Button { Content = "Clear All", HorizontalAlignment = HorizontalAlignment.Left };
        clear.Click += (_, _) => Clear();
        var list = new StackPanel();
        list.Children.AddRange(flags);
        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(clear, global::Avalonia.Controls.Dock.Bottom);
        clear.Margin = new(0, 8, 0, 0);
        layout.Children.Add(clear);
        layout.Children.Add(new ScrollViewer { Content = list });
        Content = layout;
    }

    private void Set(int bit, bool on)
    {
        byte mask = (byte)(1 << (bit % 8));
        Value[bit / 8] = (byte)(on ? Value[bit / 8] | mask : Value[bit / 8] & ~mask);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears every bit, including any the flag list doesn't name.</summary>
    public void Clear()
    {
        foreach (var flag in flags) flag.IsChecked = false;
        Array.Clear(Value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
