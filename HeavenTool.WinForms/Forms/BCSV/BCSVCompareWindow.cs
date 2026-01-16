using HeavenTool.Forms.Components;
using System.Windows.Forms;

namespace HeavenTool.Forms;

public partial class BCSVCompareWindow : Form
{
    public BCSVCompareWindow()
    {
        InitializeComponent();

        compareDataGrid.EnableDarkModeScrollbar();
    }
}
