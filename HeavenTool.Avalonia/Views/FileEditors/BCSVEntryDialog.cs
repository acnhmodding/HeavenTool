using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HeavenTool.Avalonia.FileTypes.BCSV;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.Views.FileEditors;

/// <summary>
/// Fills in a new row field by field before it is added. Values are parsed as the grid parses typed
/// text, and the row is only handed back when every field is valid.
/// </summary>
public sealed class BCSVEntryDialog : Window
{
    private readonly IReadOnlyList<HeavenDataGridColumn> fields;
    private readonly object[] values;
    private readonly string?[] errors;
    private readonly string?[] warnings;
    private readonly List<(Control Row, string[] Names)> rows = [];
    private readonly Button add = new() { Content = "Add Row", IsDefault = true };
    private readonly TextBlock status = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    /// <summary>Each field's editor, by field index.</summary>
    public Control[] Editors { get; }
    public TextBox Filter { get; } = new() { PlaceholderText = "Filter fields" };
    /// <summary>The new row's values once the dialog is accepted.</summary>
    public object[] Values => values;

    /// <param name="fields">The file's field columns, by field index.</param>
    /// <param name="start">Values the editors start from; the dialog edits its own copy.</param>
    public BCSVEntryDialog(IReadOnlyList<HeavenDataGridColumn> fields, IEnumerable<DataGridColorColumn> colors, object[] start, string source)
    {
        this.fields = fields;
        values = [.. start.Select(value => value is byte[] bytes ? bytes.Clone() : value)];
        errors = new string?[fields.Count];
        warnings = new string?[fields.Count];
        Title = "New Entry";
        Width = 560;
        Height = 640;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Editors = [.. fields.Select(CreateEditor)];
        var colorAt = colors.ToLookup(color => color.Fields.Min(field => fields[field].DisplayIndex));
        // A colour's fields are set through its picker, as in the grid.
        var components = colors.SelectMany(color => color.Fields).ToHashSet();
        var list = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 14, 0) };
        // Fields follow the grid's column order, so the unique ID comes first.
        foreach (var column in fields.OrderBy(column => column.DisplayIndex))
        {
            foreach (var color in colorAt[column.DisplayIndex])
                AddRow(list, color.Header?.ToString() ?? "Color", null, false, CreateColorEditor(color),
                    [.. color.Fields.Select(field => fields[field].Header?.ToString() ?? "")]);
            if (!components.Contains(column.FieldIndex))
                AddRow(list, column.Header?.ToString() ?? "", column.HeaderTip, column.HeaderKind == FieldHeaderKind.UniqueId,
                    Editors[column.FieldIndex], []);
        }
        Filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) ApplyFilter();
        };

        var cancel = new Button { Content = "Cancel", IsCancel = true };
        add.Click += (_, _) => Accept();
        cancel.Click += (_, _) => Close(false);
        var top = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(0, 0, 0, 12),
            Children = { new TextBlock { Text = source, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 }, Filter }
        };
        var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, cancel } };
        DockPanel.SetDock(buttons, Dock.Right);
        bottom.Children.Add(buttons);
        bottom.Children.Add(status);
        var layout = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        layout.Children.Add(top);
        layout.Children.Add(bottom);
        layout.Children.Add(new ScrollViewer { Content = list });
        Content = layout;
        UpdateStatus();
    }

    private void AddRow(StackPanel list, string name, string? tip, bool unique, Control editor, string[] related)
    {
        var label = new TextBlock
        {
            Text = name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = unique ? FontWeight.Bold : FontWeight.Normal
        };
        ToolTip.SetTip(label, tip ?? name);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*"), ColumnSpacing = 12, Children = { label, editor } };
        Grid.SetColumn(editor, 1);
        list.Children.Add(row);
        rows.Add((row, [name, .. related]));
    }

    private void ApplyFilter()
    {
        var text = Filter.Text?.Trim() ?? "";
        foreach (var (row, names) in rows)
            row.IsVisible = text.Length == 0 || names.Any(name => name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private Control CreateEditor(HeavenDataGridColumn column)
    {
        int field = column.FieldIndex;
        if (column is DataGridBitFieldColumn flags)
        {
            var editor = new BitFieldEditor(flags, (byte[])values[field]) { MaxHeight = 320, Margin = new Thickness(8) };
            var button = new DropDownButton
            {
                Content = column.FormatValue(editor.Value), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, Flyout = new Flyout { Content = editor }
            };
            editor.ValueChanged += (_, _) =>
            {
                values[field] = editor.Value.Clone();
                button.Content = column.FormatValue(editor.Value);
            };
            return button;
        }

        var text = column.FormatValue(values[field]);
        var original = values[field];
        TextBox? box = null;
        AutoCompleteBox? labels = null;
        Control control;
        if (column is DataGridHashColumn { Labels.Count: > 0 } hash)
            control = labels = new AutoCompleteBox
            {
                ItemsSource = hash.Labels, Text = text, MinimumPrefixLength = 0,
                FilterMode = AutoCompleteFilterMode.ContainsOrdinal, MaxDropDownHeight = 260
            };
        else
            control = box = new TextBox { Text = text, MaxLength = column is DataGridStringColumn strings ? strings.MaxLength : 0 };
        void Parse()
        {
            var typed = box?.Text ?? labels?.Text ?? "";
            bool parsed = column.TryParseText(typed, original, out var value, out var error);
            if (parsed) values[field] = value;
            errors[field] = error;
            warnings[field] = parsed && !Equals(value, original) ? column.Warning(typed, value!) : null;
            DataValidationErrors.SetError(control, error == null ? null : new FormatException(error));
            UpdateStatus();
        }
        // Text property changes arrive at once; TextChanged waits for the dispatcher.
        control.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == AutoCompleteBox.TextProperty) Parse();
        };
        return control;
    }

    private bool syncingColor;

    // The picker writes all three fields through their (unshown) boxes, which keep it in step.
    private ColorPicker CreateColorEditor(DataGridColorColumn color)
    {
        int[] components = color.Fields;
        Color Current() => Color.FromRgb(DataGridColorColumn.ToByte(values[color.RedField]),
            DataGridColorColumn.ToByte(values[color.GreenField]), DataGridColorColumn.ToByte(values[color.BlueField]));
        var picker = new ColorPicker
        {
            Color = Current(), IsAlphaEnabled = false, IsAlphaVisible = false, IsAccentColorsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        picker.ColorChanged += (_, e) =>
        {
            if (syncingColor || e.NewColor == Current()) return;
            syncingColor = true;
            byte[] channels = [e.NewColor.R, e.NewColor.G, e.NewColor.B];
            for (int i = 0; i < 3; i++)
                if (Editors[components[i]] is TextBox box) box.Text = fields[components[i]].FormatValue(channels[i] / 255f);
            syncingColor = false;
        };
        foreach (var component in components)
            if (Editors[component] is TextBox box)
                box.PropertyChanged += (_, e) =>
                {
                    if (e.Property != TextBox.TextProperty || syncingColor) return;
                    syncingColor = true;
                    picker.Color = Current();
                    syncingColor = false;
                };
        return picker;
    }

    private void UpdateStatus()
    {
        int invalid = errors.Count(error => error != null);
        int doubtful = warnings.Count(warning => warning != null);
        add.IsEnabled = invalid == 0;
        // Warnings don't block adding the row; they show once nothing is invalid.
        status.Foreground = App.Brush(invalid > 0 ? "ErrorText" : "WarningText");
        status.Text = (invalid, doubtful) switch
        {
            (1, _) => $"{fields[Array.FindIndex(errors, error => error != null)].Header}: {errors.First(error => error != null)}",
            ( > 1, _) => $"{invalid} fields need a valid value.",
            (_, 1) => warnings.First(warning => warning != null),
            (_, > 1) => $"{doubtful} fields hold text that isn't a known label and will be stored as new hashes.",
            _ => ""
        };
    }

    /// <summary>Closes with the row when every field is valid.</summary>
    public bool Accept()
    {
        if (errors.Any(error => error != null)) return false;
        Close(true);
        return true;
    }
}
