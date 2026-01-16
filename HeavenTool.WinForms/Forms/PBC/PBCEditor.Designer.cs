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
            saveButton = new DarkButton();
            fileInfoBar = new System.Windows.Forms.StatusStrip();
            statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            darkMenuStrip1 = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            currentZoomMenu = new System.Windows.Forms.ToolStripMenuItem();
            zoomPlusButton = new System.Windows.Forms.ToolStripMenuItem();
            zoomMinusButton = new System.Windows.Forms.ToolStripMenuItem();
            viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            collisionMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            heightMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            viewIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            gridToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            pbcPreview = new TileEditor();
            colorList = new DarkListBox();
            fileInfoBar.SuspendLayout();
            darkMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // propertyGrid
            // 
            propertyGrid.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            propertyGrid.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            propertyGrid.CategoryForeColor = System.Drawing.Color.FromArgb(230, 230, 230);
            propertyGrid.CategorySplitterColor = System.Drawing.Color.FromArgb(40, 40, 40);
            propertyGrid.DisabledItemForeColor = System.Drawing.Color.FromArgb(127, 245, 245, 245);
            propertyGrid.HelpBackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            propertyGrid.HelpBorderColor = System.Drawing.Color.FromArgb(60, 60, 60);
            propertyGrid.HelpForeColor = System.Drawing.Color.White;
            propertyGrid.HelpVisible = false;
            propertyGrid.LineColor = System.Drawing.Color.FromArgb(40, 40, 40);
            propertyGrid.Location = new System.Drawing.Point(531, 342);
            propertyGrid.Name = "propertyGrid";
            propertyGrid.Size = new System.Drawing.Size(221, 146);
            propertyGrid.TabIndex = 2;
            propertyGrid.ViewBackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            propertyGrid.ViewBorderColor = System.Drawing.Color.FromArgb(60, 60, 60);
            propertyGrid.ViewForeColor = System.Drawing.Color.WhiteSmoke;
            // 
            // saveButton
            // 
            saveButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            saveButton.BorderColour = System.Drawing.Color.Empty;
            saveButton.CustomColour = false;
            saveButton.FlatBottom = false;
            saveButton.FlatTop = false;
            saveButton.ForeColor = System.Drawing.SystemColors.ButtonFace;
            saveButton.Location = new System.Drawing.Point(531, 494);
            saveButton.Name = "saveButton";
            saveButton.Padding = new System.Windows.Forms.Padding(5);
            saveButton.Size = new System.Drawing.Size(221, 30);
            saveButton.TabIndex = 3;
            saveButton.Text = "Save";
            saveButton.Click += saveButton_Click;
            // 
            // fileInfoBar
            // 
            fileInfoBar.AutoSize = false;
            fileInfoBar.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            fileInfoBar.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            fileInfoBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusLabel });
            fileInfoBar.Location = new System.Drawing.Point(0, 527);
            fileInfoBar.Name = "fileInfoBar";
            fileInfoBar.Padding = new System.Windows.Forms.Padding(0, 5, 0, 3);
            fileInfoBar.Size = new System.Drawing.Size(764, 32);
            fileInfoBar.SizingGrip = false;
            fileInfoBar.TabIndex = 4;
            fileInfoBar.Text = "Information";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(121, 19);
            statusLabel.Text = "Width: -1 | Height: -1 ";
            // 
            // darkMenuStrip1
            // 
            darkMenuStrip1.BackColor = System.Drawing.Color.Transparent;
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
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { saveToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
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
            zoomPlusButton.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            zoomPlusButton.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            zoomPlusButton.Name = "zoomPlusButton";
            zoomPlusButton.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Oemplus;
            zoomPlusButton.Size = new System.Drawing.Size(180, 22);
            zoomPlusButton.Text = "+";
            zoomPlusButton.Click += zoomPlusButton_Click;
            // 
            // zoomMinusButton
            // 
            zoomMinusButton.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            zoomMinusButton.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            zoomMinusButton.Name = "zoomMinusButton";
            zoomMinusButton.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.OemMinus;
            zoomMinusButton.Size = new System.Drawing.Size(180, 22);
            zoomMinusButton.Text = "-";
            zoomMinusButton.Click += zoomMinusButton_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { collisionMapToolStripMenuItem, heightMapToolStripMenuItem, toolStripSeparator1, viewIDToolStripMenuItem, gridToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // collisionMapToolStripMenuItem
            // 
            collisionMapToolStripMenuItem.Name = "collisionMapToolStripMenuItem";
            collisionMapToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            collisionMapToolStripMenuItem.Text = "Collision Map";
            collisionMapToolStripMenuItem.Click += collisionMapToolStripMenuItem_Click;
            // 
            // heightMapToolStripMenuItem
            // 
            heightMapToolStripMenuItem.Name = "heightMapToolStripMenuItem";
            heightMapToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            heightMapToolStripMenuItem.Text = "Height Map";
            heightMapToolStripMenuItem.Click += heightMapToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // viewIDToolStripMenuItem
            // 
            viewIDToolStripMenuItem.Name = "viewIDToolStripMenuItem";
            viewIDToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            viewIDToolStripMenuItem.Text = "Show ID";
            viewIDToolStripMenuItem.Click += viewIDToolStripMenuItem_Click;
            // 
            // gridToolStripMenuItem
            // 
            gridToolStripMenuItem.Name = "gridToolStripMenuItem";
            gridToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            gridToolStripMenuItem.Text = "Show Grid";
            gridToolStripMenuItem.Click += gridToolStripMenuItem_Click;
            // 
            // pbcPreview
            // 
            pbcPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pbcPreview.BackColor = System.Drawing.Color.Black;
            pbcPreview.CurrentView = IO.FileFormats.PBC.ViewType.Collision;
            pbcPreview.Location = new System.Drawing.Point(12, 37);
            pbcPreview.Name = "pbcPreview";
            pbcPreview.Size = new System.Drawing.Size(513, 487);
            pbcPreview.TabIndex = 6;
            pbcPreview.Text = "tileEditor1";
            // 
            // colorList
            // 
            colorList.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            colorList.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            colorList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            colorList.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            colorList.ForeColor = System.Drawing.Color.White;
            colorList.FormattingEnabled = true;
            colorList.IntegralHeight = false;
            colorList.ItemHeight = 15;
            colorList.Location = new System.Drawing.Point(531, 27);
            colorList.Name = "colorList";
            colorList.Size = new System.Drawing.Size(221, 309);
            colorList.TabIndex = 7;
            colorList.DrawItem += colorList_DrawItem;
            colorList.SelectedIndexChanged += colorList_SelectedIndexChanged;
            // 
            // PBCEditor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(764, 559);
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
        private DarkButton saveButton;
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
        private DarkListBox colorList;
    }
}