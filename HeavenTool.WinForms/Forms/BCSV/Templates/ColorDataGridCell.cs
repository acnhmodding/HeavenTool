using AltUI.ColorPicker;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Templates;

public class ColorDataGridCell : DataGridViewButtonCell
{
    public override Type ValueType => typeof(Color);

    public Dictionary<string, int> ColorFieldIndexes;

    public static (int r, int g, int b) FloatToRgbInt(float rFloat, float gFloat, float bFloat)
    {
        // Clamp the float values to ensure they are within the 0.0 to 1.0 range
        rFloat = Math.Clamp(rFloat, 0f, 1f);
        gFloat = Math.Clamp(gFloat, 0f, 1f);
        bFloat = Math.Clamp(bFloat, 0f, 1f);

        int rInt = (int)Math.Round(rFloat * 255f);
        int gInt = (int)Math.Round(gFloat * 255f);
        int bInt = (int)Math.Round(bFloat * 255f);

        return (rInt, gInt, bInt);
    }

    protected override void OnClick(DataGridViewCellEventArgs e)
    {
        var colorPicker = new ColorPickerDialog()
        {
            ShowAlphaChannel = false,
        };


        if (Value is Color color)
        {
            colorPicker.Color = color;
        }

        if (colorPicker.ShowDialog() == DialogResult.OK)
        {
            SetValue(e.RowIndex, colorPicker.Color);
        }
    }

    public Color GetColor(float floatR, float floatG, float floatB)
    {
        var (r, g, b) = FloatToRgbInt(floatR, floatG, floatB);

        return Color.FromArgb(r, g, b);
    }

    public override object Clone()
    {
        var cloned = base.Clone();

        if (cloned is ColorDataGridCell colorDataGridCell)
        {
            colorDataGridCell.FlatStyle = FlatStyle.Flat; // Always set to flat so custom colors works :)
            colorDataGridCell.ColorFieldIndexes = ColorFieldIndexes;
        }

        return cloned;
    }
}
