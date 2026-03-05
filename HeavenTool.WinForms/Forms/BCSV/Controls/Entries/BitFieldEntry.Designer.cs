namespace HeavenTool.Forms.BCSV.Controls
{
    partial class BitFieldEntry
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
            bitfieldEntries = new CheckedComboBox();
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
            // bitfieldEntries
            // 
            bitfieldEntries.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            bitfieldEntries.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            bitfieldEntries.DropDownHeight = 1;
            bitfieldEntries.FormattingEnabled = true;
            bitfieldEntries.IntegralHeight = false;
            bitfieldEntries.Location = new System.Drawing.Point(149, 0);
            bitfieldEntries.Name = "bitfieldEntries";
            bitfieldEntries.Size = new System.Drawing.Size(148, 24);
            bitfieldEntries.TabIndex = 6;
            bitfieldEntries.ValueSeparator = ", ";
            // 
            // BitFieldEntry
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            Controls.Add(bitfieldEntries);
            Controls.Add(propertyNameLabel);
            Name = "BitFieldEntry";
            Size = new System.Drawing.Size(297, 24);
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Label propertyNameLabel;
        private CheckedComboBox bitfieldEntries;
    }
}
