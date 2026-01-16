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
            checkedComboBox1 = new CheckedComboBox();
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
            // checkedComboBox1
            // 
            checkedComboBox1.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            checkedComboBox1.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            checkedComboBox1.DropDownHeight = 1;
            checkedComboBox1.FormattingEnabled = true;
            checkedComboBox1.IntegralHeight = false;
            checkedComboBox1.Location = new System.Drawing.Point(149, 0);
            checkedComboBox1.Name = "checkedComboBox1";
            checkedComboBox1.Size = new System.Drawing.Size(148, 24);
            checkedComboBox1.TabIndex = 6;
            checkedComboBox1.ValueSeparator = ", ";
            // 
            // BitFieldEntry
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            Controls.Add(checkedComboBox1);
            Controls.Add(propertyNameLabel);
            Name = "BitFieldEntry";
            Size = new System.Drawing.Size(297, 24);
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Label propertyNameLabel;
        private CheckedComboBox checkedComboBox1;
    }
}
