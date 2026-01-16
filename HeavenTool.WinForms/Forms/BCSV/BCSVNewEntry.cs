using System;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV;

public partial class BCSVEntryEditor : Form
{
    bool callbackHasBeenCalled = false;
    private readonly Action AddEntryCallback;
    public BCSVEntryEditor(Action callback, bool isUpdateWindow = false)
    {
        InitializeComponent();

        // "hide" our tracker
        sizeTracker.Height = 0;

        AddEntryCallback = callback;

        if (isUpdateWindow) addEntryButton.Text = "Update";
    }

    private void ContentPanel_SizeChanged(object sender, EventArgs e)
    {
        sizeTracker.Width = contentPanel.DisplayRectangle.Width - SystemInformation.VerticalScrollBarWidth;
        
    }

    public void AddContent(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        contentPanel.Controls.Add(content);
        content.Dock = DockStyle.Top;
        content.BackColor = Color.Transparent;
    }

    public void MoveContent(Control content, int index)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (index < 0 || index >= content.Controls.Count) return;
        if (!contentPanel.Controls.Contains(content)) return;

        contentPanel.Controls.SetChildIndex(content, index);
    }


    private void AddEntryButton_Click(object sender, EventArgs e)
    {
        if (!callbackHasBeenCalled)
        {
            AddEntryCallback.Invoke();
            callbackHasBeenCalled = true;
        }

        Close();
    }

    private void BCSVEntryEditor_ResizeEnd(object sender, EventArgs e)
    {
        ContentPanel_SizeChanged(this, e);
    }
}
