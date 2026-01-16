using AltUI.Controls;
using System.Windows.Forms;

namespace HeavenTool
{
    partial class BCSVForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BCSVForm));
            topMenuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            importFromFileToolStripMenuItem = new ToolStripMenuItem();
            exportSelectionToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            exportToCSVFileToolStripMenuItem = new ToolStripMenuItem();
            unloadFileToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            searchToolStripMenuItem = new ToolStripMenuItem();
            newEntryToolStripMenuItem = new ToolStripMenuItem();
            duplicateRowToolStripMenuItem = new ToolStripMenuItem();
            deleteRowsToolStripMenuItem = new ToolStripMenuItem();
            compareRowsToolStripMenuItem = new ToolStripMenuItem();
            copyToolStripMenuItem1 = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            viewColumnsMenuItem = new ToolStripMenuItem();
            selectionTypeMenuItem = new ToolStripMenuItem();
            rowSelectToolStripMenuItem = new ToolStripMenuItem();
            cellSelectToolStripMenuItem = new ToolStripMenuItem();
            devToolStripMenuItem = new ToolStripMenuItem();
            associateBcsvToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            searchOnFilesToolStripMenuItem = new ToolStripMenuItem();
            hashFinderToolStripMenuItem = new ToolStripMenuItem();
            statusStripMenu = new StatusStrip();
            infoLabel = new ToolStripStatusLabel();
            versionNumberLabel = new ToolStripStatusLabel();
            mainDataGridView = new DataGridView();
            validHeaderContextMenu = new ContextMenuStrip(components);
            hideColumnToolStripMenuItem = new ToolStripMenuItem();
            viewAsToolStripMenuItem = new ToolStripMenuItem();
            copyToolStripMenuItem = new ToolStripMenuItem();
            headerNameToolStripMenuItem = new ToolStripMenuItem();
            headerHashToolStripMenuItem = new ToolStripMenuItem();
            exportValuesAstxtFileToolStripMenuItem = new ToolStripMenuItem();
            dragInfo = new Label();
            topMenuStrip.SuspendLayout();
            statusStripMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainDataGridView).BeginInit();
            validHeaderContextMenu.SuspendLayout();
            SuspendLayout();
            // 
            // topMenuStrip
            // 
            topMenuStrip.BackColor = System.Drawing.Color.Transparent;
            topMenuStrip.ForeColor = System.Drawing.Color.White;
            topMenuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, devToolStripMenuItem, toolsToolStripMenuItem });
            topMenuStrip.Location = new System.Drawing.Point(0, 0);
            topMenuStrip.Name = "topMenuStrip";
            topMenuStrip.Padding = new Padding(3, 2, 0, 2);
            topMenuStrip.Size = new System.Drawing.Size(544, 24);
            topMenuStrip.TabIndex = 1;
            topMenuStrip.Text = "Top Menu";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem, saveAsToolStripMenuItem, toolStripSeparator2, importFromFileToolStripMenuItem, exportSelectionToolStripMenuItem, toolStripSeparator1, exportToCSVFileToolStripMenuItem, unloadFileToolStripMenuItem });
            fileToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            openToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            openToolStripMenuItem.Image = Properties.Resources.open_file;
            openToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openToolStripMenuItem.Size = new System.Drawing.Size(192, 30);
            openToolStripMenuItem.Text = "&Open";
            openToolStripMenuItem.Click += OpenToolStripMenuItem_Click;
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            saveAsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            saveAsToolStripMenuItem.Image = Properties.Resources.save_as;
            saveAsToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Alt | Keys.S;
            saveAsToolStripMenuItem.Size = new System.Drawing.Size(192, 30);
            saveAsToolStripMenuItem.Text = "Save as...";
            saveAsToolStripMenuItem.Click += SaveAsToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            toolStripSeparator2.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(189, 6);
            // 
            // importFromFileToolStripMenuItem
            // 
            importFromFileToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            importFromFileToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            importFromFileToolStripMenuItem.Image = Properties.Resources.import;
            importFromFileToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            importFromFileToolStripMenuItem.Name = "importFromFileToolStripMenuItem";
            importFromFileToolStripMenuItem.Size = new System.Drawing.Size(192, 30);
            importFromFileToolStripMenuItem.Text = "Import from file...";
            importFromFileToolStripMenuItem.ToolTipText = "Import lines from a BCSV file (headers must match)";
            importFromFileToolStripMenuItem.Click += ImportFromFileToolStripMenuItem_Click;
            // 
            // exportSelectionToolStripMenuItem
            // 
            exportSelectionToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            exportSelectionToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            exportSelectionToolStripMenuItem.Image = Properties.Resources.export;
            exportSelectionToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            exportSelectionToolStripMenuItem.Name = "exportSelectionToolStripMenuItem";
            exportSelectionToolStripMenuItem.Size = new System.Drawing.Size(192, 30);
            exportSelectionToolStripMenuItem.Text = "Export selection...";
            exportSelectionToolStripMenuItem.ToolTipText = "Export the selected lines to a new BCSV file";
            exportSelectionToolStripMenuItem.Click += ExportSelectionToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            toolStripSeparator1.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(189, 6);
            // 
            // exportToCSVFileToolStripMenuItem
            // 
            exportToCSVFileToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            exportToCSVFileToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            exportToCSVFileToolStripMenuItem.Image = Properties.Resources.export_csv;
            exportToCSVFileToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            exportToCSVFileToolStripMenuItem.Name = "exportToCSVFileToolStripMenuItem";
            exportToCSVFileToolStripMenuItem.Size = new System.Drawing.Size(192, 30);
            exportToCSVFileToolStripMenuItem.Text = "Export as .CSV";
            exportToCSVFileToolStripMenuItem.Click += ExportToCSVFileToolStripMenuItem_Click;
            // 
            // unloadFileToolStripMenuItem
            // 
            unloadFileToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            unloadFileToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            unloadFileToolStripMenuItem.Image = Properties.Resources.cancel;
            unloadFileToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            unloadFileToolStripMenuItem.Name = "unloadFileToolStripMenuItem";
            unloadFileToolStripMenuItem.Size = new System.Drawing.Size(192, 30);
            unloadFileToolStripMenuItem.Text = "Unload File";
            unloadFileToolStripMenuItem.Click += UnloadFileToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { searchToolStripMenuItem, newEntryToolStripMenuItem, duplicateRowToolStripMenuItem, deleteRowsToolStripMenuItem, compareRowsToolStripMenuItem, copyToolStripMenuItem1 });
            editToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            editToolStripMenuItem.Text = "Edit";
            editToolStripMenuItem.DropDownOpening += EditToolStripMenuItem_DropDownOpening;
            // 
            // searchToolStripMenuItem
            // 
            searchToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            searchToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            searchToolStripMenuItem.Image = Properties.Resources.search;
            searchToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            searchToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.F;
            searchToolStripMenuItem.Size = new System.Drawing.Size(213, 30);
            searchToolStripMenuItem.Text = "Search";
            searchToolStripMenuItem.ToolTipText = "Search for a line on this file";
            searchToolStripMenuItem.Click += SearchToolStripMenuItem_Click;
            // 
            // newEntryToolStripMenuItem
            // 
            newEntryToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            newEntryToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            newEntryToolStripMenuItem.Image = Properties.Resources.new_entry;
            newEntryToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            newEntryToolStripMenuItem.Name = "newEntryToolStripMenuItem";
            newEntryToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newEntryToolStripMenuItem.Size = new System.Drawing.Size(213, 30);
            newEntryToolStripMenuItem.Text = "New Entry";
            newEntryToolStripMenuItem.ToolTipText = "Add a new entry into this file";
            newEntryToolStripMenuItem.Click += NewEntryToolStripMenuItem_Click;
            // 
            // duplicateRowToolStripMenuItem
            // 
            duplicateRowToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            duplicateRowToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            duplicateRowToolStripMenuItem.Image = Properties.Resources.duplicate;
            duplicateRowToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            duplicateRowToolStripMenuItem.Name = "duplicateRowToolStripMenuItem";
            duplicateRowToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            duplicateRowToolStripMenuItem.Size = new System.Drawing.Size(213, 30);
            duplicateRowToolStripMenuItem.Text = "Duplicate Row(s)";
            duplicateRowToolStripMenuItem.ToolTipText = "Duplicate the selected row(s)";
            duplicateRowToolStripMenuItem.Click += DuplicateRowToolStripMenuItem_Click;
            // 
            // deleteRowsToolStripMenuItem
            // 
            deleteRowsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            deleteRowsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            deleteRowsToolStripMenuItem.Image = Properties.Resources.remove;
            deleteRowsToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            deleteRowsToolStripMenuItem.Name = "deleteRowsToolStripMenuItem";
            deleteRowsToolStripMenuItem.ShortcutKeys = Keys.Delete;
            deleteRowsToolStripMenuItem.Size = new System.Drawing.Size(213, 30);
            deleteRowsToolStripMenuItem.Text = "Delete Row(s)";
            deleteRowsToolStripMenuItem.ToolTipText = "Delete the selected row(s)";
            deleteRowsToolStripMenuItem.Click += DeleteRowsToolStripMenuItem_Click;
            // 
            // compareRowsToolStripMenuItem
            // 
            compareRowsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            compareRowsToolStripMenuItem.Enabled = false;
            compareRowsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(101, 101, 101);
            compareRowsToolStripMenuItem.Image = Properties.Resources.compare;
            compareRowsToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            compareRowsToolStripMenuItem.Name = "compareRowsToolStripMenuItem";
            compareRowsToolStripMenuItem.Size = new System.Drawing.Size(213, 30);
            compareRowsToolStripMenuItem.Text = "Compare Rows";
            compareRowsToolStripMenuItem.ToolTipText = "Compare selected rows in a new window (read-only)";
            compareRowsToolStripMenuItem.Click += CompareRowsToolStripMenuItem_Click;
            // 
            // copyToolStripMenuItem1
            // 
            copyToolStripMenuItem1.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            copyToolStripMenuItem1.ForeColor = System.Drawing.Color.FromArgb(101, 101, 101);
            copyToolStripMenuItem1.Name = "copyToolStripMenuItem1";
            copyToolStripMenuItem1.ShortcutKeys = Keys.Control | Keys.C;
            copyToolStripMenuItem1.Size = new System.Drawing.Size(213, 30);
            copyToolStripMenuItem1.Text = "Copy";
            copyToolStripMenuItem1.Click += CopyToolStripMenuItem_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { viewColumnsMenuItem, selectionTypeMenuItem });
            viewToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // viewColumnsMenuItem
            // 
            viewColumnsMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            viewColumnsMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            viewColumnsMenuItem.Name = "viewColumnsMenuItem";
            viewColumnsMenuItem.Size = new System.Drawing.Size(150, 22);
            viewColumnsMenuItem.Text = "View Columns";
            // 
            // selectionTypeMenuItem
            // 
            selectionTypeMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            selectionTypeMenuItem.DropDownItems.AddRange(new ToolStripItem[] { rowSelectToolStripMenuItem, cellSelectToolStripMenuItem });
            selectionTypeMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            selectionTypeMenuItem.Name = "selectionTypeMenuItem";
            selectionTypeMenuItem.Size = new System.Drawing.Size(150, 22);
            selectionTypeMenuItem.Text = "Selection Type";
            // 
            // rowSelectToolStripMenuItem
            // 
            rowSelectToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            rowSelectToolStripMenuItem.Checked = true;
            rowSelectToolStripMenuItem.CheckState = CheckState.Checked;
            rowSelectToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            rowSelectToolStripMenuItem.Name = "rowSelectToolStripMenuItem";
            rowSelectToolStripMenuItem.Size = new System.Drawing.Size(131, 22);
            rowSelectToolStripMenuItem.Text = "Row Select";
            rowSelectToolStripMenuItem.Click += RowSelectToolStripMenuItem_Click;
            // 
            // cellSelectToolStripMenuItem
            // 
            cellSelectToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            cellSelectToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            cellSelectToolStripMenuItem.Name = "cellSelectToolStripMenuItem";
            cellSelectToolStripMenuItem.Size = new System.Drawing.Size(131, 22);
            cellSelectToolStripMenuItem.Text = "Cell Select";
            cellSelectToolStripMenuItem.Click += CellSelectToolStripMenuItem_Click;
            // 
            // devToolStripMenuItem
            // 
            devToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            devToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { associateBcsvToolStripMenuItem });
            devToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            devToolStripMenuItem.Name = "devToolStripMenuItem";
            devToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            devToolStripMenuItem.Text = "Options";
            // 
            // associateBcsvToolStripMenuItem
            // 
            associateBcsvToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            associateBcsvToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            associateBcsvToolStripMenuItem.Name = "associateBcsvToolStripMenuItem";
            associateBcsvToolStripMenuItem.Size = new System.Drawing.Size(279, 22);
            associateBcsvToolStripMenuItem.Text = "Associate .BCSV files with this program";
            associateBcsvToolStripMenuItem.Click += AssociateBCSVToolStripMenuItem_Click;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { searchOnFilesToolStripMenuItem, hashFinderToolStripMenuItem });
            toolsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new System.Drawing.Size(47, 20);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // searchOnFilesToolStripMenuItem
            // 
            searchOnFilesToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            searchOnFilesToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            searchOnFilesToolStripMenuItem.Image = Properties.Resources.search;
            searchOnFilesToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            searchOnFilesToolStripMenuItem.Name = "searchOnFilesToolStripMenuItem";
            searchOnFilesToolStripMenuItem.Size = new System.Drawing.Size(158, 30);
            searchOnFilesToolStripMenuItem.Text = "Search on files";
            searchOnFilesToolStripMenuItem.Click += SearchOnFilesToolStripMenuItem_Click;
            // 
            // hashFinderToolStripMenuItem
            // 
            hashFinderToolStripMenuItem.Name = "hashFinderToolStripMenuItem";
            hashFinderToolStripMenuItem.Size = new System.Drawing.Size(158, 30);
            hashFinderToolStripMenuItem.Text = "Hash Finder";
            hashFinderToolStripMenuItem.Click += HashFinderToolStripMenuItem_Click;
            // 
            // statusStripMenu
            // 
            statusStripMenu.AutoSize = false;
            statusStripMenu.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            statusStripMenu.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            statusStripMenu.Items.AddRange(new ToolStripItem[] { infoLabel, versionNumberLabel });
            statusStripMenu.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            statusStripMenu.Location = new System.Drawing.Point(0, 306);
            statusStripMenu.Name = "statusStripMenu";
            statusStripMenu.Padding = new Padding(0, 5, 0, 3);
            statusStripMenu.Size = new System.Drawing.Size(544, 30);
            statusStripMenu.SizingGrip = false;
            statusStripMenu.TabIndex = 2;
            // 
            // infoLabel
            // 
            infoLabel.BackColor = System.Drawing.Color.Transparent;
            infoLabel.Name = "infoLabel";
            infoLabel.Size = new System.Drawing.Size(28, 17);
            infoLabel.Text = "Info";
            // 
            // versionNumberLabel
            // 
            versionNumberLabel.Alignment = ToolStripItemAlignment.Right;
            versionNumberLabel.BackColor = System.Drawing.Color.Transparent;
            versionNumberLabel.Name = "versionNumberLabel";
            versionNumberLabel.Size = new System.Drawing.Size(31, 17);
            versionNumberLabel.Text = "1.0.0";
            // 
            // mainDataGridView
            // 
            mainDataGridView.AllowUserToAddRows = false;
            mainDataGridView.AllowUserToDeleteRows = false;
            mainDataGridView.AllowUserToOrderColumns = true;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            mainDataGridView.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            mainDataGridView.BackgroundColor = System.Drawing.Color.FromArgb(31, 31, 31);
            mainDataGridView.BorderStyle = BorderStyle.None;
            mainDataGridView.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
            mainDataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(80, 80, 80);
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(240, 240, 240);
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            mainDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            mainDataGridView.ColumnHeadersHeight = 25;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            mainDataGridView.DefaultCellStyle = dataGridViewCellStyle3;
            mainDataGridView.Dock = DockStyle.Fill;
            mainDataGridView.EnableHeadersVisualStyles = false;
            mainDataGridView.GridColor = System.Drawing.Color.FromArgb(60, 60, 60);
            mainDataGridView.Location = new System.Drawing.Point(0, 24);
            mainDataGridView.Margin = new Padding(4, 3, 4, 3);
            mainDataGridView.Name = "mainDataGridView";
            mainDataGridView.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.Color.FromArgb(70, 70, 70);
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            dataGridViewCellStyle4.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.True;
            mainDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            mainDataGridView.RowHeadersWidth = 25;
            mainDataGridView.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridViewCellStyle5.BackColor = System.Drawing.Color.FromArgb(51, 51, 51);
            dataGridViewCellStyle5.ForeColor = System.Drawing.Color.White;
            mainDataGridView.RowsDefaultCellStyle = dataGridViewCellStyle5;
            mainDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            mainDataGridView.Size = new System.Drawing.Size(544, 282);
            mainDataGridView.StandardTab = true;
            mainDataGridView.TabIndex = 4;
            // 
            // validHeaderContextMenu
            // 
            validHeaderContextMenu.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
            validHeaderContextMenu.Items.AddRange(new ToolStripItem[] { hideColumnToolStripMenuItem, viewAsToolStripMenuItem, copyToolStripMenuItem, exportValuesAstxtFileToolStripMenuItem });
            validHeaderContextMenu.Name = "validHeaderContextMenu";
            validHeaderContextMenu.Size = new System.Drawing.Size(196, 92);
            // 
            // hideColumnToolStripMenuItem
            // 
            hideColumnToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            hideColumnToolStripMenuItem.Name = "hideColumnToolStripMenuItem";
            hideColumnToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            hideColumnToolStripMenuItem.Text = "Hide Column";
            hideColumnToolStripMenuItem.Click += HideColumnToolStripMenuItem_Click;
            // 
            // viewAsToolStripMenuItem
            // 
            viewAsToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            viewAsToolStripMenuItem.Name = "viewAsToolStripMenuItem";
            viewAsToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            viewAsToolStripMenuItem.Text = "View as";
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { headerNameToolStripMenuItem, headerHashToolStripMenuItem });
            copyToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            copyToolStripMenuItem.Text = "Copy";
            // 
            // headerNameToolStripMenuItem
            // 
            headerNameToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            headerNameToolStripMenuItem.Name = "headerNameToolStripMenuItem";
            headerNameToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            headerNameToolStripMenuItem.Text = "Header Name";
            headerNameToolStripMenuItem.Click += HeaderNameToolStripMenuItem_Click;
            // 
            // headerHashToolStripMenuItem
            // 
            headerHashToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            headerHashToolStripMenuItem.Name = "headerHashToolStripMenuItem";
            headerHashToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            headerHashToolStripMenuItem.Text = "Header Hash";
            headerHashToolStripMenuItem.Click += HeaderHashToolStripMenuItem_Click;
            // 
            // exportValuesAstxtFileToolStripMenuItem
            // 
            exportValuesAstxtFileToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            exportValuesAstxtFileToolStripMenuItem.Name = "exportValuesAstxtFileToolStripMenuItem";
            exportValuesAstxtFileToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            exportValuesAstxtFileToolStripMenuItem.Text = "Export values as .txt file";
            exportValuesAstxtFileToolStripMenuItem.Click += ExportValuesAstxtFileToolStripMenuItem_Click;
            // 
            // dragInfo
            // 
            dragInfo.BackColor = System.Drawing.Color.FromArgb(31, 31, 31);
            dragInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.25F);
            dragInfo.ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);
            dragInfo.Location = new System.Drawing.Point(13, 224);
            dragInfo.Margin = new Padding(4, 0, 4, 0);
            dragInfo.Name = "dragInfo";
            dragInfo.Size = new System.Drawing.Size(211, 62);
            dragInfo.TabIndex = 5;
            dragInfo.Text = "Drag a file here\r\nor use File > Open";
            dragInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // BCSVForm
            // 
            AllowDrop = true;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(544, 336);
            Controls.Add(dragInfo);
            Controls.Add(mainDataGridView);
            Controls.Add(statusStripMenu);
            Controls.Add(topMenuStrip);
            ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = topMenuStrip;
            Margin = new Padding(4, 3, 4, 3);
            Name = "BCSVForm";
            Text = "ACNH Heaven Tool | v1.0.0 | BCSV Editor";
            TransparencyKey = System.Drawing.Color.Pink;
            FormClosing += MainFrm_FormClosing;
            DragDrop += MainFrm_DragDrop;
            DragEnter += MainFrm_DragEnter;
            topMenuStrip.ResumeLayout(false);
            topMenuStrip.PerformLayout();
            statusStripMenu.ResumeLayout(false);
            statusStripMenu.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)mainDataGridView).EndInit();
            validHeaderContextMenu.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private MenuStrip topMenuStrip;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private StatusStrip statusStripMenu;
        private ToolStripMenuItem toolsToolStripMenuItem;
        private DataGridView mainDataGridView;
        private ToolStripStatusLabel infoLabel;
        private ToolStripMenuItem searchOnFilesToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem newEntryToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem unloadFileToolStripMenuItem;
        private ToolStripMenuItem devToolStripMenuItem;
        private ToolStripMenuItem duplicateRowToolStripMenuItem;
        private ToolStripMenuItem associateBcsvToolStripMenuItem;
        private ToolStripMenuItem deleteRowsToolStripMenuItem;
        private ContextMenuStrip validHeaderContextMenu;
        private ToolStripMenuItem exportValuesAstxtFileToolStripMenuItem;
        private ToolStripStatusLabel versionNumberLabel;
        private ToolStripMenuItem exportToCSVFileToolStripMenuItem;
        private ToolStripMenuItem compareRowsToolStripMenuItem;
        private ToolStripMenuItem searchToolStripMenuItem;
        private ToolStripMenuItem importFromFileToolStripMenuItem;
        private ToolStripMenuItem exportSelectionToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem headerNameToolStripMenuItem;
        private ToolStripMenuItem headerHashToolStripMenuItem;
        private ToolStripMenuItem viewAsToolStripMenuItem;
        private Label dragInfo;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem viewColumnsMenuItem;
        private ToolStripMenuItem hideColumnToolStripMenuItem;
        private ToolStripMenuItem selectionTypeMenuItem;
        private ToolStripMenuItem rowSelectToolStripMenuItem;
        private ToolStripMenuItem cellSelectToolStripMenuItem;
        private ToolStripMenuItem hashFinderToolStripMenuItem;
        private ToolStripMenuItem copyToolStripMenuItem1;
    }
}

