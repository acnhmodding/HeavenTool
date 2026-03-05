using HeavenTool.Forms.BCSV.Controls.Entries;
using System;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class SbyteEntry : BCSVEntry
{
    public Action<object>? Callback;  
    public SbyteEntry(object defaultValue)
    {
        InitializeComponent();
      
        input.Text = defaultValue.ToString();
        Input_TextChanged(this, EventArgs.Empty);
    }

    public override void SetCallback(Action<object> newValueCallback)
    {
        Callback = newValueCallback;
    }

    public override void SetPropertyName(string name)
    {
        propertyNameLabel.Text = name;
    }

    public override void SetUniqueIdentifier()
    {
        propertyNameLabel.Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold);
    }

    private void Input_TextChanged(object sender, EventArgs e)
    {
        if (ushort.TryParse(input.Text, out ushort value))
            Callback?.Invoke(value);
    }

    public override object GetValue()
    {
        if (ushort.TryParse(input.Text, out ushort value))
            return value;
        else return ushort.MinValue;
    }
}