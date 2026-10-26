using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HeavenTool.Avalonia.FileTypes.BCSV.Converters;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

/// <summary>How a column header stands out: the unique ID, or a field whose name isn't known.</summary>
public enum FieldHeaderKind { Normal, UniqueId, UnknownName, UnknownShortName }

public abstract class HeavenDataGridColumn : DataGridColumn, IDisposable
{
    private static readonly CompiledBindingPath BufferPath =
        new CompiledBindingPathBuilder().ArrayElement([0], typeof(object)).Build();

    public int FieldIndex { get; set; }
    public DataType FieldType { get; set; }
    // used for string limit
    public int FieldSize { get; set; }
    private BindingExpressionBase? activeBinding;
    private Control? activeEditor;
    private EditValueConverter? activeConverter;
    private BCSVRow? editRow;
    private object[]? editBuffer;
    protected Control? ActiveEditor => activeEditor;
    public SearchHighlights? Highlights { get; set; }
    public FieldHeaderKind HeaderKind { get; set; }
    public string? HeaderTip { get; set; }

    /// <summary>Hash values without a known label stand out, as typos or missing names.</summary>
    public virtual bool IsUnknownValue(object? value) => false;

    /// <summary>Why a value parsed from <paramref name="text"/> is valid but may not be what was meant, or null.</summary>
    public virtual string? Warning(string text, object value) => null;

    /// <summary>False when the field has its own editor, so typed text (replace, paste) can't set it.</summary>
    public virtual bool EditsAsText => true;

    protected virtual IValueConverter? DisplayConverter => null;
    private FieldValueConverter? fieldConverter;
    protected virtual IValueConverter EditConverter => fieldConverter ??= new FieldValueConverter(FieldType, FieldSize);

    protected HeavenDataGridColumn()
    {
        CustomSortComparer = new FieldComparer(this);
        CanUserSort = true;
        HeaderTemplate = new FuncDataTemplate<object?>((header, _) => new FieldHeader(this) { Text = header?.ToString() });
    }

