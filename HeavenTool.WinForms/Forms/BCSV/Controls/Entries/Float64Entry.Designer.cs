namespace HeavenTool.Forms.BCSV.Controls
{
    partial class Float64Entry
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
            input = new HeavenTool.Forms.Components.Float64TextBox();
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
            // input
            // 
            input.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            input.BackColor = System.Drawing.Color.FromArgb(26, 26, 28);
            input.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            input.ForeColor = System.Drawing.Color.FromArgb(213, 213, 213);
            input.Location = new System.Drawing.Point(149, 0);
            input.Name = "input";
            input.Size = new System.Drawing.Size(148, 23);
            input.TabIndex = 3;
            input.TextChanged += Input_TextChanged;
            // 
            // Entry
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            Controls.Add(input);
            Controls.Add(propertyNameLabel);
            Name = "Entry";
            Size = new System.Drawing.Size(297, 23);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.Label propertyNameLabel;
        private Components.ValidatingTextBox input;
    }
}
