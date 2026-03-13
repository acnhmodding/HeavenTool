namespace HeavenTool.Forms.BARS
{
    partial class BARSWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BARSWindow));
            darkMenuStrip1 = new HeavenTool.Forms.Components.DarkMenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            unloadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            itemPropertyGrid = new System.Windows.Forms.PropertyGrid();
            barsTreeView = new System.Windows.Forms.TreeView();
            barsContainer = new System.Windows.Forms.SplitContainer();
            timeLabel = new System.Windows.Forms.Label();
            playButton = new AltUI.Controls.DarkButton();
            waveViewer = new HeavenTool.Forms.Components.CustomWaveViewer();
            darkMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)barsContainer).BeginInit();
            barsContainer.Panel1.SuspendLayout();
            barsContainer.Panel2.SuspendLayout();
            barsContainer.SuspendLayout();
            SuspendLayout();
            // 
            // darkMenuStrip1
            // 
            darkMenuStrip1.BackColor = System.Drawing.Color.Transparent;
            darkMenuStrip1.ForeColor = System.Drawing.Color.White;
            darkMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, optionsToolStripMenuItem });
            darkMenuStrip1.Location = new System.Drawing.Point(0, 0);
            darkMenuStrip1.Name = "darkMenuStrip1";
            darkMenuStrip1.Size = new System.Drawing.Size(500, 24);
            darkMenuStrip1.TabIndex = 1;
            darkMenuStrip1.Text = "darkMenuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { openToolStripMenuItem, saveToolStripMenuItem, toolStripSeparator1, unloadToolStripMenuItem });
            fileToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            openToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            openToolStripMenuItem.Image = Properties.Resources.open_file;
            openToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            openToolStripMenuItem.Text = "Open...";
            openToolStripMenuItem.Click += OpenToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            saveToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            saveToolStripMenuItem.Image = Properties.Resources.save_as;
            saveToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            saveToolStripMenuItem.Text = "Save As...";
            saveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            toolStripSeparator1.ForeColor = System.Drawing.Color.White;
            toolStripSeparator1.Margin = new System.Windows.Forms.Padding(0, 0, 0, 1);
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(185, 6);
            // 
            // unloadToolStripMenuItem
            // 
            unloadToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            unloadToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            unloadToolStripMenuItem.Image = Properties.Resources.cancel;
            unloadToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            unloadToolStripMenuItem.Name = "unloadToolStripMenuItem";
            unloadToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            unloadToolStripMenuItem.Text = "Unload";
            unloadToolStripMenuItem.Click += UnloadToolStripMenuItem_Click;
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            optionsToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            optionsToolStripMenuItem.Text = "Options";
            // 
            // itemPropertyGrid
            // 
            itemPropertyGrid.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            itemPropertyGrid.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            itemPropertyGrid.CanShowVisualStyleGlyphs = false;
            itemPropertyGrid.CategoryForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            itemPropertyGrid.CategorySplitterColor = System.Drawing.Color.FromArgb(31, 31, 32);
            itemPropertyGrid.CausesValidation = false;
            itemPropertyGrid.CommandsForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            itemPropertyGrid.CommandsVisibleIfAvailable = false;
            itemPropertyGrid.DisabledItemForeColor = System.Drawing.Color.FromArgb(127, 255, 255, 255);
            itemPropertyGrid.HelpBackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            itemPropertyGrid.HelpForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            itemPropertyGrid.HelpVisible = false;
            itemPropertyGrid.LineColor = System.Drawing.Color.FromArgb(26, 26, 26);
            itemPropertyGrid.Location = new System.Drawing.Point(0, 5);
            itemPropertyGrid.Margin = new System.Windows.Forms.Padding(3, 3, 3, 1);
            itemPropertyGrid.Name = "itemPropertyGrid";
            itemPropertyGrid.SelectedItemWithFocusForeColor = System.Drawing.Color.Black;
            itemPropertyGrid.Size = new System.Drawing.Size(271, 339);
            itemPropertyGrid.TabIndex = 2;
            itemPropertyGrid.ToolbarVisible = false;
            itemPropertyGrid.ViewBackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            itemPropertyGrid.ViewForeColor = System.Drawing.Color.White;
            // 
            // barsTreeView
            // 
            barsTreeView.BackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            barsTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            barsTreeView.Location = new System.Drawing.Point(5, 5);
            barsTreeView.Name = "barsTreeView";
            barsTreeView.Size = new System.Drawing.Size(202, 411);
            barsTreeView.TabIndex = 3;
            // 
            // barsContainer
            // 
            barsContainer.CausesValidation = false;
            barsContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            barsContainer.Location = new System.Drawing.Point(0, 24);
            barsContainer.Margin = new System.Windows.Forms.Padding(4);
            barsContainer.Name = "barsContainer";
            // 
            // barsContainer.Panel1
            // 
            barsContainer.Panel1.CausesValidation = false;
            barsContainer.Panel1.Controls.Add(barsTreeView);
            barsContainer.Panel1.Padding = new System.Windows.Forms.Padding(5, 5, 0, 5);
            // 
            // barsContainer.Panel2
            // 
            barsContainer.Panel2.Controls.Add(timeLabel);
            barsContainer.Panel2.Controls.Add(playButton);
            barsContainer.Panel2.Controls.Add(waveViewer);
            barsContainer.Panel2.Controls.Add(itemPropertyGrid);
            barsContainer.Panel2.Padding = new System.Windows.Forms.Padding(0, 5, 5, 5);
            barsContainer.Size = new System.Drawing.Size(500, 421);
            barsContainer.SplitterDistance = 207;
            barsContainer.SplitterWidth = 6;
            barsContainer.TabIndex = 4;
            // 
            // timeLabel
            // 
            timeLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            timeLabel.Location = new System.Drawing.Point(142, 351);
            timeLabel.Name = "timeLabel";
            timeLabel.Size = new System.Drawing.Size(129, 15);
            timeLabel.TabIndex = 7;
            timeLabel.Text = "0:000:000";
            timeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // playButton
            // 
            playButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            playButton.BackgroundImage = Properties.Resources.play;
            playButton.BorderColour = System.Drawing.Color.Empty;
            playButton.ButtonStyle = AltUI.Controls.DarkButtonStyle.Image;
            playButton.Enabled = false;
            playButton.ImagePadding = 0;
            playButton.Location = new System.Drawing.Point(0, 347);
            playButton.Name = "playButton";
            playButton.Padding = new System.Windows.Forms.Padding(5);
            playButton.Size = new System.Drawing.Size(23, 23);
            playButton.TabIndex = 6;
            playButton.Click += PlayStopButton_Click;
            // 
            // waveViewer
            // 
            waveViewer.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            waveViewer.BackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            waveViewer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            waveViewer.Location = new System.Drawing.Point(0, 373);
            waveViewer.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            waveViewer.Name = "waveViewer";
            waveViewer.Size = new System.Drawing.Size(271, 43);
            waveViewer.StartPosition = 0L;
            waveViewer.TabIndex = 3;
            // 
            // BARSWindow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(500, 445);
            Controls.Add(barsContainer);
            Controls.Add(darkMenuStrip1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = darkMenuStrip1;
            MinimumSize = new System.Drawing.Size(430, 200);
            Name = "BARSWindow";
            Text = "Binary Audio Resources Editor";
            darkMenuStrip1.ResumeLayout(false);
            darkMenuStrip1.PerformLayout();
            barsContainer.Panel1.ResumeLayout(false);
            barsContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)barsContainer).EndInit();
            barsContainer.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Components.DarkMenuStrip darkMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.PropertyGrid itemPropertyGrid;
        private System.Windows.Forms.TreeView barsTreeView;
        private System.Windows.Forms.SplitContainer barsContainer;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private Components.CustomWaveViewer waveViewer;
        private AltUI.Controls.DarkButton playButton;
        private System.Windows.Forms.Label timeLabel;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem unloadToolStripMenuItem;
    }
}