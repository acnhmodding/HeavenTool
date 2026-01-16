using AltUI.Controls;

namespace HeavenTool
{
    partial class HeavenMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HeavenMain));
            bcsvEditorButton = new DarkButton();
            topMenu = new DarkMenuStrip();
            toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            compressionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            yaz0ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            yaz0DecompressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            devToolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            bCSVToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportLabelsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportEnumsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportUsedHashesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            rstbEditorButton = new DarkButton();
            sarcEditorButton = new DarkButton();
            itemParamHelperButton = new DarkButton();
            barsEditorButton = new DarkButton();
            yaz0CompressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            topMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // bcsvEditorButton
            // 
            bcsvEditorButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            bcsvEditorButton.BorderColour = System.Drawing.Color.Empty;
            bcsvEditorButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            bcsvEditorButton.Location = new System.Drawing.Point(377, 32);
            bcsvEditorButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            bcsvEditorButton.Name = "bcsvEditorButton";
            bcsvEditorButton.Padding = new System.Windows.Forms.Padding(5);
            bcsvEditorButton.Size = new System.Drawing.Size(253, 38);
            bcsvEditorButton.TabIndex = 0;
            bcsvEditorButton.Text = "Open BCSV Editor";
            bcsvEditorButton.Click += BcsvEditorButton_Click;
            // 
            // topMenu
            // 
            topMenu.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            topMenu.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            topMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolsToolStripMenuItem, devToolsToolStripMenuItem });
            topMenu.Location = new System.Drawing.Point(0, 0);
            topMenu.Name = "topMenu";
            topMenu.Padding = new System.Windows.Forms.Padding(7, 2, 0, 2);
            topMenu.Size = new System.Drawing.Size(645, 24);
            topMenu.TabIndex = 6;
            topMenu.Text = "menuStrip1";
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { compressionToolStripMenuItem });
            toolsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new System.Drawing.Size(47, 20);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // compressionToolStripMenuItem
            // 
            compressionToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            compressionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { yaz0ToolStripMenuItem });
            compressionToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            compressionToolStripMenuItem.Name = "compressionToolStripMenuItem";
            compressionToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            compressionToolStripMenuItem.Text = "Compression";
            // 
            // yaz0ToolStripMenuItem
            // 
            yaz0ToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            yaz0ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { yaz0DecompressToolStripMenuItem, yaz0CompressToolStripMenuItem });
            yaz0ToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            yaz0ToolStripMenuItem.Name = "yaz0ToolStripMenuItem";
            yaz0ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            yaz0ToolStripMenuItem.Text = "Yaz0";
            // 
            // yaz0DecompressToolStripMenuItem
            // 
            yaz0DecompressToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            yaz0DecompressToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            yaz0DecompressToolStripMenuItem.Name = "yaz0DecompressToolStripMenuItem";
            yaz0DecompressToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            yaz0DecompressToolStripMenuItem.Text = "Decompress";
            yaz0DecompressToolStripMenuItem.Click += Yaz0DecompressToolStripMenuItem_Click;
            // 
            // devToolsToolStripMenuItem
            // 
            devToolsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            devToolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { bCSVToolStripMenuItem });
            devToolsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            devToolsToolStripMenuItem.Name = "devToolsToolStripMenuItem";
            devToolsToolStripMenuItem.Size = new System.Drawing.Size(70, 20);
            devToolsToolStripMenuItem.Text = "Dev Tools";
            // 
            // bCSVToolStripMenuItem
            // 
            bCSVToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            bCSVToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { exportLabelsToolStripMenuItem, exportEnumsToolStripMenuItem, exportUsedHashesToolStripMenuItem });
            bCSVToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            bCSVToolStripMenuItem.Name = "bCSVToolStripMenuItem";
            bCSVToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            bCSVToolStripMenuItem.Text = "BCSV";
            // 
            // exportLabelsToolStripMenuItem
            // 
            exportLabelsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            exportLabelsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            exportLabelsToolStripMenuItem.Name = "exportLabelsToolStripMenuItem";
            exportLabelsToolStripMenuItem.Size = new System.Drawing.Size(177, 22);
            exportLabelsToolStripMenuItem.Text = "Export Labels";
            exportLabelsToolStripMenuItem.Click += ExportLabelsToolStripMenuItem_Click;
            // 
            // exportEnumsToolStripMenuItem
            // 
            exportEnumsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            exportEnumsToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            exportEnumsToolStripMenuItem.Name = "exportEnumsToolStripMenuItem";
            exportEnumsToolStripMenuItem.Size = new System.Drawing.Size(177, 22);
            exportEnumsToolStripMenuItem.Text = "Export Enums";
            exportEnumsToolStripMenuItem.Click += ExportEnumsToolStripMenuItem_Click;
            // 
            // exportUsedHashesToolStripMenuItem
            // 
            exportUsedHashesToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            exportUsedHashesToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            exportUsedHashesToolStripMenuItem.Name = "exportUsedHashesToolStripMenuItem";
            exportUsedHashesToolStripMenuItem.Size = new System.Drawing.Size(177, 22);
            exportUsedHashesToolStripMenuItem.Text = "Export Used Hashes";
            exportUsedHashesToolStripMenuItem.Click += ExportUsedHashesToolStripMenuItem_Click;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = Properties.Resources.logo;
            pictureBox1.Location = new System.Drawing.Point(14, 32);
            pictureBox1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(355, 249);
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 7;
            pictureBox1.TabStop = false;
            // 
            // rstbEditorButton
            // 
            rstbEditorButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            rstbEditorButton.BorderColour = System.Drawing.Color.Empty;
            rstbEditorButton.Location = new System.Drawing.Point(377, 76);
            rstbEditorButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            rstbEditorButton.Name = "rstbEditorButton";
            rstbEditorButton.Padding = new System.Windows.Forms.Padding(5);
            rstbEditorButton.Size = new System.Drawing.Size(253, 38);
            rstbEditorButton.TabIndex = 8;
            rstbEditorButton.Text = "Open RSTB Editor";
            rstbEditorButton.Click += RstbEditorButton_Click;
            // 
            // sarcEditorButton
            // 
            sarcEditorButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            sarcEditorButton.BorderColour = System.Drawing.Color.Empty;
            sarcEditorButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            sarcEditorButton.Location = new System.Drawing.Point(377, 120);
            sarcEditorButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            sarcEditorButton.Name = "sarcEditorButton";
            sarcEditorButton.Padding = new System.Windows.Forms.Padding(5);
            sarcEditorButton.Size = new System.Drawing.Size(253, 38);
            sarcEditorButton.TabIndex = 9;
            sarcEditorButton.Text = "Open SARC Editor";
            sarcEditorButton.Click += SarcEditorButton_Click;
            // 
            // itemParamHelperButton
            // 
            itemParamHelperButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            itemParamHelperButton.BorderColour = System.Drawing.Color.Empty;
            itemParamHelperButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            itemParamHelperButton.Location = new System.Drawing.Point(377, 208);
            itemParamHelperButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            itemParamHelperButton.Name = "itemParamHelperButton";
            itemParamHelperButton.Padding = new System.Windows.Forms.Padding(5);
            itemParamHelperButton.Size = new System.Drawing.Size(253, 38);
            itemParamHelperButton.TabIndex = 10;
            itemParamHelperButton.Text = "Open ItemParam Helper";
            itemParamHelperButton.Click += ItemParamHelperButton_Click;
            // 
            // barsEditorButton
            // 
            barsEditorButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            barsEditorButton.BorderColour = System.Drawing.Color.Empty;
            barsEditorButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            barsEditorButton.Location = new System.Drawing.Point(377, 164);
            barsEditorButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            barsEditorButton.Name = "barsEditorButton";
            barsEditorButton.Padding = new System.Windows.Forms.Padding(5);
            barsEditorButton.Size = new System.Drawing.Size(253, 38);
            barsEditorButton.TabIndex = 11;
            barsEditorButton.Text = "Open BARS Editor";
            barsEditorButton.Click += BarsEditorButton_Click;
            // 
            // yaz0CompressToolStripMenuItem
            // 
            yaz0CompressToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            yaz0CompressToolStripMenuItem.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            yaz0CompressToolStripMenuItem.Name = "yaz0CompressToolStripMenuItem";
            yaz0CompressToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            yaz0CompressToolStripMenuItem.Text = "Compress";
            yaz0CompressToolStripMenuItem.Click += yaz0CompressToolStripMenuItem_Click;
            // 
            // HeavenMain
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(645, 293);
            Controls.Add(bcsvEditorButton);
            Controls.Add(rstbEditorButton);
            Controls.Add(sarcEditorButton);
            Controls.Add(barsEditorButton);
            Controls.Add(itemParamHelperButton);
            Controls.Add(pictureBox1);
            Controls.Add(topMenu);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = topMenu;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            Name = "HeavenMain";
            Text = "Heaven Tool v1.0";
            topMenu.ResumeLayout(false);
            topMenu.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private DarkButton bcsvEditorButton;
        private DarkMenuStrip topMenu;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.PictureBox pictureBox1;
        private DarkButton rstbEditorButton;
        private DarkButton sarcEditorButton;
        private DarkButton itemParamHelperButton;
        private System.Windows.Forms.ToolStripMenuItem compressionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem yaz0ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem yaz0DecompressToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem devToolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bCSVToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportLabelsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportEnumsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportUsedHashesToolStripMenuItem;
        private DarkButton barsEditorButton;
        private System.Windows.Forms.ToolStripMenuItem yaz0CompressToolStripMenuItem;
    }
}