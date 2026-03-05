using HeavenTool.Forms.BCSV.Controls.Entries;
using System;

namespace HeavenTool.Forms.BCSV.Controls;
public partial class StringEntry : BCSVEntry
{
    public Action<object>? Callback;  
    public StringEntry(object defaultValue, int limit)
    {
        InitializeComponent();
      
        input.ByteSizeLimit = limit;
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
        Callback?.Invoke(input.Text);
    }

    public override object GetValue()
    {
        return input.Text;
    }
}
