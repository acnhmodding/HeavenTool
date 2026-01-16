using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Templates;

public class BitFieldDataGridCell() : DataGridViewTextBoxCell
{
    public override Type EditType => typeof(DataGridViewTextBoxEditingControl);

    public override Type ValueType => typeof(byte[]);
    public override object DefaultNewRowValue => new byte[FieldLength];
    public int FieldLength { get; set; }

    public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle, TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
    {
        if (formattedValue == null)
            return new byte[FieldLength];

        var bitField = new byte[FieldLength];
        if (formattedValue is string s)
        {
            var split = s.Split(' ');

            for (int i = 0; i < bitField.Length; i++)
            {
                if (i >= split.Length || !byte.TryParse(split[i], out byte b))
                    break;

                bitField[i] = b;
            }

            return bitField;
        }

        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

    public override void InitializeEditingControl(int rowIndex,
        object initialFormattedValue,
        DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
    }

    public override object Clone()
    {
        var cloned = base.Clone();

        if (cloned is BitFieldDataGridCell bitFieldDataGridCell)
            bitFieldDataGridCell.FieldLength = FieldLength;

        return cloned;
    }
}
