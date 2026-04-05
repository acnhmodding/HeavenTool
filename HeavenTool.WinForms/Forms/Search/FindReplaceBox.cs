using System;
using System.Collections.Generic;
using System.Windows.Forms;
using HeavenTool.Forms.Search;

namespace HeavenTool.Forms;

public partial class FindReplaceBox : Form
{
    private readonly Form _callerForm;
    private readonly IFindReplaceable? _target;

    public FindReplaceBox(Form caller)
    {
        InitializeComponent();
        _callerForm = caller;
        Owner = caller;
        if (caller is IFindReplaceable fr)
            _target = fr;
    }

    public new void Show()
    {
        Owner = _callerForm;
        base.Show();
    }

    private void FindReplaceBox_FormClosing(object? sender, FormClosingEventArgs e)
    {
        Owner = null;
        e.Cancel = true;
        Hide();
    }

    private void FindReplaceBox_Activated(object? sender, EventArgs e) => Opacity = 1;

    private void FindReplaceBox_Deactivate(object? sender, EventArgs e) => Opacity = 0.5d;

    private void FindText_TextChanged(object? sender, EventArgs e) =>
        replaceAllButton.Enabled = !string.IsNullOrEmpty(findText.Text);

    private void ReplaceAllButton_Click(object? sender, EventArgs e)
    {
        if (_target == null || string.IsNullOrEmpty(findText.Text))
            return;

        var excluded = ParseExcludedColumnNames(excludeColumnsText.Text);
        var count = _target.ReplaceAllExact(findText.Text, replaceText.Text, caseSensitiveCheckBox.Checked, excluded);
        if (count > 0)
            MessageBox.Show(this, $"Replaced {count} cell(s).", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static List<string> ParseExcludedColumnNames(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var token in text.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (t.Length > 0)
                result.Add(t);
        }

        return result;
    }
}
