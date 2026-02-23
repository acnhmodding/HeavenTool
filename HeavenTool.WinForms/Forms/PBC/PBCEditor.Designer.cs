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
            propertyGrid = new System.Windows.Forms.PropertyGrid();
            saveButton = new System.Windows.Forms.Button();
            fileInfoBar = new System.Windows.Forms.StatusStrip();
            statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            darkMenuStrip1 = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            currentZoomMenu = new System.Windows.Forms.ToolStripMenuItem();
            zoomPlusButton = new System.Windows.Forms.ToolStripMenuItem();
            zoomMinusButton = new System.Windows.Forms.ToolStripMenuItem();
            viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            collisionMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            heightMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            viewIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            gridToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            layer0ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            layer1ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            layer2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            pbcPreview = new TileEditor();
            colorList = new System.Windows.Forms.ListBox();
            fileInfoBar.SuspendLayout();
            darkMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // propertyGrid
            // 
            propertyGrid.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            propertyGrid.BackColor = System.Drawing.SystemColors.Control;
            propertyGrid.HelpVisible = false;
            propertyGrid.Location = new System.Drawing.Point(531, 324);
            propertyGrid.Name = "propertyGrid";
            propertyGrid.Size = new System.Drawing.Size(221, 177);
            propertyGrid.TabIndex = 2;
            // 
            // saveButton
            // 
            saveButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            saveButton.BackColor = System.Drawing.Color.FromArgb(31, 31, 31);
            saveButton.Location = new System.Drawing.Point(531, 507);
            saveButton.Name = "saveButton";
            saveButton.Padding = new System.Windows.Forms.Padding(5);
            saveButton.Size = new System.Drawing.Size(221, 30);
            saveButton.TabIndex = 3;
            saveButton.Text = "Save";
            saveButton.UseVisualStyleBackColor = false;
            saveButton.Click += SaveButton_Click;
            // 
            // fileInfoBar
            // 
            fileInfoBar.AutoSize = false;
            fileInfoBar.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            fileInfoBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusLabel });
            fileInfoBar.Location = new System.Drawing.Point(0, 544);
            fileInfoBar.Name = "fileInfoBar";
            fileInfoBar.Padding = new System.Windows.Forms.Padding(0, 5, 0, 3);
            fileInfoBar.Size = new System.Drawing.Size(764, 28);
            fileInfoBar.SizingGrip = false;
            fileInfoBar.TabIndex = 4;
            fileInfoBar.Text = "Information";
            // 
            // statusLabel
            // 
            statusLabel.Margin = new System.Windows.Forms.Padding(0);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(121, 20);
            statusLabel.Text = "Width: -1 | Height: -1 ";
            // 
            // darkMenuStrip1
            // 
            darkMenuStrip1.BackColor = System.Drawing.Color.FromArgb(20, 20, 20);
            darkMenuStrip1.ForeColor = System.Drawing.Color.White;
            darkMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, currentZoomMenu, viewToolStripMenuItem });
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
            saveToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            // 
            // undoToolStripMenuItem
            // 
            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z;
            undoToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += UndoToolStripMenuItem_Click;
            // 
            // redoToolStripMenuItem
            // 
            redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            redoToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y;
            redoToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
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
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { collisionMapToolStripMenuItem, heightMapToolStripMenuItem, toolStripSeparator1, viewIDToolStripMenuItem, gridToolStripMenuItem, toolStripSeparator2, layer0ToolStripMenuItem, layer1ToolStripMenuItem, layer2ToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // collisionMapToolStripMenuItem
            // 
            collisionMapToolStripMenuItem.Name = "collisionMapToolStripMenuItem";
            collisionMapToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            collisionMapToolStripMenuItem.Text = "Collision Map";
            collisionMapToolStripMenuItem.Click += CollisionMapToolStripMenuItem_Click;
            // 
            // heightMapToolStripMenuItem
            // 
            heightMapToolStripMenuItem.Name = "heightMapToolStripMenuItem";
            heightMapToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            heightMapToolStripMenuItem.Text = "Height Map";
            heightMapToolStripMenuItem.Click += HeightMapToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(144, 6);
            // 
            // viewIDToolStripMenuItem
            // 
            viewIDToolStripMenuItem.Name = "viewIDToolStripMenuItem";
            viewIDToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            viewIDToolStripMenuItem.Text = "Show ID";
            viewIDToolStripMenuItem.Click += ViewIDToolStripMenuItem_Click;
            // 
            // gridToolStripMenuItem
            // 
            gridToolStripMenuItem.Name = "gridToolStripMenuItem";
            gridToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            gridToolStripMenuItem.Text = "Show Grid";
            gridToolStripMenuItem.Click += GridToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(144, 6);
            // 
            // layer0ToolStripMenuItem
            // 
            layer0ToolStripMenuItem.Name = "layer0ToolStripMenuItem";
            layer0ToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            layer0ToolStripMenuItem.Text = "Layer 0";
            layer0ToolStripMenuItem.Click += Layer0ToolStripMenuItem_Click;
            // 
            // layer1ToolStripMenuItem
            // 
            layer1ToolStripMenuItem.Name = "layer1ToolStripMenuItem";
            layer1ToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            layer1ToolStripMenuItem.Text = "Layer 1";
            layer1ToolStripMenuItem.Click += Layer1ToolStripMenuItem_Click;
            // 
            // layer2ToolStripMenuItem
            // 
            layer2ToolStripMenuItem.Name = "layer2ToolStripMenuItem";
            layer2ToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            layer2ToolStripMenuItem.Text = "Layer 2";
            layer2ToolStripMenuItem.Click += Layer2ToolStripMenuItem_Click;
            // 
            // pbcPreview
            // 
            pbcPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pbcPreview.BackColor = System.Drawing.Color.Black;
            pbcPreview.CurrentView = ViewType.Collision;
            pbcPreview.DisplayGrid = false;
            pbcPreview.LayerView = LayerView.Layer0;
            pbcPreview.Location = new System.Drawing.Point(12, 27);
            pbcPreview.Name = "pbcPreview";
            pbcPreview.ShowType = false;
            pbcPreview.Size = new System.Drawing.Size(513, 510);
            pbcPreview.TabIndex = 6;
            pbcPreview.Text = "tileEditor1";
            pbcPreview.Zoom = 0;
            // 
            // colorList
            // 
            colorList.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            colorList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            colorList.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            colorList.FormattingEnabled = true;
            colorList.IntegralHeight = false;
            colorList.ItemHeight = 15;
            colorList.Location = new System.Drawing.Point(531, 27);
            colorList.Name = "colorList";
            colorList.Size = new System.Drawing.Size(221, 291);
            colorList.TabIndex = 7;
            colorList.DrawItem += ColorList_DrawItem;
            colorList.SelectedIndexChanged += ColorList_SelectedIndexChanged;
            // 
            // PBCEditor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(764, 572);
            Controls.Add(colorList);
            Controls.Add(pbcPreview);
            Controls.Add(fileInfoBar);
            Controls.Add(darkMenuStrip1);
            Controls.Add(saveButton);
            Controls.Add(propertyGrid);
            ForeColor = System.Drawing.Color.White;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = darkMenuStrip1;
            Name = "PBCEditor";
            Text = "PBC Editor";
            fileInfoBar.ResumeLayout(false);
            fileInfoBar.PerformLayout();
            darkMenuStrip1.ResumeLayout(false);
            darkMenuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.StatusStrip fileInfoBar;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.MenuStrip darkMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem currentZoomMenu;
        private System.Windows.Forms.ToolStripMenuItem zoomPlusButton;
        private System.Windows.Forms.ToolStripMenuItem zoomMinusButton;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collisionMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem heightMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem viewIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gridToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private TileEditor pbcPreview;
        private System.Windows.Forms.ListBox colorList;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem layer0ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem layer1ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem layer2ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
    }
}