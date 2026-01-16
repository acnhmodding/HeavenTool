using AltUI.Controls;

namespace HeavenTool.Forms
{
    partial class SearchBox
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SearchBox));
            searchValue = new DarkTextBox();
            label1 = new DarkLabel();
            findButton = new DarkButton();
            containsButton = new DarkRadioButton();
            exactlyButton = new DarkRadioButton();
            matchesCount = new DarkLabel();
            reverseDirectionCheckbox = new DarkCheckBox();
            caseSensitivivtyCheckbox = new DarkCheckBox();
            panel1 = new System.Windows.Forms.Panel();
            optionsGroupBox = new DarkGroupBox();
            panel1.SuspendLayout();
            optionsGroupBox.SuspendLayout();
            SuspendLayout();
            // 
            // searchValue
            // 
            searchValue.BackColor = System.Drawing.Color.FromArgb(90, 90, 90);
            searchValue.BorderStyle = System.Windows.Forms.BorderStyle.None;
            searchValue.ForeColor = System.Drawing.Color.FromArgb(230, 230, 230);
            searchValue.Location = new System.Drawing.Point(4, 4);
            searchValue.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            searchValue.Name = "searchValue";
            searchValue.Size = new System.Drawing.Size(343, 16);
            searchValue.TabIndex = 0;
            searchValue.TextChanged += SearchValue_TextChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(15, 16);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(35, 15);
            label1.TabIndex = 1;
            label1.Text = "Value";
            // 
            // findButton
            // 
            findButton.BorderColour = System.Drawing.Color.Empty;
            findButton.Enabled = false;
            findButton.ForeColor = System.Drawing.Color.Gainsboro;
            findButton.Location = new System.Drawing.Point(254, 137);
            findButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            findButton.Name = "findButton";
            findButton.Padding = new System.Windows.Forms.Padding(5);
            findButton.Size = new System.Drawing.Size(168, 28);
            findButton.TabIndex = 2;
            findButton.Text = "Next";
            findButton.Click += FindButton_Click;
            // 
            // containsButton
            // 
            containsButton.AutoSize = true;
            containsButton.Checked = true;
            containsButton.ForeColor = System.Drawing.Color.Gainsboro;
            containsButton.Location = new System.Drawing.Point(7, 22);
            containsButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            containsButton.Name = "containsButton";
            containsButton.Size = new System.Drawing.Size(72, 19);
            containsButton.TabIndex = 3;
            containsButton.TabStop = true;
            containsButton.Text = "Contains";
            // 
            // exactlyButton
            // 
            exactlyButton.AutoSize = true;
            exactlyButton.ForeColor = System.Drawing.Color.Gainsboro;
            exactlyButton.Location = new System.Drawing.Point(91, 22);
            exactlyButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            exactlyButton.Name = "exactlyButton";
            exactlyButton.Size = new System.Drawing.Size(61, 19);
            exactlyButton.TabIndex = 4;
            exactlyButton.Text = "Exactly";
            // 
            // matchesCount
            // 
            matchesCount.AutoSize = true;
            matchesCount.Location = new System.Drawing.Point(15, 144);
            matchesCount.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            matchesCount.Name = "matchesCount";
            matchesCount.Size = new System.Drawing.Size(106, 15);
            matchesCount.TabIndex = 5;
            matchesCount.Text = "No matches found";
            // 
            // reverseDirectionCheckbox
            // 
            reverseDirectionCheckbox.AutoSize = true;
            reverseDirectionCheckbox.ForeColor = System.Drawing.Color.Gainsboro;
            reverseDirectionCheckbox.Location = new System.Drawing.Point(7, 47);
            reverseDirectionCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            reverseDirectionCheckbox.Name = "reverseDirectionCheckbox";
            reverseDirectionCheckbox.Offset = 1;
            reverseDirectionCheckbox.Size = new System.Drawing.Size(117, 19);
            reverseDirectionCheckbox.TabIndex = 6;
            reverseDirectionCheckbox.Text = "Reverse Direction";
            // 
            // caseSensitivivtyCheckbox
            // 
            caseSensitivivtyCheckbox.AutoSize = true;
            caseSensitivivtyCheckbox.ForeColor = System.Drawing.Color.Gainsboro;
            caseSensitivivtyCheckbox.Location = new System.Drawing.Point(132, 47);
            caseSensitivivtyCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            caseSensitivivtyCheckbox.Name = "caseSensitivivtyCheckbox";
            caseSensitivivtyCheckbox.Offset = 1;
            caseSensitivivtyCheckbox.Size = new System.Drawing.Size(100, 19);
            caseSensitivivtyCheckbox.TabIndex = 7;
            caseSensitivivtyCheckbox.Text = "Case Sensitive";
            // 
            // panel1
            // 
            panel1.BackColor = System.Drawing.Color.FromArgb(90, 90, 90);
            panel1.Controls.Add(searchValue);
            panel1.Location = new System.Drawing.Point(71, 12);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(351, 24);
            panel1.TabIndex = 8;
            // 
            // optionsGroupBox
            // 
            optionsGroupBox.Controls.Add(caseSensitivivtyCheckbox);
            optionsGroupBox.Controls.Add(containsButton);
            optionsGroupBox.Controls.Add(exactlyButton);
            optionsGroupBox.Controls.Add(reverseDirectionCheckbox);
            optionsGroupBox.Location = new System.Drawing.Point(15, 48);
            optionsGroupBox.Name = "optionsGroupBox";
            optionsGroupBox.Size = new System.Drawing.Size(407, 76);
            optionsGroupBox.TabIndex = 9;
            optionsGroupBox.TabStop = false;
            optionsGroupBox.Text = "Options";
            // 
            // SearchBox
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(435, 178);
            Controls.Add(optionsGroupBox);
            Controls.Add(panel1);
            Controls.Add(matchesCount);
            Controls.Add(findButton);
            Controls.Add(label1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SearchBox";
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            Text = "Search";
            Activated += SearchBox_Activated;
            Deactivate += SearchBox_Deactivate;
            FormClosing += SearchBox_FormClosing;
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            optionsGroupBox.ResumeLayout(false);
            optionsGroupBox.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DarkTextBox searchValue;
        private DarkLabel label1;
        private DarkButton findButton;
        private DarkRadioButton containsButton;
        private DarkRadioButton exactlyButton;
        private DarkLabel matchesCount;
        private DarkCheckBox reverseDirectionCheckbox;
        private DarkCheckBox caseSensitivivtyCheckbox;
        private System.Windows.Forms.Panel panel1;
        private DarkGroupBox optionsGroupBox;
    }
}