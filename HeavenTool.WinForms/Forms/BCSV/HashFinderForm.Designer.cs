namespace HeavenTool.Forms.BCSV
{
    partial class HashFinderForm
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
            hashInput = new HeavenTool.Forms.Components.StringTextBox();
            hashResultLbl = new AltUI.Controls.DarkLabel();
            searchButton = new AltUI.Controls.DarkButton();
            darkLabel1 = new AltUI.Controls.DarkLabel();
            pasteButton = new AltUI.Controls.DarkButton();
            copyButton = new AltUI.Controls.DarkButton();
            SuspendLayout();
            // 
            // hashInput
            // 
            hashInput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            hashInput.BackColor = System.Drawing.Color.FromArgb(26, 26, 28);
            hashInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            hashInput.ForeColor = System.Drawing.Color.FromArgb(230, 230, 230);
            hashInput.Location = new System.Drawing.Point(53, 12);
            hashInput.Name = "hashInput";
            hashInput.Size = new System.Drawing.Size(199, 23);
            hashInput.TabIndex = 0;
            // 
            // hashResultLbl
            // 
            hashResultLbl.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            hashResultLbl.AutoSize = true;
            hashResultLbl.Font = new System.Drawing.Font("Segoe UI", 10F);
            hashResultLbl.Location = new System.Drawing.Point(12, 89);
            hashResultLbl.Name = "hashResultLbl";
            hashResultLbl.Size = new System.Drawing.Size(64, 19);
            hashResultLbl.TabIndex = 1;
            hashResultLbl.Text = "Waiting...";
            hashResultLbl.Click += HashResultLbl_Click;
            // 
            // searchButton
            // 
            searchButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            searchButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            searchButton.BorderColour = System.Drawing.Color.Empty;
            searchButton.Location = new System.Drawing.Point(12, 41);
            searchButton.Name = "searchButton";
            searchButton.Padding = new System.Windows.Forms.Padding(5);
            searchButton.Size = new System.Drawing.Size(307, 39);
            searchButton.TabIndex = 2;
            searchButton.Text = "Search";
            searchButton.Click += SearchButton_Click;
            // 
            // darkLabel1
            // 
            darkLabel1.AutoSize = true;
            darkLabel1.Location = new System.Drawing.Point(12, 16);
            darkLabel1.Name = "darkLabel1";
            darkLabel1.Size = new System.Drawing.Size(35, 15);
            darkLabel1.TabIndex = 3;
            darkLabel1.Text = "Input";
            // 
            // pasteButton
            // 
            pasteButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            pasteButton.BorderColour = System.Drawing.Color.Empty;
            pasteButton.Location = new System.Drawing.Point(258, 11);
            pasteButton.Name = "pasteButton";
            pasteButton.Padding = new System.Windows.Forms.Padding(5);
            pasteButton.Size = new System.Drawing.Size(61, 25);
            pasteButton.TabIndex = 4;
            pasteButton.Text = "Paste";
            pasteButton.Click += PasteButton_Click;
            // 
            // copyButton
            // 
            copyButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            copyButton.BorderColour = System.Drawing.Color.Empty;
            copyButton.Location = new System.Drawing.Point(244, 86);
            copyButton.Name = "copyButton";
            copyButton.Padding = new System.Windows.Forms.Padding(5);
            copyButton.Size = new System.Drawing.Size(75, 25);
            copyButton.TabIndex = 5;
            copyButton.Text = "Copy";
            copyButton.Click += CopyButton_Click;
            // 
            // HashFinderForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(331, 123);
            Controls.Add(copyButton);
            Controls.Add(pasteButton);
            Controls.Add(darkLabel1);
            Controls.Add(searchButton);
            Controls.Add(hashResultLbl);
            Controls.Add(hashInput);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Name = "HashFinderForm";
            Text = "Hash Finder";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Components.StringTextBox hashInput;
        private AltUI.Controls.DarkLabel hashResultLbl;
        private AltUI.Controls.DarkButton searchButton;
        private AltUI.Controls.DarkLabel darkLabel1;
        private AltUI.Controls.DarkButton pasteButton;
        private AltUI.Controls.DarkButton copyButton;
    }
}