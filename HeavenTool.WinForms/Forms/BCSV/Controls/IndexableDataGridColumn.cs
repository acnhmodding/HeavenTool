using System.ComponentModel;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

public class IndexableDataGridColumn(int index) : DataGridViewColumn
{

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HeaderIndex { get; set; } = index;
}
