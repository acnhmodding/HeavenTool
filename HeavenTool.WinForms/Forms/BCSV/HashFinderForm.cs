using HeavenTool.IO;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV
{
    public partial class HashFinderForm : Form
    {

        public HashFinderForm()
        {
            InitializeComponent();
        }

        private string? lastResult = null;

        private void SearchButton_Click(object sender, EventArgs e)
        {
            if (TryConvertInt32(hashInput.Text, out var hash))
            {
                if (HashManager.GetHashTranslationOrNull((uint)hash) is string translatedHash)
                {
                    lastResult = translatedHash;
                    hashResultLbl.Text = $"Result: {translatedHash}";
                    return;
                }
                else hashResultLbl.Text = "Hash not found";
            }
            else hashResultLbl.Text = "Invalid Input";
            lastResult = null;
        }

        private void HashResultLbl_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(lastResult)) return;

            Clipboard.SetText(lastResult);
        }

        public static bool TryConvertInt32(string input, out int result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            var isNegative = false;

            // Check for negative sign
            if (input.StartsWith('-'))
            {
                isNegative = true;
                input = input[1..];
            }

            // Check if input is hexadecimal
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                input = input[2..]; // remove '0x'

                if (int.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                {
                    if (isNegative)
                        result = -result;

                    return true;
                }

                return false;
            }

            // Try normal decimal parse (with sign already removed if it was hex)
            if (isNegative)
                input = "-" + input;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            // Try hex parse without 0x prefix, in case it's hex like "ABCDEF"
            if (int.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            return false;
        }

        private void PasteButton_Click(object sender, EventArgs e)
        {
            hashInput.Text = Clipboard.GetText();
            SearchButton_Click(sender, e);
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            HashResultLbl_Click(sender, e);
        }
    }
}
