using HeavenTool.Forms.Components;
using HeavenTool.Forms.Editor;
using HeavenTool.Forms.Editor.Containers;
using HeavenTool.Properties;
using HeavenTool.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms;

public partial class EditorForm : Form
{
    private DockableControl<TreeView> Explorer { get; set; }
    private TreeView TreeView { get; } = new TreeView()
    {
        ShowLines = false,
        ShowRootLines = true,
        ShowPlusMinus = true,
        FullRowSelect = true,
        BorderStyle = BorderStyle.None,
        Indent = 6
    };

    public EditorForm()
    {
        InitializeComponent();

        dockPanel.Theme = new VS2015DarkTheme();

        Explorer = DockableControl.Create(TreeView, "Explorer");
        Explorer.Show(dockPanel, DockState.DockLeft);

        TreeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
        TreeView.BeforeExpand += TreeView_BeforeExpand;
        TreeView.NodeMouseClick += TreeView_NodeMouseClick;
        
        var icons = new ImageList()
        {
            Images =
            {
                { "file", Resources.file },
                { "folder", Resources.folder },
                { "zip", Resources.zip },
                { "audio_file", Resources.audio_file },
                { "image_file", Resources.image_file },
                { "spreadsheet_file", Resources.spreadsheet_file }
            }
        };

        TreeView.ImageList = icons;
    }

    private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        if (e.Node == null || e.Node.Tag is not IFileContainer node) return;

        var contextMenu = new ContextMenuStrip();
        var isDirectory = node.IsDirectory;

        if (!isDirectory)
        {
            if (TryLocateEditorDock(node.FullPath, out var dock))
            {
                contextMenu.AddItem("Save", dock.SaveFile);
                dock.BuildContextMenu(contextMenu);
            }
            else
            {
                contextMenu.AddItem("Open", () => OpenNode(node));
            }
        }

        contextMenu.AddItem("Remove", () =>
        {
            if (isDirectory)
            {
                var childEditors = e.Node.Nodes.Cast<TreeNode>();
                var loadedEditors = childEditors
                    .Select(x => x.Tag)
                    .OfType<IFileContainer>()
                    .Select(x => TryLocateEditorDock(x.FullPath, out var dock) ? dock : null!)
                    .Where(dock => dock != null);

                if (loadedEditors.Any() && MessageBox.Show("Removing that file will unload all the parent files, any non-saved changes will be lost!", "Do you want to unload?", MessageBoxButtons.YesNo) == DialogResult.No)
                    return;

                foreach (var editor in loadedEditors)
                {
                    if (editor == null) continue;

                    editor.Dispose();
                }
            }
            e.Node.Remove();
        });

        contextMenu.Show(TreeView, e.Location);
    }

    // Load items just when expanding
    private void TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node == null || e.Node.Tag is not IFileContainer node) return;

        if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "...")
        {
            e.Node.Nodes.Clear();

            var children = node.GetChildren();

            if (children.Count() > 5_000 && 
                MessageBox.Show($"This folder contains {children.Count()} items, loading it can cause the editor to freeze and crash.\n\n" +
                $"Do you really want to continue?", 
                "Too many items!", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Warning, 
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            

            foreach (var child in children)
                e.Node.Nodes.Add(CreateNode(child));
        }
    }

    private static readonly Dictionary<string, string> fileIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".bcsv", "spreadsheet_file" }
    };

    private static string GetIconForFile(IFileContainer container)
    {
        var extension = Path.GetExtension(container.FullPath);

        if (!container.IsDirectory && fileIcons.TryGetValue(extension, out var icon))
            return icon;

        return container switch
        {
            ZipFileNode => "zip",
            _ => "folder",
        };
    }

    private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node == null || e.Node.Tag is not IFileContainer container) return;

        OpenNode(container);
    }

    private bool TryLocateEditorDock(string path, [MaybeNullWhen(false)] out BaseEditor editorDock)
    {
        editorDock = null;

        foreach (var doc in dockPanel.Contents)
        {
            if (doc is not BaseEditor editor) continue;

            if (string.Equals(editor.FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                editorDock = editor;
                return true;
            }
        }

        return false;
    }

    private void LoadFolderToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var folderSelect = new FolderBrowserDialog();

        if (folderSelect.ShowDialog() == DialogResult.OK)
        {
            var path = folderSelect.SelectedPath;

            LoadFolder(path);
        }
    }

    private void LoadFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        var rootDirectory = new DirectoryInfo(path);
        var rootNode = CreateDirectoryNode(rootDirectory);

        TreeView.Nodes.Add(rootNode);
        rootNode.Expand();
    }

    private static TreeNode CreateDirectoryNode(DirectoryInfo directory)
    {
        var node = new PhysicalFileNode(directory.FullName);

        var directoryNode = new TreeNode(node.Name)
        {
            Tag = node,
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };

        directoryNode.Nodes.Add("...");
        return directoryNode;
    }

    private static TreeNode CreateNode(IFileContainer file)
    {
        var node = new TreeNode(file.Name)
        {
            Tag = file,
            ImageKey = GetIconForFile(file),
            SelectedImageKey = GetIconForFile(file)
        };

        if (file.IsDirectory)
            node.Nodes.Add("...");

        return node;
    }

    private void OpenNode(IFileContainer node)
    {
        // Can't open a directory in editor
        if (node.IsDirectory)
            return;

        if (TryLocateEditorDock(node.FullPath, out var dock))
        {
            dock.Activate();
            return;
        }

        if (!EditorFactory.TryCreateEditor(node.Name, out var editor))
        {
            MessageBox.Show("Heaven Tool does not support that file yet.");
            return;
        }


        using var stream = node.OpenRead();
        editor.LoadFile(stream);
        editor.FilePath = node.FullPath;
        editor.Text = node.Name;
        editor.Show(dockPanel, DockState.Document);
    }
}