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

    protected override void OnResizeBegin(EventArgs e)
    {
        contentPanel.BeginResize();
        base.OnResizeBegin(e);
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        contentPanel.EndResize();
        base.OnResizeEnd(e);
    }

    public void AddContent(BCSVEntry content)
    {
        ArgumentNullException.ThrowIfNull(content);

        content.Margin = new Padding(0, 3, 0, 3);
        content.Dock = DockStyle.Top;
        content.TabIndex = contentPanel.Controls.Count;
        var row = contentPanel.RowCount++;
        contentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, content.Height + content.Margin.Vertical));
        contentPanel.Controls.Add(content, 0, row);
    }

    public void MoveContent(Control content, int index)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (index < 0 || index >= contentPanel.Controls.Count) return;
        if (!contentPanel.Controls.Contains(content)) return;

        var entries = GetEntries().Cast<Control>().ToList();
        entries.Remove(content);
        entries.Insert(index, content);
        contentPanel.SuspendLayout();
        try
        {
            for (int row = 0; row < entries.Count; row++)
            {
                contentPanel.SetRow(entries[row], row);
                entries[row].TabIndex = row;
                contentPanel.RowStyles[row].Height = entries[row].Height + entries[row].Margin.Vertical;
            }
        }
        finally
        {
            contentPanel.ResumeLayout(true);
        }
    }

    public BCSVEntry[] GetEntries()
    {
        return [.. contentPanel.Controls.Cast<BCSVEntry>().OrderBy(contentPanel.GetRow)];
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