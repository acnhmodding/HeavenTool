using HeavenTool.Forms.BCSV.Controls.Entries;
using System;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class Int32Entry : BCSVEntry
{
    public Action<object>? Callback;  
    public Int32Entry(object defaultValue)
    {
        InitializeComponent();

        input.Text = defaultValue.ToString();
        Input_TextChanged(this, EventArgs.Empty);
    }

    public override object GetValue()
    {
        if (int.TryParse(input.Text, out int value))
            return value;
        else return int.MinValue;
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
        if (int.TryParse(input.Text, out int value))
            Callback?.Invoke(value);
    }
}
