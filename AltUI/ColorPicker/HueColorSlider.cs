using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace AltUI.ColorPicker
{
    // Cyotek Color Picker controls library
    // Copyright © 2013-2017 Cyotek Ltd.
    // http://cyotek.com/blog/tag/colorpicker

    // Licensed under the MIT License. See license.txt for the full text.

    // If you use this code in your applications, donations or attribution are welcome

    public class HueColorSlider : ColorSlider
    {
        #region Constructors

        public HueColorSlider()
        {
            BarStyle = ColorBarStyle.Custom;
            Maximum = 359;
            CustomColors = new ColorCollection(Enumerable.Range(0, 359).Select(h => HslColor.HslToRgb(h, 1, 0.5)));
        }

        #endregion

        #region Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override ColorBarStyle BarStyle
        {
            get => base.BarStyle;
            set => base.BarStyle = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color Color1
        {
            get => base.Color1;
            set => base.Color1 = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color Color2
        {
            get => base.Color2;
            set => base.Color2 = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color Color3
        {
            get => base.Color3;
            set => base.Color3 = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override float Maximum
        {
            get => base.Maximum;
            set => base.Maximum = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override float Minimum
        {
            get => base.Minimum;
            set => base.Minimum = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override float Value
        {
            get => base.Value;
            set => base.Value = (int)value;
        }

        #endregion
    }
}
