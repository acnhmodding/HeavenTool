using AltUI.Config;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkTextBox : TextBox
    {
        #region Constructor Region

        public DarkTextBox()
        {
            BackColor = ThemeProvider.Theme.Colors.LightBackground;
            ForeColor = ThemeProvider.Theme.Colors.LightText;
            Padding = new Padding(2, 2, 2, 2);
            BorderStyle = BorderStyle.FixedSingle;
        }
        #endregion
    }
}
