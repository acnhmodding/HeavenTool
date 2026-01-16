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
            darkMenuStrip1 = new DarkMenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            searchTextBox = new System.Windows.Forms.ToolStripTextBox();
            filesTreeView = new DarkTreeView();
            darkMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // darkMenuStrip1
            // 
            darkMenuStrip1.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            darkMenuStrip1.ForeColor = System.Drawing.Color.White;
            darkMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, searchTextBox });
            darkMenuStrip1.Location = new System.Drawing.Point(0, 0);
            darkMenuStrip1.Name = "darkMenuStrip1";
            darkMenuStrip1.Padding = new System.Windows.Forms.Padding(3, 2, 0, 2);
            darkMenuStrip1.Size = new System.Drawing.Size(636, 24);
            darkMenuStrip1.TabIndex = 0;
            darkMenuStrip1.Text = "darkMenuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { openToolStripMenuItem, saveAsToolStripMenuItem });
            fileToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.ForeColor = System.Drawing.Color.White;
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
            saveAsToolStripMenuItem.ForeColor = System.Drawing.Color.White;
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
            searchTextBox.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            searchTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            searchTextBox.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            searchTextBox.Margin = new System.Windows.Forms.Padding(0, 0, 5, 0);
            searchTextBox.Name = "searchTextBox";
            searchTextBox.Size = new System.Drawing.Size(100, 20);
            searchTextBox.Text = "Search";
            searchTextBox.ToolTipText = "Search in SARC";
            searchTextBox.Click += searchTextBox_Click;
            // 
            // filesTreeView
            // 
            filesTreeView.BackColor = System.Drawing.Color.FromArgb(35, 35, 36);
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
            Controls.Add(darkMenuStrip1);
            ForeColor = System.Drawing.Color.White;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = darkMenuStrip1;
            Name = "SarcEditor";
            Text = "SARC Editor";
            darkMenuStrip1.ResumeLayout(false);
            darkMenuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DarkMenuStrip darkMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private DarkTreeView filesTreeView;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox searchTextBox;
    }
}