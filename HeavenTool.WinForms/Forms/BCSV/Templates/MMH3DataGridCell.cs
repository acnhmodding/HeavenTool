using HeavenTool.IO;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Templates;

public class MMH3DataGridCell : DataGridViewTextBoxCell
{
    public override Type EditType => typeof(DataGridViewTextBoxEditingControl);

    public override Type ValueType => typeof(uint);
    public override object DefaultNewRowValue => (uint)0;

    public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle, TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
    {
        if (formattedValue == null) return (uint)0;

        if (formattedValue is string s)
            return s.ToMurmur();

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
