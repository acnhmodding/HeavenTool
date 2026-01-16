using System.Windows.Forms;

namespace HeavenTool.DataTable;

internal class IndexRow : DataGridViewRow
{
    public int OriginalIndex;

    public IndexRow(): base()
    {
        OriginalIndex = Index;
    }
}
