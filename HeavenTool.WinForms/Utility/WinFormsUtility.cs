using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace HeavenTool.Utility;

public static class WinFormsUtility
{
    public static void FormClosingConfirmation(FormClosingEventArgs e, bool condition, Action? onClosing = null)
    {
        if (!condition) return;

        var result = MessageBox.Show("Do you really want to close this file?\nUnsaved changes will be lost!", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            onClosing?.Invoke();
        }
        else e.Cancel = true;
    }

    public static bool SaveDialog(string filter, string fileName, [MaybeNullWhen(false)] out string result)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = filter,
            RestoreDirectory = true,
            OverwritePrompt = true,
            FileName = fileName
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            result = dlg.FileName;
            return true;
        }

        result = null;
        return false;
    }

    public static ContextMenuStrip ContextMenu(params ToolStripItem[] items)
    {
        var menu = new ContextMenuStrip();
        menu.Items.AddRange(items);
        return menu;
    }

    public static ContextMenuStrip ContextMenu(Action<ContextMenuStrip> builder)
    {
        var menu = new ContextMenuStrip();

        builder(menu);

        return menu;
    }

    public static ToolStripMenuItem ContextMenuButton(string name, Action action)
    {
        return new ToolStripMenuItem(name, null, (_, _) =>
        {
            action();
        });
    }

    extension(ContextMenuStrip menu)
    {
        public void AddItem(string name, Action action)
        {
            menu.Items.Add(ContextMenuButton(name, action));
        }

        public void AddSeparator() {
            menu.Items.Add(new ToolStripSeparator());
        }
    }

    extension(ToolStripMenuItem menu)
    {
        public ToolStripMenuItem AddItem(string name, Action action)
        {
            var item = ContextMenuButton(name, action);
            menu.DropDownItems.Add(ContextMenuButton(name, action));

            return item;
        }

        public void AddSeparator()
        {
            menu.DropDownItems.Add(new ToolStripSeparator());
        }
    }
}
