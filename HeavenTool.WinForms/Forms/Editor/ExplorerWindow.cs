using System.ComponentModel;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms.Editor;

public class ExplorerWindow : DockContent
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeView TreeView { get; set; }
    
    public ExplorerWindow()
    {
        Text = "Explorer";
        CloseButton = false;
        CloseButtonVisible = false;

        TreeView = new TreeView()
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(TreeView);
    }
}