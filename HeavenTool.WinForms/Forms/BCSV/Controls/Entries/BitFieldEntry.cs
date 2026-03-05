using HeavenTool.Forms.BCSV.Controls.Entries;
using HeavenTool.IO;
using System;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class BitFieldEntry : BCSVEntry
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

    public Action<byte[]>? Callback;
    private readonly byte[] tagBits;

    public BitFieldEntry(int lenght, object defaultValue, string[]? names = null)
    {
        InitializeComponent();

        if (defaultValue is byte[] value)
        {
            tagBits = new byte[lenght];
            for (int i = 0; i < value.Length; i++)
            {
                tagBits[i] = value[i];
            }
        }
        else throw new Exception("Default value for BitFieldEntry must be a byte array.");

        //input.Text = defaultValue.ToString();
        //Input_TextChanged(this, EventArgs.Empty);
        bitfieldEntries.MaxDropDownItems = 10;
        bitfieldEntries.ValueSeparator = ", ";
        bitfieldEntries.DisplayMember = "Name";
        bool canUpdateText = false;

        bitfieldEntries.ItemCheck += (s, e) =>
        {
            if (bitfieldEntries.Items[e.Index] is not BitFieldItem item)
                return;

            // If something other than "None" is being checked
            if (item.Value >= 0 && e.NewValue == CheckState.Checked)
            {
                bitfieldEntries.SetItemChecked(0, false);
            }

            // If "None" is being checked
            if (item.Value < 0 && e.NewValue == CheckState.Checked)
            {
                for (int i = 1; i < bitfieldEntries.Items.Count; i++)
                    bitfieldEntries.SetItemChecked(i, false);
            }

            // If a non-"None" item is being unchecked
            if (item.Value >= 0 && e.NewValue == CheckState.Unchecked)
            {
                bool anyOtherChecked = bitfieldEntries.CheckedItems
                    .Cast<BitFieldItem>()
                    .Any(x => x.Value >= 0 && bitfieldEntries.Items.IndexOf(x) != e.Index);

                if (!anyOtherChecked)
                {
                    // Schedule after the current event so the state is updated
                    BeginInvoke(() =>
                    {
                        bitfieldEntries.SetItemChecked(0, true);
                    });
                }
            }

            // 
            if (canUpdateText)
                BeginInvoke(() =>
                {
                    bitfieldEntries.UpdateText();
                });
        };

        bitfieldEntries.Items?.Add(new BitFieldItem("None", -1), !(tagBits.Any(x => x != 0)));

        if (names != null && names.Length > lenght * 8) names = null;
        var count = names != null ? names.Length : lenght * 8;

        for (int i = 0; i < count; i++)
            bitfieldEntries.Items?.Add(new BitFieldItem(names != null ? names[i] : $"BitMask_{i}", i), IsFlagSet(i));

        canUpdateText = true;
    }

    public bool IsFlagSet(int bitIndex)
    {
        return (tagBits[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;
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
        // Clear all bits
        Array.Clear(tagBits, 0, tagBits.Length);

        foreach (var obj in bitfieldEntries.CheckedItems)
        {
            if (obj is not BitFieldItem item)
                continue;

            // "None" option
            if (item.Value < 0)
                return tagBits;

            int byteIndex = item.Value / 8;
            int bitOffset = item.Value % 8;

            tagBits[byteIndex] |= (byte)(1 << bitOffset);
        }

        return tagBits;
    }
}