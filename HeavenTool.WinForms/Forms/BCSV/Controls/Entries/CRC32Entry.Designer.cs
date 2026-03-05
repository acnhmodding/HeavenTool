namespace HeavenTool.Forms.BCSV.Controls
{
    partial class CRC32Entry
    {
        /// <summary> 
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region
        private void InitializeComponent()
        {
            propertyNameLabel = new System.Windows.Forms.Label();
            crcComboBox = new System.Windows.Forms.ComboBox();
            SuspendLayout();
            // 
            // propertyNameLabel
            // 
            propertyNameLabel.AutoEllipsis = true;
            propertyNameLabel.ForeColor = System.Drawing.Color.FromArgb(210, 210, 210);
            propertyNameLabel.Location = new System.Drawing.Point(3, 0);
            propertyNameLabel.Name = "propertyNameLabel";
            propertyNameLabel.Size = new System.Drawing.Size(140, 23);
            propertyNameLabel.TabIndex = 2;
            propertyNameLabel.Text = "This is Property Name";
            propertyNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            propertyNameLabel.UseMnemonic = false;
            // 
            // crcComboBox
            // 
            crcComboBox.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            crcComboBox.FormattingEnabled = true;
            crcComboBox.Location = new System.Drawing.Point(149, -2);
            crcComboBox.Name = "crcComboBox";
            crcComboBox.Size = new System.Drawing.Size(148, 23);
            crcComboBox.TabIndex = 3;
            // 
            // CRC32Entry
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            Controls.Add(crcComboBox);
            Controls.Add(propertyNameLabel);
            Name = "CRC32Entry";
            Size = new System.Drawing.Size(297, 24);
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Label propertyNameLabel;
        private System.Windows.Forms.ComboBox crcComboBox;
    }
}
