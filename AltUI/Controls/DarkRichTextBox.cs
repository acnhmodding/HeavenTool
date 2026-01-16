using AltUI.Config;
using System.ComponentModel;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkRichTextBox : RichTextBox
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasFocus => Focused;

        public DarkRichTextBox()
        {
            BackColor = ThemeProvider.Theme.Colors.GreyBackground;
            ForeColor = ThemeProvider.Theme.Colors.LightText;
        }
    }
}