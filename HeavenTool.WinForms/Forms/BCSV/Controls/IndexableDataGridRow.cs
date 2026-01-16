using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

internal class IndexableDataGridRow(int index) : DataGridViewRow
{
    public int FileIndex { get; set; } = index;
}
