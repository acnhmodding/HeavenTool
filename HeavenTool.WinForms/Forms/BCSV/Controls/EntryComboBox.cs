using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class EntryComboBox : ComboBox
{
    private bool initialSelectionCleared;

    protected virtual bool IsDropDownOpen => DroppedDown;

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        // Native initialization and initial layout can restore the edit selection
        // after the containing entry has loaded. Clear it once those have finished.
        if (!initialSelectionCleared && DropDownStyle != ComboBoxStyle.DropDownList)
        {
            Select(0, 0);
            initialSelectionCleared = true;
            BeginInvoke(() =>
            {
                if (!IsDisposed && !Disposing)
                    Select(0, 0);
            });
        }
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        Select(0,0);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_MOUSEWHEEL = 0x020A;
        if (m.Msg == WM_MOUSEWHEEL && !IsDropDownOpen)
        {
            // Forward the original wheel message so the enclosing page scrolls
            // without letting the native combo change its selected value.
            m.Result = Parent is { IsHandleCreated: true } parent
                ? SendMessage(parent.Handle, m.Msg, m.WParam, m.LParam)
                : IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}