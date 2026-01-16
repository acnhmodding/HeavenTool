using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace HeavenTool.Forms.BCSV.Controls
{
    public class ColorPickerEditingControl : Button, IDataGridViewEditingControl
    {
        private DataGridView _dataGridView;
        private bool _valueChanged;
        private int _rowIndex;
        private Color _currentColor = Color.White;

        public ColorPickerEditingControl()
        {
            // Default appearance
            UpdateButtonAppearance();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowColorDialog();
           
        }

        public void ShowColorDialog()
        {
            using var dlg = new ColorDialog();
            dlg.Color = _currentColor;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                CurrentColor = dlg.Color;
                // Notify the grid that the user changed the value
                _valueChanged = true;
                _dataGridView?.NotifyCurrentCellDirty(true);
            }
        }

        /// <summary>
        /// Update Text and BackColor to reflect _currentColor
        /// </summary>
        private void UpdateButtonAppearance()
        {
            BackColor = _currentColor;
            ForeColor = _currentColor.GetBrightness() < 0.5f ? Color.White : Color.Black;
            Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
        }


        /// <summary>
        /// The color currently being edited
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                _currentColor = value;
                UpdateButtonAppearance();
            }
        }

        #region IDataGridViewEditingControl Members
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object EditingControlFormattedValue
        {
            get => Text; // returns the hex string
            set
            {
                if (value is string s)
                {
                    var c = ColorTranslator.FromHtml(s);
                    CurrentColor = c;
                }
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
            => EditingControlFormattedValue;

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            // You can mirror the cell style if you want
            Font = dataGridViewCellStyle.Font;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int EditingControlRowIndex
        {
            get => _rowIndex;
            set => _rowIndex = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EditingControlValueChanged
        {
            get => _valueChanged;
            set => _valueChanged = value;
        }

        public bool RepositionEditingControlOnValueChange => false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataGridView EditingControlDataGridView
        {
            get => _dataGridView;
            set => _dataGridView = value;
        }

        public Cursor EditingPanelCursor => Cursors.Default;

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            // Let the button handle Space (to click), otherwise let the grid handle navigation
            if (keyData == Keys.Space)
                return true;
            return !dataGridViewWantsInputKey;
        }

        public void PrepareEditingControlForEdit(bool selectAll)
        {
            // No initialization needed before edit starts
        }

        #endregion
    }
}
