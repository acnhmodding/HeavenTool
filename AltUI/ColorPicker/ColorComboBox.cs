// Cyotek Color Picker Controls Library
// http://cyotek.com/blog/tag/colorpicker

// Copyright (c) 2021 Cyotek Ltd.

// This work is licensed under the MIT License.
// See LICENSE.TXT for the full text

// Found this code useful?
// https://www.cyotek.com/contribute

using AltUI.Config;
using AltUI.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace AltUI.ColorPicker
{
    [ToolboxItem(false)]
    internal sealed class ColorComboBox : DarkComboBox
    {
        #region Public Constructors

        private DarkTextBox _textBox;

        public ColorComboBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            DrawMode = DrawMode.OwnerDrawVariable;

            FlatStyle = FlatStyle.Flat;
            DropDownStyle = ComboBoxStyle.DropDown;
        }

        #endregion Public Constructors

        #region Public Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new DrawMode DrawMode
        {
            get => base.DrawMode;
            set => base.DrawMode = value;
        }

        #endregion Public Properties

        #region Internal Methods

        internal void FillNamedColors()
        {
            BeginUpdate();
            Items.Clear();
            AddColorProperties(typeof(SystemColors));
            AddColorProperties(typeof(Color));
            SetDropDownWidth();
            EndUpdate();
        }
        private void OnTextBoxTextChanged(object sender, EventArgs e)
        {
            Text = _textBox.Text;
        }

        private void OnTextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (Char.IsControl(e.KeyChar)) return;
            // Add the typed character to the text box's text
            _textBox.Text += e.KeyChar;
            // Move the cursor to the end of the text
            _textBox.SelectionStart = _textBox.Text.Length;
            _textBox.SelectionLength = 0;
            // Cancel the key press event so that the character is not added twice
            e.Handled = true;
        }


        #endregion Internal Methods

        #region Protected Methods

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index != -1)
            {
                var fillColor = ThemeProvider.Theme.Colors.GreyBackground;

                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected ||
                    (e.State & DrawItemState.Focus) == DrawItemState.Focus ||
                    (e.State & DrawItemState.NoFocusRect) != DrawItemState.NoFocusRect && !TabStop)
                {
                    fillColor = ThemeProvider.Theme.Colors.BlueSelection;
                }

                e.Graphics.FillRectangle(new SolidBrush(fillColor), e.Bounds);

                var name = (string)Items[e.Index];
                var color = Color.FromName(name);
                var colorBox = new Rectangle(e.Bounds.Left + 1, e.Bounds.Top + 1, e.Bounds.Height - 3, e.Bounds.Height - 3);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(color))
                {
                    e.Graphics.FillRoundedRectangle(brush, colorBox, 7);
                }
                e.Graphics.DrawRoundedRectangle(new Pen(ThemeProvider.Theme.Colors.GreySelection), colorBox, 7);
                e.Graphics.SmoothingMode = SmoothingMode.Default;

                TextRenderer.DrawText(e.Graphics, AddSpaces(name), Font,
                    new Point(colorBox.Right + 3, colorBox.Top), ThemeProvider.Theme.Colors.LightText);
            }
        }

        protected override void OnDropDown(EventArgs e)
        {
            if (Items.Count == 0)
            {
                FillNamedColors();
            }

            base.OnDropDown(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);

            SetDropDownWidth();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsNavigationKey(e.KeyCode) && Items.Count == 0)
            {
                FillNamedColors();
            }

            base.OnKeyDown(e);
        }

        #endregion Protected Methods

        #region Private Methods

        private static bool IsNavigationKey(Keys keys)
        {
            return keys is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown;
        }

        private void AddColorProperties(IReflect type)
        {
            var colorType = typeof(Color);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);

            foreach (var t in properties)
            {
                if (t.PropertyType != colorType) continue;

                var color = (Color)t.GetValue(type, null);
                if (!color.IsEmpty)
                {
                    Items.Add(color.Name);
                }
            }
        }

        private static string AddSpaces(string text)
        {
            string result;

            //http://stackoverflow.com/a/272929/148962

            if (!string.IsNullOrEmpty(text))
            {
                var newText = new StringBuilder(text.Length * 2);
                newText.Append(text[0]);
                for (var i = 1; i < text.Length; i++)
                {
                    if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                    {
                        newText.Append(' ');
                    }
                    newText.Append(text[i]);
                }

                result = newText.ToString();
            }
            else
            {
                result = null;
            }

            return result;
        }

        private void SetDropDownWidth()
        {
            if (Items.Count != 0)
            {
                DropDownWidth = ItemHeight * 2 + Items.Cast<string>().Max(s => TextRenderer.MeasureText(s, Font).Width);
            }
        }

        #endregion Private Methods
    }
}