using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

    public static bool SaveFileDialog(string filter, string fileName, [MaybeNullWhen(false)] out string result)
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

    public static bool OpenFileDialog(string filter, string defaultExt, [MaybeNullWhen(false)] out string result)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = filter,
            RestoreDirectory = true,
            DefaultExt = defaultExt
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            result = dlg.FileName;
            return true;
        }

        result = null;
        return false;
    }

    public static bool FolderSelector([MaybeNullWhen(false)] out string result, string description = "")
    {
        using var dlg = new FolderBrowserDialog()
        {
            Description = description
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            result = dlg.SelectedPath;
            return true;
        }

        result = null;
        return false;
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Failed to open URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

    public static bool AssociateProgram(bool associate, string progId, string description, params string[] extensions)
    {
        var validExtensions = extensions.Distinct().ToList();

        if (validExtensions.Count == 0) return false;

        try
        {
            if (associate)
            {
                foreach (var item in validExtensions)
                    ProgramAssociation.AssociateProgram(item, progId, description, Application.ExecutablePath);
            }
            else
            {
                foreach (var item in validExtensions)
                    ProgramAssociation.DisassociateProgram(item, progId);
            }
        }
        catch (Exception ex)
        {
            var action = associate ? "associate" : "disassociate";
            MessageBox.Show(ex.Message, $"Failed to {action}");
        }

        // Check for first extension only
        return ProgramAssociation.GetAssociatedProgram(validExtensions[0]) == Application.ExecutablePath;
    }

    extension(ContextMenuStrip menu)
    {
        public void AddItem(string name, Action action)
        {
            menu.Items.Add(ContextMenuButton(name, action));
        }

        public void AddSeparator()
        {
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
