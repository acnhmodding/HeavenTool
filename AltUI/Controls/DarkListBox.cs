using AltUI.Config;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkListBox : ListBox
    {
        #region Constructor Region

        public DarkListBox()
        {
            BackColor = ThemeProvider.Theme.Colors.LightBackground;
            ForeColor = ThemeProvider.Theme.Colors.LightText;
            BorderStyle = BorderStyle.FixedSingle;
        }

        #endregion
    }
}