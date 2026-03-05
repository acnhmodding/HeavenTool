using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.ComponentModel;

namespace HeavenTool.Forms.BCSV.Controls;

public class CheckedComboBox : ComboBox
{
    private readonly ToolStripDropDown _dropDown;
    private readonly CheckedListBox _checkedListBox;
    private readonly ToolStripControlHost _host;

    private bool _suppressClose;
    private bool _isDropped;

    public event ItemCheckEventHandler? ItemCheck;

    public CheckedComboBox()
    {
        DrawMode = DrawMode.Normal;
        DropDownStyle = ComboBoxStyle.DropDown;
        ValueSeparator = ", ";

        // Prevent default dropdown
        DropDownHeight = 1;

        _checkedListBox = new CheckedListBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            CheckOnClick = true
        };


        _checkedListBox.ItemCheck += CheckedListBox_ItemCheck;

        _checkedListBox.KeyDown += CheckedListBox_KeyDown;

        _host = new ToolStripControlHost(_checkedListBox)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };

        _dropDown = new ToolStripDropDown
        {
            Padding = Padding.Empty
        };

        _dropDown.Items.Add(_host);
        _dropDown.Closing += DropDown_Closing;
    }

    private void CheckedListBox_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        // Update text using future state
        UpdateTextWithItemCheck(e);

        ItemCheck?.Invoke(sender, e);
    }

    [DefaultValue(", ")]
    public string ValueSeparator { get; set; }

    public new CheckedListBox.ObjectCollection Items => _checkedListBox.Items;
    public CheckedListBox.CheckedItemCollection CheckedItems => _checkedListBox.CheckedItems;
    public CheckedListBox.CheckedIndexCollection CheckedIndices => _checkedListBox.CheckedIndices;

    [DefaultValue("Name")]
    public new string DisplayMember
    {
        get => _checkedListBox.DisplayMember;
        set => _checkedListBox.DisplayMember = value;
    }

    [DefaultValue(true)]
    public bool CheckOnClick
    {
        get => _checkedListBox.CheckOnClick;
        set => _checkedListBox.CheckOnClick = value;
    }

    public bool GetItemChecked(int index)
        => _checkedListBox.GetItemChecked(index);

    public void SetItemChecked(int index, bool isChecked)
    {
        _checkedListBox.SetItemChecked(index, isChecked);
        UpdateText();
    }

    public CheckState GetItemCheckState(int index)
        => _checkedListBox.GetItemCheckState(index);

    public void SetItemCheckState(int index, CheckState state)
    {
        _checkedListBox.SetItemCheckState(index, state);
        UpdateText();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            ToggleDropDown();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down && !_isDropped)
        {
            ShowDropDown();
            e.Handled = true;
            return;
        }

        e.Handled = true;
        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        e.Handled = true; // prevent manual editing
        base.OnKeyPress(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_isDropped)
        {
            // If the dropdown is open, forward the mouse wheel event to the CheckedListBox
            _checkedListBox.Focus();
            var args = new HandledMouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta);
            base.OnMouseWheel(args);
        }
        else
        {
            // Prevent the dropdown from opening when scrolling
            ((HandledMouseEventArgs)e).Handled = true;
        }
    }

    private void ToggleDropDown()
    {
        if (_isDropped)
            _dropDown.Close();
        else
            ShowDropDown();
    }

    private void ShowDropDown()
    {
        if (_checkedListBox.Items.Count == 0)
            return;

        int itemCount = Math.Min(MaxDropDownItems, _checkedListBox.Items.Count);
        int height = _checkedListBox.ItemHeight * itemCount + 2;

        _checkedListBox.Height = height;
        _checkedListBox.Width = Width;

        _host.Size = _checkedListBox.Size;

        _dropDown.Show(this, new Point(0, Height));
        _checkedListBox.Focus();

        _isDropped = true;
    }

    private void DropDown_Closing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        if (_suppressClose)
        {
            e.Cancel = true;
            return;
        }

        _isDropped = false;
        UpdateText();
    }

    private void CheckedListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            _dropDown.Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            _suppressClose = false;
            _dropDown.Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            for (int i = 0; i < _checkedListBox.Items.Count; i++)
                _checkedListBox.SetItemChecked(i, e.Shift);

            UpdateText();
            e.Handled = true;
        }
    }

    private void UpdateTextWithItemCheck(ItemCheckEventArgs e)
    {
        StringBuilder sb = new();

        for (int i = 0; i < _checkedListBox.Items.Count; i++)
        {
            bool isChecked;

            if (i == e.Index)
                isChecked = e.NewValue == CheckState.Checked;
            else
                isChecked = _checkedListBox.GetItemChecked(i);

            if (isChecked)
            {
                sb.Append(_checkedListBox.GetItemText(_checkedListBox.Items[i]));
                sb.Append(ValueSeparator);
            }
        }

        if (sb.Length > 0)
            sb.Length -= ValueSeparator.Length;

        Text = sb.ToString();
    }

    private void UpdateText()
    {
        StringBuilder sb = new();

        foreach (var item in _checkedListBox.CheckedItems)
        {
            sb.Append(_checkedListBox.GetItemText(item));
            sb.Append(ValueSeparator);
        }

        if (sb.Length > 0)
            sb.Length -= ValueSeparator.Length;

        Text = sb.ToString();
    }
}