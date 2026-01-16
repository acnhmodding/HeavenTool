using HeavenTool.IO;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Templates;

public class CRC32DataGridComboCell : DataGridViewTextBoxCell
{
    public override Type EditType => typeof(DataGridViewComboBoxEditingControl);
    public override Type ValueType => typeof(uint);
    public override object DefaultNewRowValue => (uint) 0;

    public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle, TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
    {
        if (formattedValue == null) return (uint) 0;

        if (formattedValue is string s)
        {
            var hash = s.ToCRC32();
            // TODO: If the hash is unknown (which basically means that user added a new one)
            // then add it to our HashManager.EnumListCRC32
            // and save to disk
            string hashedName = OwningColumn.Name;
            if (uint.TryParse(hashedName,
                              NumberStyles.HexNumber,
                              CultureInfo.CurrentCulture,
                              out uint enumHash)
                && HashManager.EnumListCRC32.TryGetValue(enumHash, out var list) && !list.Contains(hash))
            {
                list.Add(hash);
                HashManager.CRC32_Hashes.TryAdd(hash, s);
            }

            return hash;
        }
        
        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

    public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

        if (DataGridView.EditingControl is DataGridViewComboBoxEditingControl control)
        {
            control.DropDownStyle = ComboBoxStyle.DropDown;
            control.AutoCompleteSource = AutoCompleteSource.ListItems;
            control.AutoCompleteMode = AutoCompleteMode.SuggestAppend;

            string hashedName = OwningColumn.Name;
            if (control.DataSource is null && uint.TryParse(hashedName,
                              NumberStyles.HexNumber,
                              CultureInfo.CurrentCulture,
                              out uint enumHash)
                && HashManager.EnumListCRC32.TryGetValue(enumHash, out var list))
            {
                var source = new AutoCompleteStringCollection();
                source.AddRange([.. list.Select(x => x.GetHashTranslation())]);

                control.DataSource = source;
                control.TextChanged += Control_TextChanged;
            }

            if (initialFormattedValue is string text) control.Text = text;
            
        }
    }

    private void Control_TextChanged(object sender, EventArgs e)
    {
        // Since we are using 
        DataGridView.NotifyCurrentCellDirty(true);
    }
}

public class CRC32DataGridCell : DataGridViewTextBoxCell
{
    public override Type EditType => typeof(DataGridViewTextBoxEditingControl);

    public override Type ValueType => typeof(uint);
    public override object DefaultNewRowValue => (uint)0;

    public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle, TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
    {
        if (formattedValue == null) return (uint)0;

        if (formattedValue is string s)
            return s.ToCRC32();

        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

    public override void InitializeEditingControl(int rowIndex,
        object initialFormattedValue,
        DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

        if (DataGridView.EditingControl is TextBox txt)
        {
            string hashedName = OwningColumn.Name;
            if (uint.TryParse(hashedName,
                              NumberStyles.HexNumber,
                              CultureInfo.CurrentCulture,
                              out uint enumHash)
                && HashManager.EnumListCRC32.TryGetValue(enumHash, out var list))
            {
                var source = new AutoCompleteStringCollection();
                source.AddRange([.. list.Select(x => x.GetHashTranslation())]);

                txt.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txt.AutoCompleteSource = AutoCompleteSource.CustomSource;
                txt.AutoCompleteCustomSource = source;
            }
            else
            {
                txt.AutoCompleteMode = AutoCompleteMode.None;
            }
        }
    }
}
