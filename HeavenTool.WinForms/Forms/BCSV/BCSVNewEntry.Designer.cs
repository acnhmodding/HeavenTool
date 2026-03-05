namespace HeavenTool.Forms.BCSV
{
    partial class BCSVEntryEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BCSVEntryEditor));
            bottomPanel = new System.Windows.Forms.Panel();
            addEntryButton = new AltUI.Controls.DarkButton();
            contentPanel = new System.Windows.Forms.FlowLayoutPanel();
            bottomPanel.SuspendLayout();
            SuspendLayout();
            // 
            // bottomPanel
            // 
            bottomPanel.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
            bottomPanel.Controls.Add(addEntryButton);
            bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            bottomPanel.Location = new System.Drawing.Point(0, 347);
            bottomPanel.Name = "bottomPanel";
            bottomPanel.Size = new System.Drawing.Size(340, 36);
            bottomPanel.TabIndex = 0;
            // 
            // addEntryButton
            // 
            addEntryButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            addEntryButton.BorderColour = System.Drawing.Color.Empty;
            addEntryButton.Location = new System.Drawing.Point(193, 5);
            addEntryButton.Name = "addEntryButton";
            addEntryButton.Padding = new System.Windows.Forms.Padding(5);
            addEntryButton.Size = new System.Drawing.Size(144, 28);
            addEntryButton.TabIndex = 0;
            addEntryButton.Text = "Add Entry";
            addEntryButton.Click += AddEntryButton_Click;
            // 
            // contentPanel
            // 
            contentPanel.AutoScroll = true;
            contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            contentPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            contentPanel.Location = new System.Drawing.Point(0, 0);
            contentPanel.Margin = new System.Windows.Forms.Padding(0);
            contentPanel.Name = "contentPanel";
            contentPanel.Padding = new System.Windows.Forms.Padding(3);
            contentPanel.Size = new System.Drawing.Size(340, 347);
            contentPanel.TabIndex = 1;
            contentPanel.WrapContents = false;
            contentPanel.SizeChanged += ContentPanel_SizeChanged;
            // 
            // BCSVEntryEditor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(340, 383);
            Controls.Add(contentPanel);
            Controls.Add(bottomPanel);
            ForeColor = System.Drawing.Color.White;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "BCSVEntryEditor";
            ShowIcon = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "New Entry";
            bottomPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel bottomPanel;
        private AltUI.Controls.DarkButton addEntryButton;
        public System.Windows.Forms.FlowLayoutPanel contentPanel;
    }
}