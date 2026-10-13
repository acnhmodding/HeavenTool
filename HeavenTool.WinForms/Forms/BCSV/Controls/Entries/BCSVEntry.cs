using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls.Entries;

public class BCSVEntry : UserControl
{
    private bool initialSelectionCleared;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (initialSelectionCleared)
            return;

        initialSelectionCleared = true;
        ClearInitialSelection(this);
    }

    private static void ClearInitialSelection(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBoxBase textBox)
            {
                textBox.HideSelection = true;
                textBox.Select(0, 0);
            }
            else if (control is CheckedComboBox checkedComboBox)
            {
                checkedComboBox.Select(0, 0);
            }
            else if (control is ComboBox comboBox && comboBox.DropDownStyle != ComboBoxStyle.DropDownList)
            {
                comboBox.Select(0, 0);
            }

            if (control.HasChildren)
                ClearInitialSelection(control);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Field? Field { get; set; }

    public void SetField(Field field) => Field = field;
    public virtual void SetCallback(Action<object> newValueCallback)
    {
        throw new NotImplementedException();
    }

    public virtual void SetPropertyName(string name)
    {
        throw new NotImplementedException();
    }

    public virtual void SetUniqueIdentifier()
    {
        throw new NotImplementedException();
    }

    public virtual object GetValue()
    {
        throw new NotImplementedException();
    }
}
