using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace HeavenTool.Forms.BCSV.Controls.Entries;

public class BCSVEntry : UserControl
{
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
