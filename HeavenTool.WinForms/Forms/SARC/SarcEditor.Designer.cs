using AltUI.Controls;

namespace HeavenTool.Forms.SARC
{
    partial class SarcEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SarcEditor));
            headerMenu = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            searchTextBox = new System.Windows.Forms.ToolStripTextBox();
            filesTreeView = new System.Windows.Forms.TreeView();
            headerMenu.SuspendLayout();
            SuspendLayout();
            // 
            // headerMenu
            // 
            headerMenu.ForeColor = System.Drawing.Color.White;
            headerMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, searchTextBox });
            headerMenu.Location = new System.Drawing.Point(0, 0);
            headerMenu.Name = "headerMenu";
            headerMenu.Padding = new System.Windows.Forms.Padding(3, 2, 0, 2);
            headerMenu.Size = new System.Drawing.Size(636, 24);
            headerMenu.TabIndex = 0;
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { openToolStripMenuItem, saveAsToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Image = Properties.Resources.open_file;
            openToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O;
            openToolStripMenuItem.Size = new System.Drawing.Size(154, 30);
            openToolStripMenuItem.Text = "Open";
            openToolStripMenuItem.Click += OpenToolStripMenuItem_Click;
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.Image = Properties.Resources.save;
            saveAsToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.Size = new System.Drawing.Size(154, 30);
            saveAsToolStripMenuItem.Text = "Save as...";
            saveAsToolStripMenuItem.Click += SaveAsToolStripMenuItem_Click;
            // 
            // searchTextBox
            // 
            searchTextBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            searchTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            searchTextBox.Margin = new System.Windows.Forms.Padding(0, 0, 5, 0);
            searchTextBox.Name = "searchTextBox";
            searchTextBox.Size = new System.Drawing.Size(100, 20);
            searchTextBox.Text = "Search";
            searchTextBox.ToolTipText = "Search in SARC";
            // 
            // filesTreeView
            // 
            filesTreeView.BackColor = System.Drawing.Color.FromArgb(35, 35, 36);
            filesTreeView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            filesTreeView.Dock = System.Windows.Forms.DockStyle.Left;
            filesTreeView.ForeColor = System.Drawing.Color.White;
            filesTreeView.Location = new System.Drawing.Point(0, 24);
            filesTreeView.Name = "filesTreeView";
            filesTreeView.Size = new System.Drawing.Size(233, 373);
            filesTreeView.TabIndex = 1;
            // 
            // SarcEditor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(636, 397);
            Controls.Add(filesTreeView);
            Controls.Add(headerMenu);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = headerMenu;
            Name = "SarcEditor";
            Text = "SARC Editor";
            headerMenu.ResumeLayout(false);
            headerMenu.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip headerMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.TreeView filesTreeView;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox searchTextBox;
    }
}