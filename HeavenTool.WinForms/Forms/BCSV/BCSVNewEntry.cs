using HeavenTool.Forms.BCSV.Controls.Entries;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV;

public partial class BCSVEntryEditor : Form
{
    bool callbackHasBeenCalled = false;
    private Action? addEntryCallback;

    public BCSVEntryEditor(bool isUpdateWindow = false)
    {
        InitializeComponent();

        DoubleBuffered = true;

        contentPanel.Layout += ContentPanel_SizeChanged;

        if (isUpdateWindow)
            addEntryButton.Text = "Update";

        MinimumSize = new Size(340, 170);
    }

    public BCSVEntryEditor(Action callback, bool isUpdateWindow = false) : this(isUpdateWindow)
    {
        addEntryCallback = callback;
    }

    public void SetCallback(Action callback)
    {
        addEntryCallback = callback;
    }

    private void ContentPanel_SizeChanged(object? sender, EventArgs e)
    {
        // Use DisplayRectangle.Width which represents the client area available for contained controls
        var targetWidth = Math.Max(0, contentPanel.DisplayRectangle.Width - contentPanel.Padding.Left - contentPanel.Padding.Right);

        contentPanel.SuspendLayout();
    
        foreach (Control c in contentPanel.Controls)
        {
            var horizMargin = c.Margin.Left + c.Margin.Right;
            var w = Math.Max(0, targetWidth - horizMargin);
            if (c.Width != w) c.Width = w;
        }

        contentPanel.ResumeLayout();
    }

    public void AddContent(BCSVEntry content)
    {
        ArgumentNullException.ThrowIfNull(content);

        contentPanel.Controls.Add(content);

        content.Margin = new Padding(0, 3, 0, 3);
    }

    public void MoveContent(Control content, int index)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (index < 0 || index >= contentPanel.Controls.Count) return;
        if (!contentPanel.Controls.Contains(content)) return;

        contentPanel.Controls.SetChildIndex(content, index);
    }

    public BCSVEntry[] GetEntries()
    {
        return [.. contentPanel.Controls.Cast<BCSVEntry>()];
    }

    private void AddEntryButton_Click(object sender, EventArgs e)
    {
        if (!callbackHasBeenCalled)
        {
            addEntryCallback?.Invoke();
            callbackHasBeenCalled = true;
        }

        Close();
    }
}
