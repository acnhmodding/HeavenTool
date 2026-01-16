using System;
using System.Windows.Forms;

namespace HeavenTool.Forms.Pack
{
    public partial class ItemIDHelper : Form
    {
        public ItemIDHelper()
        {
            InitializeComponent();
        }

        public static long Encode(ushort id, ushort rotation, byte fabricVariantIndex, byte fabricPatternIndex, byte rebodyVariantIndex, byte rebodyPatternIndex, ushort itemState)
        {
            var refabric = (fabricPatternIndex << 3) | fabricVariantIndex;
            var rebody = (rebodyPatternIndex << 3) | rebodyVariantIndex;

            long encoded = id;
            encoded |= (long)rotation << 16;
            encoded |= (long)refabric << 32;
            encoded |= (long)rebody << 40;
            encoded |= (long)itemState << 48;

            return encoded;
        }

        bool shouldUpdate = true;
        private void UpdateEncodedId()
        {
            if (!shouldUpdate) return;

            if (!ushort.TryParse(itemIdBox.Text, out ushort itemId)) return;
            if (!ushort.TryParse(itemRotationBox.Text, out ushort rotation)) return;

            if (!byte.TryParse(reFabricInput.Text, out byte reFabricIndex)) return;
            if (!byte.TryParse(reFabricPatternInput.Text, out byte reFabricPattern)) return;
            if (!byte.TryParse(reBodyInput.Text, out byte reBodyIndex)) return;
            if (!byte.TryParse(reBodyPatternInput.Text, out byte reBodyPattern)) return;

            if (!ushort.TryParse(itemStateInput.Text, out ushort itemState)) return;

            var encodedId = Encode(itemId, rotation, reFabricIndex, reFabricPattern, reBodyIndex, reBodyPattern, itemState);

            shouldEncode = false;
            encodedIdInput.Text = encodedId.ToString();
            shouldEncode = true;
        }

        private void copyIdButton_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(encodedIdInput.Text);
        }
        private void copyItemIdButton_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(itemIdBox.Text);
        }

        private bool shouldEncode = true;
        private void encodedIdInput_TextChanged(object sender, EventArgs e)
        {
            if (!shouldEncode) return;
            if (!ulong.TryParse(encodedIdInput.Text, out ulong encoded)) return;

            ushort id = (ushort)encoded;
            ushort rotation = (byte)((encoded >> 16) & 0xFFFF);
            byte refabric = (byte)((encoded >> 32) & 0xFF);
            byte rebody = (byte)((encoded >> 40) & 0xFF);
            ushort itemState = (ushort)(encoded >> 48);

            var fabricVariantIndex = refabric & 0x7;
            var fabricPatternIndex = refabric >> 3;

            var rebodyVariantIndex = rebody & 0x7;
            var rebodyPatternIndex = rebody >> 3;

            shouldUpdate = false;
            itemIdBox.Text = id.ToString();
            itemRotationBox.Text = rotation.ToString();

            reFabricInput.Text = fabricVariantIndex.ToString();
            reFabricPatternInput.Text = fabricPatternIndex.ToString();

            reBodyInput.Text = rebodyVariantIndex.ToString();
            reBodyPatternInput.Text = rebodyPatternIndex.ToString();

            itemStateInput.Text = itemState.ToString();
            shouldUpdate = true;
        }

        #region On Changes -> UpdateEncodedId
        private void itemIdBox_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }

        private void itemRotationBox_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }

        private void reFabricInput_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }

        private void reFabricPatternInput_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }

        private void reBodyInput_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }

        private void reBodyPatternInput_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }

        private void itemStateInput_TextChanged(object sender, EventArgs e)
        {
            UpdateEncodedId();
        }
        #endregion
    }
}
