using HeavenTool.Forms.BCSV.Controls.Entries;
using System;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;
public partial class StringEntry : UserControl, IBCSVEntry
{
    public Action<object> Callback;  
    public StringEntry(object defaultValue, int limit)
    {
        InitializeComponent();
      
        input.ByteSizeLimit = limit;
        input.Text = defaultValue.ToString();

        Input_TextChanged(this, EventArgs.Empty);
    }

    public void SetCallback(Action<object> newValueCallback)
    {
        Callback = newValueCallback;
    }

    public void SetPropertyName(string name)
    {
        propertyNameLabel.Text = name;
    }

    public void SetUniqueIdentifier()
    {
       propertyNameLabel.Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold);
    }

    private void Input_TextChanged(object sender, EventArgs e)
    {
        Callback?.Invoke(input.Text);
    }
}
