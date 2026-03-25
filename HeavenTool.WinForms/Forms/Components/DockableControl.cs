using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using System.ComponentModel;
using System;

namespace HeavenTool.Forms.Components;

public class DockableControl<T> : DockContent where T : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public T Control { get; set; }

    public DockableControl(T control, string text)
    {
        Control = control;
        Text = text;

        CloseButton = false;
        CloseButtonVisible = false;

        control.Dock = DockStyle.Fill;
        Controls.Add(control);
    }
}

public static class DockableControl
{
    public static DockableControl<T> Create<T>(T control, string text) where T : Control
    {
        return new DockableControl<T>(control, text);
    }
}