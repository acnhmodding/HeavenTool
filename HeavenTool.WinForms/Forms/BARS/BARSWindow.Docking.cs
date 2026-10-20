using HeavenTool.Forms.Components;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow
{
    private readonly List<DockContent> dockContents = [];

    private void InitializeDocking()
    {
        dockPanel.Theme = new VS2015DarkTheme();
        var resources = new BarsDockContent(this, resourcesPanel, "Resources");
        var inspector = new BarsDockContent(this, itemPropertyGrid, "Inspector");
        var preview = new BarsDockContent(this, previewPanel, "Audio Preview");
        dockContents.AddRange([resources, inspector, preview]);
        resources.Show(dockPanel, DockState.DockLeft);
        preview.Show(dockPanel, DockState.DockBottom);
        inspector.Show(dockPanel, DockState.DockRight);
    }

    private sealed class BarsDockContent(BARSWindow editor, Control control, string title) : DockableControl<Control>(control, title)
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                editor.SaveFileAs();
                return true;
            }
            return editor.HandlePlaybackKey(keyData) || base.ProcessCmdKey(ref msg, keyData);
        }
    }
}