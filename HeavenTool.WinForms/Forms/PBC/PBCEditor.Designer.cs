using AltUI.Controls;

namespace HeavenTool.Forms.PBC
{
    partial class PBCEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PBCEditor));
            darkMenuStrip1 = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            currentZoomMenu = new System.Windows.Forms.ToolStripMenuItem();
            zoomPlusButton = new System.Windows.Forms.ToolStripMenuItem();
            zoomMinusButton = new System.Windows.Forms.ToolStripMenuItem();
            viewMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            viewIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            gridToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            dockPanel = new WeifenLuo.WinFormsUI.Docking.DockPanel();
            darkMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // darkMenuStrip1
            // 
            darkMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, currentZoomMenu, viewMenuItem });
            darkMenuStrip1.Location = new System.Drawing.Point(0, 0);
            darkMenuStrip1.Name = "darkMenuStrip1";
            darkMenuStrip1.Padding = new System.Windows.Forms.Padding(3, 2, 0, 2);
            darkMenuStrip1.Size = new System.Drawing.Size(764, 24);
            darkMenuStrip1.TabIndex = 5;
            darkMenuStrip1.Text = "darkMenuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { saveToolStripMenuItem, undoToolStripMenuItem, redoToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            // 
            // undoToolStripMenuItem
            // 
            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z;
            undoToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += UndoToolStripMenuItem_Click;
            // 
            // redoToolStripMenuItem
            // 
            redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            redoToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y;
            redoToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            redoToolStripMenuItem.Text = "Redo";
            redoToolStripMenuItem.Click += RedoToolStripMenuItem_Click;
            // 
            // currentZoomMenu
            // 
            currentZoomMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { zoomPlusButton, zoomMinusButton });
            currentZoomMenu.Name = "currentZoomMenu";
            currentZoomMenu.Size = new System.Drawing.Size(68, 20);
            currentZoomMenu.Text = "Zoom: 5x";
            // 
            // zoomPlusButton
            // 
            zoomPlusButton.Name = "zoomPlusButton";
            zoomPlusButton.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Oemplus;
            zoomPlusButton.Size = new System.Drawing.Size(172, 22);
            zoomPlusButton.Text = "+";
            zoomPlusButton.Click += ZoomPlusButton_Click;
            // 
            // zoomMinusButton
            // 
            zoomMinusButton.Name = "zoomMinusButton";
            zoomMinusButton.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.OemMinus;
            zoomMinusButton.Size = new System.Drawing.Size(172, 22);
            zoomMinusButton.Text = "-";
            zoomMinusButton.Click += ZoomMinusButton_Click;
            // 
            // viewMenuItem
            // 
            viewMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { viewIDToolStripMenuItem, gridToolStripMenuItem });
            viewMenuItem.Name = "viewMenuItem";
            viewMenuItem.Size = new System.Drawing.Size(44, 20);
            viewMenuItem.Text = "View";
            // 
            // viewIDToolStripMenuItem
            // 
            viewIDToolStripMenuItem.Name = "viewIDToolStripMenuItem";
            viewIDToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            viewIDToolStripMenuItem.Text = "Show ID";
            viewIDToolStripMenuItem.Click += ViewIDToolStripMenuItem_Click;
            // 
            // gridToolStripMenuItem
            // 
            gridToolStripMenuItem.Name = "gridToolStripMenuItem";
            gridToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            gridToolStripMenuItem.Text = "Show Grid";
            gridToolStripMenuItem.Click += GridToolStripMenuItem_Click;
            // 
            // dockPanel
            // 
            dockPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            dockPanel.Location = new System.Drawing.Point(0, 24);
            dockPanel.Name = "dockPanel";
            dockPanel.Size = new System.Drawing.Size(764, 548);
            dockPanel.TabIndex = 8;
            // 
            // PBCEditorNew
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(764, 572);
            Controls.Add(dockPanel);
            Controls.Add(darkMenuStrip1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = darkMenuStrip1;
            Name = "PBCEditorNew";
            Text = "PBC Editor";
            darkMenuStrip1.ResumeLayout(false);
            darkMenuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.MenuStrip darkMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem currentZoomMenu;
        private System.Windows.Forms.ToolStripMenuItem zoomPlusButton;
        private System.Windows.Forms.ToolStripMenuItem zoomMinusButton;
        private System.Windows.Forms.ToolStripMenuItem viewMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gridToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel;
    }
}