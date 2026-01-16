using AltUI.Controls;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace HeavenTool.Forms.Components;

[ToolboxItem(false)]
public class ValidatingTextBox : DarkTextBox
{
    private string _validText;
    private int _selectionStart;
    private int _selectionEnd;
    private bool _dontProcessMessages;

    public event EventHandler<TextValidatingEventArgs> TextValidating;

    protected virtual void OnTextValidating(object sender, TextValidatingEventArgs e) => TextValidating?.Invoke(sender, e);

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (_dontProcessMessages)
            return;

        const int WM_KEYDOWN = 0x100;
        const int WM_ENTERIDLE = 0x121;
        const int VK_DELETE = 0x2e;

        bool delete = m.Msg == WM_KEYDOWN && (int)m.WParam == VK_DELETE;
        if ((m.Msg == WM_KEYDOWN && !delete) || m.Msg == WM_ENTERIDLE)
        {
            DontProcessMessage(() =>
            {
                _validText = Text;
                _selectionStart = SelectionStart;
                _selectionEnd = SelectionLength;
            });
        }

        const int WM_CHAR = 0x102;
        const int WM_PASTE = 0x302;
        if (m.Msg == WM_CHAR || m.Msg == WM_PASTE || delete)
        {
            string newText = null;
            DontProcessMessage(() =>
            {
                newText = Text;
            });

            var e = new TextValidatingEventArgs(newText);
            OnTextValidating(this, e);
            if (e.Cancel)
            {
                DontProcessMessage(() =>
                {
                    Text = _validText;
                    SelectionStart = _selectionStart;
                    SelectionLength = _selectionEnd;
                });
            }
        }
    }

    private void DontProcessMessage(Action action)
    {
        _dontProcessMessages = true;
        try
        {
            action();
        }
        finally
        {
            _dontProcessMessages = false;
        }
    }
}

public class TextValidatingEventArgs : CancelEventArgs
{
    public TextValidatingEventArgs(string newText) => NewText = newText;
    public string NewText { get; }
}
