namespace HeavenTool.Forms.Pack
{
    partial class ItemIDHelper
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ItemIDHelper));
            encodedIdInput = new HeavenTool.Forms.Components.UInt64TextBox();
            panel1 = new System.Windows.Forms.Panel();
            copyIdButton = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            panel2 = new System.Windows.Forms.Panel();
            copyItemIdButton = new System.Windows.Forms.Button();
            itemIdBox = new HeavenTool.Forms.Components.UInt64TextBox();
            panel3 = new System.Windows.Forms.Panel();
            itemRotationBox = new HeavenTool.Forms.Components.UInt16TextBox();
            itemRotationLabel = new System.Windows.Forms.Label();
            panel4 = new System.Windows.Forms.Panel();
            itemStateInput = new HeavenTool.Forms.Components.UInt16TextBox();
            itemStateLabel = new System.Windows.Forms.Label();
            panel5 = new System.Windows.Forms.Panel();
            reFabricInput = new HeavenTool.Forms.Components.ByteTextBox();
            reFabricLabel = new System.Windows.Forms.Label();
            panel6 = new System.Windows.Forms.Panel();
            reFabricPatternInput = new HeavenTool.Forms.Components.ByteTextBox();
            reFabricPatternLabel = new System.Windows.Forms.Label();
            reBodyPatternLabel = new System.Windows.Forms.Label();
            panel7 = new System.Windows.Forms.Panel();
            reBodyPatternInput = new HeavenTool.Forms.Components.ByteTextBox();
            panel8 = new System.Windows.Forms.Panel();
            reBodyInput = new HeavenTool.Forms.Components.ByteTextBox();
            reBodyLabel = new System.Windows.Forms.Label();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            panel3.SuspendLayout();
            panel4.SuspendLayout();
            panel5.SuspendLayout();
            panel6.SuspendLayout();
            panel7.SuspendLayout();
            panel8.SuspendLayout();
            SuspendLayout();
            // 
            // encodedIdInput
            // 
            encodedIdInput.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            encodedIdInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            encodedIdInput.Font = new System.Drawing.Font("Segoe UI", 16F);
            encodedIdInput.ForeColor = System.Drawing.Color.Gainsboro;
            encodedIdInput.Location = new System.Drawing.Point(5, 5);
            encodedIdInput.Name = "encodedIdInput";
            encodedIdInput.PlaceholderText = "Put mItemNameParam here";
            encodedIdInput.Size = new System.Drawing.Size(388, 29);
            encodedIdInput.TabIndex = 2;
            encodedIdInput.TextChanged += encodedIdInput_TextChanged;
            // 
            // panel1
            // 
            panel1.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel1.Controls.Add(copyIdButton);
            panel1.Controls.Add(encodedIdInput);
            panel1.Location = new System.Drawing.Point(12, 13);
            panel1.Name = "panel1";
            panel1.Padding = new System.Windows.Forms.Padding(5);
            panel1.Size = new System.Drawing.Size(434, 39);
            panel1.TabIndex = 3;
            // 
            // copyIdButton
            // 
            copyIdButton.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            copyIdButton.FlatAppearance.BorderSize = 0;
            copyIdButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            copyIdButton.Image = Properties.Resources.copy;
            copyIdButton.Location = new System.Drawing.Point(399, 5);
            copyIdButton.Name = "copyIdButton";
            copyIdButton.Size = new System.Drawing.Size(30, 29);
            copyIdButton.TabIndex = 5;
            copyIdButton.UseVisualStyleBackColor = false;
            copyIdButton.Click += copyIdButton_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            label1.Location = new System.Drawing.Point(12, 74);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(45, 15);
            label1.TabIndex = 4;
            label1.Text = "Item ID";
            // 
            // panel2
            // 
            panel2.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel2.Controls.Add(copyItemIdButton);
            panel2.Controls.Add(itemIdBox);
            panel2.Location = new System.Drawing.Point(12, 92);
            panel2.Name = "panel2";
            panel2.Padding = new System.Windows.Forms.Padding(5);
            panel2.Size = new System.Drawing.Size(246, 31);
            panel2.TabIndex = 5;
            // 
            // copyItemIdButton
            // 
            copyItemIdButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            copyItemIdButton.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            copyItemIdButton.FlatAppearance.BorderSize = 0;
            copyItemIdButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            copyItemIdButton.Image = Properties.Resources.copy;
            copyItemIdButton.Location = new System.Drawing.Point(213, 5);
            copyItemIdButton.Name = "copyItemIdButton";
            copyItemIdButton.Size = new System.Drawing.Size(30, 21);
            copyItemIdButton.TabIndex = 5;
            copyItemIdButton.UseVisualStyleBackColor = false;
            copyItemIdButton.Click += copyItemIdButton_Click;
            // 
            // itemIdBox
            // 
            itemIdBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            itemIdBox.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            itemIdBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            itemIdBox.Font = new System.Drawing.Font("Segoe UI", 11F);
            itemIdBox.ForeColor = System.Drawing.Color.Gainsboro;
            itemIdBox.Location = new System.Drawing.Point(8, 5);
            itemIdBox.Name = "itemIdBox";
            itemIdBox.PlaceholderText = "0";
            itemIdBox.Size = new System.Drawing.Size(199, 20);
            itemIdBox.TabIndex = 2;
            itemIdBox.TextChanged += itemIdBox_TextChanged;
            // 
            // panel3
            // 
            panel3.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel3.Controls.Add(itemRotationBox);
            panel3.Location = new System.Drawing.Point(264, 92);
            panel3.Name = "panel3";
            panel3.Padding = new System.Windows.Forms.Padding(5);
            panel3.Size = new System.Drawing.Size(182, 31);
            panel3.TabIndex = 7;
            // 
            // itemRotationBox
            // 
            itemRotationBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            itemRotationBox.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            itemRotationBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            itemRotationBox.Font = new System.Drawing.Font("Segoe UI", 11F);
            itemRotationBox.ForeColor = System.Drawing.Color.Gainsboro;
            itemRotationBox.Location = new System.Drawing.Point(8, 5);
            itemRotationBox.Name = "itemRotationBox";
            itemRotationBox.PlaceholderText = "0";
            itemRotationBox.Size = new System.Drawing.Size(167, 20);
            itemRotationBox.TabIndex = 2;
            itemRotationBox.TextChanged += itemRotationBox_TextChanged;
            // 
            // itemRotationLabel
            // 
            itemRotationLabel.AutoSize = true;
            itemRotationLabel.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            itemRotationLabel.Location = new System.Drawing.Point(264, 74);
            itemRotationLabel.Name = "itemRotationLabel";
            itemRotationLabel.Size = new System.Drawing.Size(52, 15);
            itemRotationLabel.TabIndex = 6;
            itemRotationLabel.Text = "Rotation";
            // 
            // panel4
            // 
            panel4.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel4.Controls.Add(itemStateInput);
            panel4.Location = new System.Drawing.Point(264, 213);
            panel4.Name = "panel4";
            panel4.Padding = new System.Windows.Forms.Padding(5);
            panel4.Size = new System.Drawing.Size(182, 31);
            panel4.TabIndex = 9;
            // 
            // itemStateInput
            // 
            itemStateInput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            itemStateInput.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            itemStateInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            itemStateInput.Font = new System.Drawing.Font("Segoe UI", 11F);
            itemStateInput.ForeColor = System.Drawing.Color.Gainsboro;
            itemStateInput.Location = new System.Drawing.Point(6, 5);
            itemStateInput.Name = "itemStateInput";
            itemStateInput.PlaceholderText = "0";
            itemStateInput.Size = new System.Drawing.Size(168, 20);
            itemStateInput.TabIndex = 2;
            itemStateInput.TextChanged += itemStateInput_TextChanged;
            // 
            // itemStateLabel
            // 
            itemStateLabel.AutoSize = true;
            itemStateLabel.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            itemStateLabel.Location = new System.Drawing.Point(264, 195);
            itemStateLabel.Name = "itemStateLabel";
            itemStateLabel.Size = new System.Drawing.Size(60, 15);
            itemStateLabel.TabIndex = 8;
            itemStateLabel.Text = "Item State";
            // 
            // panel5
            // 
            panel5.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel5.Controls.Add(reFabricInput);
            panel5.Location = new System.Drawing.Point(12, 153);
            panel5.Name = "panel5";
            panel5.Padding = new System.Windows.Forms.Padding(5);
            panel5.Size = new System.Drawing.Size(139, 31);
            panel5.TabIndex = 11;
            // 
            // reFabricInput
            // 
            reFabricInput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            reFabricInput.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            reFabricInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            reFabricInput.Font = new System.Drawing.Font("Segoe UI", 11F);
            reFabricInput.ForeColor = System.Drawing.Color.Gainsboro;
            reFabricInput.Location = new System.Drawing.Point(5, 5);
            reFabricInput.Name = "reFabricInput";
            reFabricInput.PlaceholderText = "0";
            reFabricInput.Size = new System.Drawing.Size(126, 20);
            reFabricInput.TabIndex = 2;
            reFabricInput.TextChanged += reFabricInput_TextChanged;
            // 
            // reFabricLabel
            // 
            reFabricLabel.AutoSize = true;
            reFabricLabel.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            reFabricLabel.Location = new System.Drawing.Point(12, 135);
            reFabricLabel.Name = "reFabricLabel";
            reFabricLabel.Size = new System.Drawing.Size(52, 15);
            reFabricLabel.TabIndex = 10;
            reFabricLabel.Text = "ReFabric";
            // 
            // panel6
            // 
            panel6.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel6.Controls.Add(reFabricPatternInput);
            panel6.Location = new System.Drawing.Point(157, 153);
            panel6.Name = "panel6";
            panel6.Padding = new System.Windows.Forms.Padding(5);
            panel6.Size = new System.Drawing.Size(101, 31);
            panel6.TabIndex = 12;
            // 
            // reFabricPatternInput
            // 
            reFabricPatternInput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            reFabricPatternInput.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            reFabricPatternInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            reFabricPatternInput.Font = new System.Drawing.Font("Segoe UI", 11F);
            reFabricPatternInput.ForeColor = System.Drawing.Color.Gainsboro;
            reFabricPatternInput.Location = new System.Drawing.Point(5, 5);
            reFabricPatternInput.Name = "reFabricPatternInput";
            reFabricPatternInput.PlaceholderText = "0";
            reFabricPatternInput.Size = new System.Drawing.Size(91, 20);
            reFabricPatternInput.TabIndex = 2;
            reFabricPatternInput.TextChanged += reFabricPatternInput_TextChanged;
            // 
            // reFabricPatternLabel
            // 
            reFabricPatternLabel.AutoSize = true;
            reFabricPatternLabel.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            reFabricPatternLabel.Location = new System.Drawing.Point(157, 135);
            reFabricPatternLabel.Name = "reFabricPatternLabel";
            reFabricPatternLabel.Size = new System.Drawing.Size(45, 15);
            reFabricPatternLabel.TabIndex = 13;
            reFabricPatternLabel.Text = "Pattern";
            // 
            // reBodyPatternLabel
            // 
            reBodyPatternLabel.AutoSize = true;
            reBodyPatternLabel.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            reBodyPatternLabel.Location = new System.Drawing.Point(157, 195);
            reBodyPatternLabel.Name = "reBodyPatternLabel";
            reBodyPatternLabel.Size = new System.Drawing.Size(45, 15);
            reBodyPatternLabel.TabIndex = 17;
            reBodyPatternLabel.Text = "Pattern";
            // 
            // panel7
            // 
            panel7.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel7.Controls.Add(reBodyPatternInput);
            panel7.Location = new System.Drawing.Point(157, 213);
            panel7.Name = "panel7";
            panel7.Padding = new System.Windows.Forms.Padding(5);
            panel7.Size = new System.Drawing.Size(101, 31);
            panel7.TabIndex = 16;
            // 
            // reBodyPatternInput
            // 
            reBodyPatternInput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            reBodyPatternInput.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            reBodyPatternInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            reBodyPatternInput.Font = new System.Drawing.Font("Segoe UI", 11F);
            reBodyPatternInput.ForeColor = System.Drawing.Color.Gainsboro;
            reBodyPatternInput.Location = new System.Drawing.Point(5, 5);
            reBodyPatternInput.Name = "reBodyPatternInput";
            reBodyPatternInput.PlaceholderText = "0";
            reBodyPatternInput.Size = new System.Drawing.Size(91, 20);
            reBodyPatternInput.TabIndex = 2;
            reBodyPatternInput.TextChanged += reBodyPatternInput_TextChanged;
            // 
            // panel8
            // 
            panel8.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            panel8.Controls.Add(reBodyInput);
            panel8.Location = new System.Drawing.Point(12, 213);
            panel8.Name = "panel8";
            panel8.Padding = new System.Windows.Forms.Padding(5);
            panel8.Size = new System.Drawing.Size(139, 31);
            panel8.TabIndex = 15;
            // 
            // reBodyInput
            // 
            reBodyInput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            reBodyInput.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            reBodyInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            reBodyInput.Font = new System.Drawing.Font("Segoe UI", 11F);
            reBodyInput.ForeColor = System.Drawing.Color.Gainsboro;
            reBodyInput.Location = new System.Drawing.Point(5, 5);
            reBodyInput.Name = "reBodyInput";
            reBodyInput.PlaceholderText = "0";
            reBodyInput.Size = new System.Drawing.Size(126, 20);
            reBodyInput.TabIndex = 2;
            reBodyInput.TextChanged += reBodyInput_TextChanged;
            // 
            // reBodyLabel
            // 
            reBodyLabel.AutoSize = true;
            reBodyLabel.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            reBodyLabel.Location = new System.Drawing.Point(12, 195);
            reBodyLabel.Name = "reBodyLabel";
            reBodyLabel.Size = new System.Drawing.Size(47, 15);
            reBodyLabel.TabIndex = 14;
            reBodyLabel.Text = "ReBody";
            // 
            // ItemIDHelper
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(458, 259);
            Controls.Add(reBodyPatternLabel);
            Controls.Add(panel7);
            Controls.Add(panel8);
            Controls.Add(reBodyLabel);
            Controls.Add(reFabricPatternLabel);
            Controls.Add(panel6);
            Controls.Add(panel5);
            Controls.Add(reFabricLabel);
            Controls.Add(panel4);
            Controls.Add(itemStateLabel);
            Controls.Add(panel3);
            Controls.Add(itemRotationLabel);
            Controls.Add(panel2);
            Controls.Add(label1);
            Controls.Add(panel1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "ItemIDHelper";
            ShowIcon = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            Text = "ItemNameParam Helper";
            TopMost = true;
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            panel3.ResumeLayout(false);
            panel3.PerformLayout();
            panel4.ResumeLayout(false);
            panel4.PerformLayout();
            panel5.ResumeLayout(false);
            panel5.PerformLayout();
            panel6.ResumeLayout(false);
            panel6.PerformLayout();
            panel7.ResumeLayout(false);
            panel7.PerformLayout();
            panel8.ResumeLayout(false);
            panel8.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Components.UInt64TextBox encodedIdInput;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button copyIdButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button copyItemIdButton;
        private Components.UInt64TextBox itemIdBox;
        private System.Windows.Forms.Panel panel3;
        private Components.UInt16TextBox itemRotationBox;
        private System.Windows.Forms.Label itemRotationLabel;
        private System.Windows.Forms.Panel panel4;
        private Components.UInt16TextBox itemStateInput;
        private System.Windows.Forms.Label itemStateLabel;
        private System.Windows.Forms.Panel panel5;
        private Components.ByteTextBox reFabricInput;
        private System.Windows.Forms.Label reFabricLabel;
        private System.Windows.Forms.Panel panel6;
        private Components.ByteTextBox reFabricPatternInput;
        private System.Windows.Forms.Label reFabricPatternLabel;
        private System.Windows.Forms.Label reBodyPatternLabel;
        private System.Windows.Forms.Panel panel7;
        private Components.ByteTextBox reBodyPatternInput;
        private System.Windows.Forms.Panel panel8;
        private Components.ByteTextBox reBodyInput;
        private System.Windows.Forms.Label reBodyLabel;
    }
}