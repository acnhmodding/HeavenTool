using AltUI.Controls;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms;

partial class FindReplaceBox
{
    private System.ComponentModel.IContainer? components;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        findLabel = new DarkLabel();
        replaceLabel = new DarkLabel();
        findPanel = new Panel();
        findText = new DarkTextBox();
        replacePanel = new Panel();
        replaceText = new DarkTextBox();
        excludeColumnsLabel = new DarkLabel();
        excludePanel = new Panel();
        excludeColumnsText = new DarkTextBox();
        excludeColumnsHintLabel = new DarkLabel();
        caseSensitiveCheckBox = new DarkCheckBox();
        replaceAllButton = new DarkButton();
        hintLabel = new DarkLabel();
        findPanel.SuspendLayout();
        replacePanel.SuspendLayout();
        excludePanel.SuspendLayout();
        SuspendLayout();
        //
        // findLabel
        //
        findLabel.AutoSize = true;
        findLabel.ForeColor = Color.Gainsboro;
        findLabel.Location = new Point(12, 15);
        findLabel.Name = "findLabel";
        findLabel.Size = new Size(31, 15);
        findLabel.Text = "Find";
        //
        // replaceLabel
        //
        replaceLabel.AutoSize = true;
        replaceLabel.ForeColor = Color.Gainsboro;
        replaceLabel.Location = new Point(12, 50);
        replaceLabel.Name = "replaceLabel";
        replaceLabel.Size = new Size(31, 15);
        replaceLabel.Text = "Replace";
        const int contentLeft = 128;
        const int contentWidth = 400;
        const int innerTextWidth = contentWidth - 8;
        //
        // findPanel
        //
        findPanel.BackColor = Color.FromArgb(90, 90, 90);
        findPanel.Controls.Add(findText);
        findPanel.Location = new Point(contentLeft, 12);
        findPanel.Name = "findPanel";
        findPanel.Size = new Size(contentWidth, 24);
        //
        // findText
        //
        findText.BackColor = Color.FromArgb(90, 90, 90);
        findText.BorderStyle = BorderStyle.None;
        findText.ForeColor = Color.FromArgb(230, 230, 230);
        findText.Location = new Point(4, 4);
        findText.Name = "findText";
        findText.Size = new Size(innerTextWidth, 16);
        findText.TextChanged += FindText_TextChanged;
        //
        // replacePanel
        //
        replacePanel.BackColor = Color.FromArgb(90, 90, 90);
        replacePanel.Controls.Add(replaceText);
        replacePanel.Location = new Point(contentLeft, 47);
        replacePanel.Name = "replacePanel";
        replacePanel.Size = new Size(contentWidth, 24);
        //
        // replaceText
        //
        replaceText.BackColor = Color.FromArgb(90, 90, 90);
        replaceText.BorderStyle = BorderStyle.None;
        replaceText.ForeColor = Color.FromArgb(230, 230, 230);
        replaceText.Location = new Point(4, 4);
        replaceText.Name = "replaceText";
        replaceText.Size = new Size(innerTextWidth, 16);
        //
        // excludeColumnsLabel
        //
        excludeColumnsLabel.AutoSize = false;
        excludeColumnsLabel.ForeColor = Color.Gainsboro;
        excludeColumnsLabel.Location = new Point(12, 78);
        excludeColumnsLabel.Name = "excludeColumnsLabel";
        excludeColumnsLabel.Size = new Size(112, 36);
        excludeColumnsLabel.Text = "Exclude columns";
        excludeColumnsLabel.TextAlign = ContentAlignment.TopLeft;
        //
        // excludePanel
        //
        excludePanel.BackColor = Color.FromArgb(90, 90, 90);
        excludePanel.Controls.Add(excludeColumnsText);
        excludePanel.Location = new Point(contentLeft, 78);
        excludePanel.Name = "excludePanel";
        excludePanel.Size = new Size(contentWidth, 56);
        //
        // excludeColumnsText
        //
        excludeColumnsText.BackColor = Color.FromArgb(90, 90, 90);
        excludeColumnsText.BorderStyle = BorderStyle.None;
        excludeColumnsText.ForeColor = Color.FromArgb(230, 230, 230);
        excludeColumnsText.Location = new Point(4, 4);
        excludeColumnsText.Multiline = true;
        excludeColumnsText.Name = "excludeColumnsText";
        excludeColumnsText.ScrollBars = ScrollBars.Vertical;
        excludeColumnsText.Size = new Size(innerTextWidth, 48);
        excludeColumnsText.WordWrap = true;
        //
        // excludeColumnsHintLabel
        //
        excludeColumnsHintLabel.AutoSize = true;
        excludeColumnsHintLabel.ForeColor = Color.FromArgb(160, 160, 160);
        excludeColumnsHintLabel.Location = new Point(contentLeft, 136);
        excludeColumnsHintLabel.MaximumSize = new Size(contentWidth, 0);
        excludeColumnsHintLabel.Name = "excludeColumnsHintLabel";
        excludeColumnsHintLabel.Text = "Column header text, one per line or comma-separated (case-insensitive)";
        //
        // caseSensitiveCheckBox
        //
        caseSensitiveCheckBox.AutoSize = true;
        caseSensitiveCheckBox.ForeColor = Color.Gainsboro;
        caseSensitiveCheckBox.Location = new Point(15, 174);
        caseSensitiveCheckBox.Name = "caseSensitiveCheckBox";
        caseSensitiveCheckBox.Offset = 1;
        caseSensitiveCheckBox.Size = new Size(99, 19);
        caseSensitiveCheckBox.Text = "Case sensitive";
        //
        // replaceAllButton
        //
        replaceAllButton.BorderColour = Color.Empty;
        replaceAllButton.Enabled = false;
        replaceAllButton.ForeColor = Color.Gainsboro;
        replaceAllButton.Location = new Point(372, 204);
        replaceAllButton.Name = "replaceAllButton";
        replaceAllButton.Padding = new Padding(5);
        replaceAllButton.Size = new Size(168, 28);
        replaceAllButton.Text = "Replace all";
        replaceAllButton.Click += ReplaceAllButton_Click;
        //
        // hintLabel
        //
        hintLabel.ForeColor = Color.FromArgb(180, 180, 180);
        hintLabel.Location = new Point(12, 204);
        hintLabel.Name = "hintLabel";
        hintLabel.Size = new Size(350, 28);
        hintLabel.Text = "Exact match on displayed text";
        //
        // FindReplaceBox
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(31, 31, 32);
        ClientSize = new Size(552, 244);
        Controls.Add(hintLabel);
        Controls.Add(replaceAllButton);
        Controls.Add(caseSensitiveCheckBox);
        Controls.Add(excludeColumnsHintLabel);
        Controls.Add(excludePanel);
        Controls.Add(excludeColumnsLabel);
        Controls.Add(replacePanel);
        Controls.Add(findPanel);
        Controls.Add(replaceLabel);
        Controls.Add(findLabel);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Margin = new Padding(4, 3, 4, 3);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "FindReplaceBox";
        ShowIcon = false;
        ShowInTaskbar = false;
        SizeGripStyle = SizeGripStyle.Hide;
        Text = "Find and Replace";
        Activated += FindReplaceBox_Activated;
        Deactivate += FindReplaceBox_Deactivate;
        FormClosing += FindReplaceBox_FormClosing;
        findPanel.ResumeLayout(false);
        findPanel.PerformLayout();
        replacePanel.ResumeLayout(false);
        replacePanel.PerformLayout();
        excludePanel.ResumeLayout(false);
        excludePanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private DarkLabel findLabel;
    private DarkLabel replaceLabel;
    private Panel findPanel;
    private DarkTextBox findText;
    private Panel replacePanel;
    private DarkTextBox replaceText;
    private DarkLabel excludeColumnsLabel;
    private Panel excludePanel;
    private DarkTextBox excludeColumnsText;
    private DarkLabel excludeColumnsHintLabel;
    private DarkCheckBox caseSensitiveCheckBox;
    private DarkButton replaceAllButton;
    private DarkLabel hintLabel;
}
