using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

public class ColorPickerCell : DataGridViewCell
{
    public override Type EditType => typeof(ColorPickerEditingControl);
    public override Type ValueType => typeof(Color);
    public override Type FormattedValueType => typeof(string);
    public override object DefaultNewRowValue => Color.White;

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (value == null) return "Invalid";

        if (value is string colorString) return colorString;
        else if (value is Color color) return ColorTranslator.ToHtml(color);

        return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
    }

    public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        if (DataGridView?.EditingControl is ColorPickerEditingControl colorPickerEditingControl)
        {
            colorPickerEditingControl.ShowColorDialog();
        }
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
    }
}