    public string FormatValue(object? value)
    {
        if (DisplayConverter != null) value = DisplayConverter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);
        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
    }

    /// <summary>Parses text typed for this field, keeping the original value when the text is unchanged.</summary>
    public bool TryParseText(string text, object original, [NotNullWhen(true)] out object? value, [NotNullWhen(false)] out string? error)
    {
        try
        {
            value = new EditValueConverter(EditConverter, original).Parse(text, null, CultureInfo.CurrentCulture);
            error = value == null ? "A value is required." : null;
            return value != null;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            value = null;
            error = ex.Message;
            return false;
        }
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem) => new FieldTextCell(this, cell);

    protected BindingExpressionBase BindEditor(Control editor, AvaloniaProperty property, object dataItem)
    {
        editRow = (BCSVRow)dataItem;
        var original = editRow[FieldIndex];
        // The editor writes into a buffer; the view model applies it to the row as an undoable change.
        editBuffer = [original];
        activeEditor = editor;
        var binding = new CompiledBinding(BufferPath)
        {
            Source = editBuffer,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            Converter = activeConverter = new EditValueConverter(EditConverter, original)
        };
        return activeBinding = editor.Bind(property, binding);
    }

    public virtual bool CommitPendingEdit()
    {
        activeBinding?.UpdateSource();
        if (activeConverter?.Error != null) return false;
        // The buffer doesn't notify, so text that parses after rejected text would keep the old error and block the commit.
        if (activeEditor != null) DataValidationErrors.ClearErrors(activeEditor);
        return true;
    }

    /// <summary>Hands over the committed editor value once, so a stale buffer can never reach another row.</summary>
    public bool TryTakePendingEdit([NotNullWhen(true)] out BCSVRow? row, [NotNullWhen(true)] out object? value, out string? warning)
    {
        row = editRow;
        value = editBuffer?[0];
        var text = activeEditor switch { TextBox box => box.Text, ComboBox combo => combo.SelectedItem as string, _ => null };
        warning = text != null && value != null ? Warning(text, value) : null;
        editRow = null;
        editBuffer = null;
        return row != null && value != null;
    }

    public void Dispose()
    {
        activeBinding = null;
        activeEditor = null;
        activeConverter = null;
        editRow = null;
        editBuffer = null;
        GC.SuppressFinalize(this);
    }

    // Hashes sort by their shown label, and "-1" sorts before 0; everything else by the stored value.
    private sealed class FieldComparer(HeavenDataGridColumn column) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not BCSVRow left || y is not BCSVRow right) return 0;
            object a = left[column.FieldIndex], b = right[column.FieldIndex];
            if (column.FieldType is DataType.CRC32 or DataType.MMH3)
                return StringComparer.OrdinalIgnoreCase.Compare(column.FormatValue(a), column.FormatValue(b));
            if (column.FieldType is DataType.UInt16 or DataType.UInt32)
                return Signed(a).CompareTo(Signed(b));
            return StructuralComparisons.StructuralComparer.Compare(a, b);
        }

        private static long Signed(object value) => value is ushort.MaxValue or uint.MaxValue ? -1 : Convert.ToInt64(value);
    }

    // The header control belongs to the grid, so the label styles it once it's placed inside.
    private sealed class FieldHeader(HeavenDataGridColumn column) : TextBlock
    {
        private static readonly IBrush UniqueBrush = App.Brush("UniqueIdHeaderBackground");
        private static readonly IBrush UnknownNameBrush = App.Brush("UnknownNameHeaderBackground");
        private static readonly IBrush UnknownShortNameBrush = App.Brush("UnknownShortNameHeaderBackground");

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            VerticalAlignment = VerticalAlignment.Center;
            if (column.HeaderKind == FieldHeaderKind.UniqueId) FontWeight = FontWeight.Bold;
            if (this.FindAncestorOfType<DataGridColumnHeader>() is not { } header) return;
            ToolTip.SetTip(header, column.HeaderTip);
            var brush = column.HeaderKind switch
            {
                FieldHeaderKind.UniqueId => UniqueBrush,
                FieldHeaderKind.UnknownName => UnknownNameBrush,
                FieldHeaderKind.UnknownShortName => UnknownShortNameBrush,
                _ => null
            };
            if (brush != null) header.Background = brush;
        }
    }

    // DataGrid creates cells for every column; format text only as a cell enters the viewport.
    private sealed class FieldTextCell(HeavenDataGridColumn column, DataGridCell cell) : TextBlock
    {
        private static readonly IBrush MatchBrush = App.Brush("SearchMatchBackground");
        private static readonly IBrush CurrentBrush = App.Brush("SearchCurrentBackground");
        private static readonly IBrush UnknownBrush = App.Brush("UnknownHashBackground");
        private BCSVRow? row;
        private SearchHighlights? highlights;
        private bool attached;
        private bool stale = true;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            Track();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            attached = true;
            // Hash labels are bold, as in the WinForms editor.
            if (column.FieldType is DataType.CRC32 or DataType.MMH3) FontWeight = FontWeight.SemiBold;
            Track();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            attached = false;
            Track();
        }

        // Only cells in the tree listen to their row, so recycled or removed cells don't leak.
        private void Track()
        {
            var nextHighlights = attached ? column.Highlights : null;
            if (!ReferenceEquals(highlights, nextHighlights))
            {
                if (highlights != null) highlights.Changed -= OnHighlightsChanged;
                highlights = nextHighlights;
                if (highlights != null) highlights.Changed += OnHighlightsChanged;
            }
            var next = attached ? DataContext as BCSVRow : null;
            if (ReferenceEquals(row, next)) return;
            if (row != null) row.PropertyChanged -= OnRowChanged;
            row = next;
            if (row != null) row.PropertyChanged += OnRowChanged;
            OnRowChanged(null, null);
            OnHighlightsChanged(null, EventArgs.Empty);
        }

        // The whole cell is marked, so the highlight shows even when the text is short. Search
        // highlights win over the unknown-hash mark.
        private void OnHighlightsChanged(object? sender, EventArgs e)
        {
            var hit = row != null && highlights != null ? highlights[row, column.FieldIndex] : SearchHit.None;
            if (hit != SearchHit.None) cell.Background = hit == SearchHit.Current ? CurrentBrush : MatchBrush;
            else if (row != null && column.IsUnknownValue(row[column.FieldIndex])) cell.Background = UnknownBrush;
            else cell.ClearValue(DataGridCell.BackgroundProperty);
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs? e)
        {
            stale = true;
            InvalidateMeasure();
            if (e != null) OnHighlightsChanged(null, EventArgs.Empty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (stale && row != null)
            {
                stale = false;
                Text = column.FormatValue(row[column.FieldIndex]);
            }
            return base.MeasureOverride(availableSize);
        }
    }
}
