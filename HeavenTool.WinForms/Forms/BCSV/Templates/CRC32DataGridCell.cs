using HeavenTool.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Templates;

public class CRC32DataGridComboCell : DataGridViewTextBoxCell
{
    private static readonly Dictionary<string, AutoCompleteStringCollection> _columnAutoCompleteCache = [];
    private static readonly Dictionary<string, uint> _columnNameHashCache = [];

    private static uint GetColumnHash(string columnName)
    {
        if (_columnNameHashCache.TryGetValue(columnName, out var hash))
            return hash;

        if (uint.TryParse(columnName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            _columnNameHashCache[columnName] = parsed;
            return parsed;
        }

        return 0;
    }

    public override Type EditType => typeof(DataGridViewComboBoxEditingControl);
    public override Type ValueType => typeof(uint);
    public override object DefaultNewRowValue => 0u;

    public override object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle, TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        if (formattedValue == null) return 0u;

        if (formattedValue is string s && OwningColumn != null)
        {
            var hash = s.ToCRC32();
            // TODO: If the hash is unknown (which basically means that user added a new one)
            // then add it to our HashManager.EnumListCRC32
            // and save to disk
            var enumHash = GetColumnHash(OwningColumn.Name);
            if (enumHash > 0 && HashManager.EnumListCRC32.TryGetValue(enumHash, out var list) && !list.Contains(hash))
            {
                list.Add(hash);
                HashManager.CRC32_Hashes.TryAdd(hash, s);
            }

            return hash;
        }
        
        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

    public override void InitializeEditingControl(int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

        if (DataGridView != null && OwningColumn != null && DataGridView.EditingControl is DataGridViewComboBoxEditingControl control)
        {
            control.DropDownStyle = ComboBoxStyle.DropDown;
            control.AutoCompleteSource = AutoCompleteSource.ListItems;
            control.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            control.MaxDropDownItems = 10;

            string hashedName = OwningColumn.Name;

            if (!_columnAutoCompleteCache.TryGetValue(OwningColumn.Name, out var source))
            {
                var enumHash = GetColumnHash(OwningColumn.Name);
                if (enumHash > 0 && HashManager.EnumListCRC32.TryGetValue(enumHash, out var list))
                {
                    source = [.. list.Select(x => x.GetHashTranslation())];
                    _columnAutoCompleteCache[OwningColumn.Name] = source;
                }
                else source = [];
            }

            if (control.DataSource != source)
            {
                control.DataSource = source;
                control.TextChanged -= Control_TextChanged;
                control.TextChanged += Control_TextChanged;
            }

            if (initialFormattedValue is string text) control.Text = text;
            
        }
    }

    private void Control_TextChanged(object? sender, EventArgs e)
    {
        // Since we are using 
        DataGridView?.NotifyCurrentCellDirty(true);
    }
}