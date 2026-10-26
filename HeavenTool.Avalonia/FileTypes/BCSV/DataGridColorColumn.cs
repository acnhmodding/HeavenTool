using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.ComponentModel;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

/// <summary>
/// Shows three float fields (0 to 1) as one colour, in place of their own columns, which stay hidden.
/// It holds no field of its own: copy and exports write the three fields, and search skips it.
/// </summary>
public sealed class DataGridColorColumn : DataGridColumn
{
    public int RedField { get; init; }
    public int GreenField { get; init; }
    public int BlueField { get; init; }
    public int[] Fields => [RedField, GreenField, BlueField];

    public DataGridColorColumn()
    {
        CanUserSort = false;
        Width = new DataGridLength(120);
    }

    public static byte ToByte(object value) =>
        value is float component && float.IsFinite(component) ? (byte)Math.Round(Math.Clamp(component, 0f, 1f) * 255f) : (byte)0;

    public Color GetColor(BCSVRow row) => Color.FromRgb(ToByte(row[RedField]), ToByte(row[GreenField]), ToByte(row[BlueField]));

    protected override Control GenerateElement(DataGridCell cell, object dataItem) => new ColorCell(this);

    // The grid opens the colour picker instead.
    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase? binding)
    {
        binding = null;
        return new TextBlock();
    }

    protected override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs) => null;

    private sealed class ColorCell : StackPanel
    {
        private readonly DataGridColorColumn column;
        private readonly Border swatch = new()
        {
            Width = 16, Height = 12, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center
        };
        private readonly TextBlock text = new() { VerticalAlignment = VerticalAlignment.Center };
        private BCSVRow? row;
        private bool attached;

        public ColorCell(DataGridColorColumn column)
        {
            this.column = column;
            Orientation = Orientation.Horizontal;
            Spacing = 6;
            Margin = new Thickness(6, 0);
            Children.Add(swatch);
            Children.Add(text);
        }

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

        // Only cells in the tree listen to their row, so recycled cells don't leak.
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
            if (row == null) return;
            var color = column.GetColor(row);
            swatch.Background = new SolidColorBrush(color);
            text.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
