using HeavenTool.Forms.BCSV.Controls.Entries;
using HeavenTool.IO;
using System;
using System.Linq;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class CRC32Entry : BCSVEntry
{
    public class CRC32ComboBoxEntry(string name, uint val)
    {
        public uint Value
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

    public Action<object>? Callback;

    public CRC32Entry(object defaultValue, uint columnHash)
    {
        InitializeComponent();

        if (defaultValue is not uint value)
            throw new Exception("Default value for CRC32 must be a uint");

        crcComboBox.MaxDropDownItems = 10;
        crcComboBox.DisplayMember = "Name";
        crcComboBox.ValueMember = "Value";

        if (HashManager.EnumListCRC32.TryGetValue(columnHash, out var list))
        {
            CRC32ComboBoxEntry[] items = [.. list.Select(x => new CRC32ComboBoxEntry(x.GetHashTranslation(), x))];

            crcComboBox.BeginUpdate();
            crcComboBox.Items.AddRange(items);
            crcComboBox.EndUpdate();

            int index = Array.FindIndex(items, x => x.Value == value);
            if (index >= 0)
                crcComboBox.SelectedIndex = index;
        }
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

    public override object GetValue()
    {
        if (crcComboBox.SelectedValue is uint val)
            return val;

        if (crcComboBox.SelectedItem is CRC32ComboBoxEntry entry)
            return entry.Value;
        else return 0u;
    }
}