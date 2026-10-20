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
            if (disposing)
            {
                DisposeResources();
                components?.Dispose();
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
            components = new System.ComponentModel.Container();
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(BARSWindow));
            darkMenuStrip1 = new HeavenTool.Forms.Components.DarkMenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            unloadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            baseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            singersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            readPrefetchedFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            whistlingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            loopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            itemPropertyGrid = new System.Windows.Forms.PropertyGrid();
            barsTreeView = new System.Windows.Forms.TreeView();
            dockPanel = new WeifenLuo.WinFormsUI.Docking.DockPanel();
            previewPanel = new System.Windows.Forms.Panel();
            waveViewer = new HeavenTool.Forms.Components.CustomWaveViewer();
            playbackBar = new System.Windows.Forms.TableLayoutPanel();
            playButton = new System.Windows.Forms.Button();
            volumeSlider = new NAudio.Gui.VolumeSlider();
            timeLabel = new System.Windows.Forms.Label();
            resourcesPanel = new System.Windows.Forms.Panel();
            searchBox = new System.Windows.Forms.TextBox();
            playbackTips = new System.Windows.Forms.ToolTip(components);
            assetContextMenu = new System.Windows.Forms.ContextMenuStrip(components);
            metadataContextMenu = new System.Windows.Forms.ContextMenuStrip(components);
            assetMenuSeparator = new System.Windows.Forms.ToolStripSeparator();
            exportBwavMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportWaveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            importAudioMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            editLoopMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportMetadataMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            replaceMetadataMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            assetContextMenu.SuspendLayout();
            metadataContextMenu.SuspendLayout();
            darkMenuStrip1.SuspendLayout();
            dockPanel.SuspendLayout();
            previewPanel.SuspendLayout();
            playbackBar.SuspendLayout();
            resourcesPanel.SuspendLayout();
            SuspendLayout();
            //
            // darkMenuStrip1
            //
            darkMenuStrip1.BackColor = System.Drawing.Color.Transparent;
            darkMenuStrip1.ForeColor = System.Drawing.Color.White;
            darkMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, optionsToolStripMenuItem });
            darkMenuStrip1.Location = new System.Drawing.Point(0, 0);
            darkMenuStrip1.Name = "darkMenuStrip1";
            darkMenuStrip1.Size = new System.Drawing.Size(1100, 24);
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
            openToolStripMenuItem.Size = new System.Drawing.Size(171, 30);
            openToolStripMenuItem.Text = "Open...";
            openToolStripMenuItem.Click += OpenToolStripMenuItem_Click;
            //
            // saveToolStripMenuItem
            //
            saveToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            saveToolStripMenuItem.Enabled = false;
            saveToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            saveToolStripMenuItem.Image = Properties.Resources.save_as;
            saveToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
            saveToolStripMenuItem.Size = new System.Drawing.Size(171, 30);
            saveToolStripMenuItem.Text = "Save As...";
            //
            // toolStripSeparator1
            //
            toolStripSeparator1.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            toolStripSeparator1.ForeColor = System.Drawing.Color.White;
            toolStripSeparator1.Margin = new System.Windows.Forms.Padding(0, 0, 0, 1);
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(168, 6);
            //
            // unloadToolStripMenuItem
            //
            unloadToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            unloadToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            unloadToolStripMenuItem.Image = Properties.Resources.cancel;
            unloadToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            unloadToolStripMenuItem.Name = "unloadToolStripMenuItem";
            unloadToolStripMenuItem.Size = new System.Drawing.Size(171, 30);
            unloadToolStripMenuItem.Text = "Unload";
            unloadToolStripMenuItem.Click += UnloadToolStripMenuItem_Click;
            //
            // optionsToolStripMenuItem
            //
            optionsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { baseToolStripMenuItem, singersToolStripMenuItem, readPrefetchedFilesToolStripMenuItem, whistlingToolStripMenuItem, loopToolStripMenuItem });
            optionsToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            optionsToolStripMenuItem.Text = "Options";
            //
            // baseToolStripMenuItem
            //
            baseToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            baseToolStripMenuItem.Checked = true;
            baseToolStripMenuItem.CheckOnClick = true;
            baseToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            baseToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            baseToolStripMenuItem.Name = "baseToolStripMenuItem";
            baseToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            baseToolStripMenuItem.Text = "Base";
            //
            // singersToolStripMenuItem
            //
            singersToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            singersToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            singersToolStripMenuItem.Name = "singersToolStripMenuItem";
            singersToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            singersToolStripMenuItem.Text = "Singers";
            //
            // readPrefetchedFilesToolStripMenuItem
            //
            readPrefetchedFilesToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            readPrefetchedFilesToolStripMenuItem.Checked = true;
            readPrefetchedFilesToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            readPrefetchedFilesToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            readPrefetchedFilesToolStripMenuItem.Name = "readPrefetchedFilesToolStripMenuItem";
            readPrefetchedFilesToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            readPrefetchedFilesToolStripMenuItem.Text = "Read Prefetched Files";
            readPrefetchedFilesToolStripMenuItem.Click += ReadPrefetchedFilesToolStripMenuItem_Click;
            //
            // whistlingToolStripMenuItem
            //
            whistlingToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            whistlingToolStripMenuItem.Checked = true;
            whistlingToolStripMenuItem.CheckOnClick = true;
            whistlingToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            whistlingToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            whistlingToolStripMenuItem.Name = "whistlingToolStripMenuItem";
            whistlingToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            whistlingToolStripMenuItem.Text = "Whistling";
            //
            // loopToolStripMenuItem
            //
            loopToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            loopToolStripMenuItem.CheckOnClick = true;
            loopToolStripMenuItem.Enabled = false;
            loopToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            loopToolStripMenuItem.Name = "loopToolStripMenuItem";
            loopToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            loopToolStripMenuItem.Text = "Loop";
            //
            // itemPropertyGrid
            //
            itemPropertyGrid.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            itemPropertyGrid.CanShowVisualStyleGlyphs = false;
            itemPropertyGrid.CategoryForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            itemPropertyGrid.CategorySplitterColor = System.Drawing.Color.FromArgb(31, 31, 32);
            itemPropertyGrid.CausesValidation = false;
            itemPropertyGrid.CommandsForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            itemPropertyGrid.CommandsVisibleIfAvailable = false;
            itemPropertyGrid.DisabledItemForeColor = System.Drawing.Color.FromArgb(127, 255, 255, 255);
            itemPropertyGrid.Dock = System.Windows.Forms.DockStyle.Right;
            itemPropertyGrid.HelpBackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            itemPropertyGrid.HelpForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            itemPropertyGrid.HelpVisible = false;
            itemPropertyGrid.LineColor = System.Drawing.Color.FromArgb(26, 26, 26);
            itemPropertyGrid.Location = new System.Drawing.Point(800, 0);
            itemPropertyGrid.Margin = new System.Windows.Forms.Padding(3, 3, 3, 1);
            itemPropertyGrid.Name = "itemPropertyGrid";
            itemPropertyGrid.SelectedItemWithFocusForeColor = System.Drawing.Color.Black;
            itemPropertyGrid.Size = new System.Drawing.Size(300, 626);
            itemPropertyGrid.TabIndex = 2;
            itemPropertyGrid.ToolbarVisible = false;
            itemPropertyGrid.ViewBackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            itemPropertyGrid.ViewForeColor = System.Drawing.Color.White;
            //
            // barsTreeView
            //
            barsTreeView.BackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            barsTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            barsTreeView.ForeColor = System.Drawing.Color.White;
            barsTreeView.HideSelection = false;
            barsTreeView.Location = new System.Drawing.Point(5, 28);
            barsTreeView.Name = "barsTreeView";
            barsTreeView.Size = new System.Drawing.Size(250, 593);
            barsTreeView.TabIndex = 3;
            barsTreeView.BeforeExpand += BarsTreeView_BeforeExpand;
            //
            // dockPanel
            //
            dockPanel.Controls.Add(previewPanel);
            dockPanel.Controls.Add(itemPropertyGrid);
            dockPanel.Controls.Add(resourcesPanel);
            dockPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            dockPanel.DockLeftPortion = 0.24D;
            dockPanel.DockRightPortion = 0.27D;
            dockPanel.Location = new System.Drawing.Point(0, 24);
            dockPanel.Name = "dockPanel";
            dockPanel.Size = new System.Drawing.Size(1100, 626);
            dockPanel.TabIndex = 4;
            //
            // previewPanel
            //
            previewPanel.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            previewPanel.Controls.Add(waveViewer);
            previewPanel.Controls.Add(playbackBar);
            previewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            previewPanel.Location = new System.Drawing.Point(260, 0);
            previewPanel.Name = "previewPanel";
            previewPanel.Padding = new System.Windows.Forms.Padding(5);
            previewPanel.Size = new System.Drawing.Size(540, 626);
            previewPanel.TabIndex = 1;
            //
            // waveViewer
            //
            waveViewer.BackColor = System.Drawing.Color.FromArgb(26, 26, 26);
            waveViewer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            waveViewer.Dock = System.Windows.Forms.DockStyle.Fill;
            waveViewer.Location = new System.Drawing.Point(5, 37);
            waveViewer.Name = "waveViewer";
            waveViewer.Size = new System.Drawing.Size(530, 584);
            waveViewer.TabIndex = 3;
            playbackTips.SetToolTip(waveViewer, "Click to seek; drag to pan; scroll to zoom.\nSpace: play / pause. Left / Right: 5 seconds. Home: beginning.");
            //
            // playbackBar
            //
            playbackBar.ColumnCount = 3;
            playbackBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            playbackBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            playbackBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            playbackBar.Controls.Add(playButton, 0, 0);
            playbackBar.Controls.Add(volumeSlider, 1, 0);
            playbackBar.Controls.Add(timeLabel, 2, 0);
            playbackBar.Dock = System.Windows.Forms.DockStyle.Top;
            playbackBar.Location = new System.Drawing.Point(5, 5);
            playbackBar.Name = "playbackBar";
            playbackBar.RowCount = 1;
            playbackBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            playbackBar.Size = new System.Drawing.Size(530, 32);
            playbackBar.TabIndex = 0;
            //
            // playButton
            //
            playButton.BackgroundImage = Properties.Resources.play;
            playButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            playButton.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            playButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            playButton.FlatAppearance.BorderSize = 0;
            playButton.AccessibleName = "Play / pause";
            playButton.Enabled = false;
            playButton.Location = new System.Drawing.Point(3, 4);
            playButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            playButton.Name = "playButton";
            playButton.Size = new System.Drawing.Size(23, 23);
            playButton.TabIndex = 6;
            playbackTips.SetToolTip(playButton, "Play / pause (Space)");
            playButton.Click += PlayStopButton_Click;
            //
            // volumeSlider
            //
            volumeSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            volumeSlider.Location = new System.Drawing.Point(34, 7);
            volumeSlider.Margin = new System.Windows.Forms.Padding(4, 7, 4, 7);
            volumeSlider.Name = "volumeSlider";
            volumeSlider.Size = new System.Drawing.Size(431, 18);
            volumeSlider.TabIndex = 8;
            //
            // timeLabel
            //
            timeLabel.Anchor = System.Windows.Forms.AnchorStyles.Right;
            timeLabel.AutoSize = true;
            timeLabel.ForeColor = System.Drawing.Color.White;
            timeLabel.Location = new System.Drawing.Point(472, 8);
            timeLabel.Name = "timeLabel";
            timeLabel.Size = new System.Drawing.Size(55, 15);
            timeLabel.TabIndex = 7;
            timeLabel.Text = "0:000:000";
            timeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // resourcesPanel
            //
            resourcesPanel.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            resourcesPanel.Controls.Add(barsTreeView);
            resourcesPanel.Controls.Add(searchBox);
            resourcesPanel.Dock = System.Windows.Forms.DockStyle.Left;
            resourcesPanel.Location = new System.Drawing.Point(0, 0);
            resourcesPanel.Name = "resourcesPanel";
            resourcesPanel.Padding = new System.Windows.Forms.Padding(5);
            resourcesPanel.Size = new System.Drawing.Size(260, 626);
            resourcesPanel.TabIndex = 0;
            //
            // searchBox
            //
            searchBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            searchBox.Dock = System.Windows.Forms.DockStyle.Top;
            searchBox.Location = new System.Drawing.Point(5, 5);
            searchBox.Name = "searchBox";
            searchBox.PlaceholderText = "Search...";
            searchBox.Size = new System.Drawing.Size(250, 23);
            searchBox.TabIndex = 3;
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBox.KeyDown += SearchBox_KeyDown;
            //
            // assetContextMenu
            //
            assetContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { exportBwavMenuItem, exportWaveMenuItem, assetMenuSeparator, importAudioMenuItem, editLoopMenuItem });
            assetContextMenu.Name = "assetContextMenu";
            assetContextMenu.Opening += EntryMenu_Opening;
            //
            // metadataContextMenu
            //
            metadataContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { exportMetadataMenuItem, replaceMetadataMenuItem });
            metadataContextMenu.Name = "metadataContextMenu";
            metadataContextMenu.Opening += EntryMenu_Opening;
            //
            // exportBwavMenuItem
            //
            exportBwavMenuItem.Name = "exportBwavMenuItem";
            exportBwavMenuItem.Text = "Export BWAV...";
            exportBwavMenuItem.Click += ExportBwavMenuItem_Click;
            //
            // exportWaveMenuItem
            //
            exportWaveMenuItem.Name = "exportWaveMenuItem";
            exportWaveMenuItem.Text = "Export as WAV...";
            exportWaveMenuItem.Click += ExportWaveMenuItem_Click;
            //
            // importAudioMenuItem
            //
            importAudioMenuItem.Name = "importAudioMenuItem";
            importAudioMenuItem.Text = "Replace / Import Audio...";
            importAudioMenuItem.Click += ImportAudioMenuItem_Click;
            //
            // editLoopMenuItem
            //
            editLoopMenuItem.Name = "editLoopMenuItem";
            editLoopMenuItem.Text = "Edit Loop...";
            editLoopMenuItem.Click += EditLoopMenuItem_Click;
            //
            // exportMetadataMenuItem
            //
            exportMetadataMenuItem.Name = "exportMetadataMenuItem";
            exportMetadataMenuItem.Text = "Export Metadata...";
            exportMetadataMenuItem.Click += ExportMetadataMenuItem_Click;
            //
            // replaceMetadataMenuItem
            //
            replaceMetadataMenuItem.Name = "replaceMetadataMenuItem";
            replaceMetadataMenuItem.Text = "Import Metadata...";
            replaceMetadataMenuItem.Click += ImportMetadataMenuItem_Click;
            //
            // BARSWindow
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(1100, 650);
            Controls.Add(dockPanel);
            Controls.Add(darkMenuStrip1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = darkMenuStrip1;
            MinimumSize = new System.Drawing.Size(720, 350);
            Name = "BARSWindow";
            Text = "Binary Audio Resources Editor";
            assetContextMenu.ResumeLayout(false);
            metadataContextMenu.ResumeLayout(false);
            darkMenuStrip1.ResumeLayout(false);
            darkMenuStrip1.PerformLayout();
            dockPanel.ResumeLayout(false);
            previewPanel.ResumeLayout(false);
            playbackBar.ResumeLayout(false);
            playbackBar.PerformLayout();
            resourcesPanel.ResumeLayout(false);
            resourcesPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.ContextMenuStrip assetContextMenu;
        private System.Windows.Forms.ContextMenuStrip metadataContextMenu;
        private System.Windows.Forms.ToolStripSeparator assetMenuSeparator;
        private System.Windows.Forms.ToolStripMenuItem exportBwavMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportWaveMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importAudioMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editLoopMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportMetadataMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replaceMetadataMenuItem;
        private Components.DarkMenuStrip darkMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.PropertyGrid itemPropertyGrid;
        private System.Windows.Forms.TreeView barsTreeView;
        private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel;
        private System.Windows.Forms.Panel resourcesPanel;
        private System.Windows.Forms.Panel previewPanel;
        private System.Windows.Forms.TableLayoutPanel playbackBar;
        private System.Windows.Forms.ToolTip playbackTips;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private Components.CustomWaveViewer waveViewer;
        private System.Windows.Forms.Button playButton;
        private System.Windows.Forms.Label timeLabel;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem unloadToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singersToolStripMenuItem;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.ToolStripMenuItem readPrefetchedFilesToolStripMenuItem;
        private NAudio.Gui.VolumeSlider volumeSlider;
        private System.Windows.Forms.ToolStripMenuItem whistlingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem baseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loopToolStripMenuItem;
    }
}
