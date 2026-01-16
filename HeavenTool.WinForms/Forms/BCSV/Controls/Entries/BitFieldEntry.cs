using HeavenTool.Forms.BCSV.Controls.Entries;
using System;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class BitFieldEntry : UserControl, IBCSVEntry
{
    public class BitFieldItem(string name, int val)
    {
        public int Value
        {
            get { return val; }
            set { val = value; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }
    }

    public Action<byte[]> Callback;
    private readonly byte[] tagBits;

    public BitFieldEntry(int lenght, object defaultValue, string[] names = null)
    {
        InitializeComponent();

        if (defaultValue is byte[] value)
            tagBits = value;

        //input.Text = defaultValue.ToString();
        //Input_TextChanged(this, EventArgs.Empty);
        checkedComboBox1.MaxDropDownItems = 10;
        checkedComboBox1.ValueSeparator = ", ";
        checkedComboBox1.DisplayMember = "Name";

        checkedComboBox1.Items.Add(new BitFieldItem("None", -1), !tagBits.Any(x => x != 0));

        if (names != null && names.Length > lenght * 8) names = null;
        var count = names != null ? names.Length : lenght * 8;

        for (int i = 0; i < count; i++)
            checkedComboBox1.Items.Add(new BitFieldItem(names != null ? names[i] : $"BitMask_{i}", i), IsFlagSet(i));
        
    }

    public bool IsFlagSet(int bitIndex)
    {
        return (tagBits[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;
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
        //if (ushort.TryParse(input.Text, out ushort value))
        //    Callback?.Invoke(value);
    }
}