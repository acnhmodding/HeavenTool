using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace HeavenTool.DataTable
{
    /// <summary>
    /// Represents a column of <see cref="DataGridViewDropDownComboBoxCell"/> objects.
    /// </summary>
    public class DataGridViewDropDownComboBoxColumn : DataGridViewComboBoxColumn
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridViewDropDownComboBoxColumn"/> class to the default state.
        /// </summary>
        public DataGridViewDropDownComboBoxColumn() : base()
        {
            base.CellTemplate = new DataGridViewDropDownComboBoxCell();
        }

        /// <summary>
        /// Gets or sets the template used to create cells.
        /// </summary>
        /// <returns>A <see cref="DataGridViewCell"/> that all other cells in the column are modeled after. The default value is a new <see cref="DataGridViewDropDownComboBoxCell"/>.</returns>
        /// <exception cref="InvalidCastException">When setting this property to a value that is not of type <see cref="DataGridViewDropDownComboBoxCell"/>.</exception>

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override DataGridViewCell CellTemplate
        {
            get => base.CellTemplate;
            set
            {
                if ((value != null) && (!(value is DataGridViewDropDownComboBoxCell)))
                {
                    throw new InvalidCastException("CellTemplate must be a DataGridViewDropDownComboBoxCell");
                }

                base.CellTemplate = value;
            }
        }
    }

    /// <summary>
    /// Displays a combo box in a <see cref="DataGridView"/> control.
    /// </summary>
    public class DataGridViewDropDownComboBoxCell : DataGridViewComboBoxCell
    {
        /// <summary>
        /// Gets the type of the cell's hosted editing control.
        /// </summary>
        /// <returns>The <see cref="Type"/> of the underlying editing control. This property always returns <see cref="DataGridViewDropDownComboBoxEditingControl"/>.</returns>
        public override Type EditType => typeof(DataGridViewDropDownComboBoxEditingControl);

        /// <summary>
        /// Gets the data type of the values in the cell.
        /// </summary>
        /// <returns>A <see cref="Type"/> representing the data type of the value in the cell.</returns>
        public override Type ValueType => typeof(string);

        /// <summary>
        /// Gets the class type of the formatted value associated with the cell.
        /// </summary>
        /// <returns>The type of the cell's formatted value. This property always returns <see cref="string"/>.</returns>
        public override Type FormattedValueType => typeof(string);

        /// <summary>
        /// Gets the default value for a cell in the row for new records.
        /// </summary>
        /// <returns>An <see cref="object"/> representing the default value.</returns>
        public override object DefaultNewRowValue => string.Empty;

        /// <summary>
        /// Gets the formatted value of the cell's data.
        /// </summary>
        /// <param name="value">The value to be formatted.</param>
        /// <param name="rowIndex">The index of the cell's parent row.</param>
        /// <param name="cellStyle">The <see cref="DataGridViewCellStyle"/> in effect for the cell.</param>
        /// <param name="valueTypeConverter">A <see cref="TypeConverter"/> associated with the value type that provides custom conversion to the formatted value type, or null if no such custom conversion is needed.</param>
        /// <param name="formattedValueTypeConverter">A <see cref="TypeConverter"/> associated with the formatted value type that provides custom conversion from the value type, or null if no such custom conversion is needed.</param>
        /// <param name="context">A bitwise combination of <see cref="DataGridViewDataErrorContexts"/> values describing the context in which the formatted value is needed.</param>
        /// <returns>The value of the cell's data after formatting has been applied or <see cref="string.Empty"/> if the cell is not part of a <see cref="DataGridView"/> control.</returns>
        /// <exception cref="Exception">Formatting failed and either there is no handler for the <see cref="DataGridView.DataError"/> event of the <see cref="DataGridView"/> control or the handler set the <see cref="DataGridViewDataErrorEventArgs.ThrowException"/> property to true. The exception object can typically be cast to type <see cref="FormatException"/> for type conversion errors.</exception>
        protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
        {
            if (value is string text)
            {
                return text;
            }
            else
            {
                if (DataGridView != null)
                {
                    DataGridViewDataErrorEventArgs dataGridViewDataErrorEventArgs = new DataGridViewDataErrorEventArgs(new FormatException("value must be a string"), ColumnIndex, rowIndex, context);
                    RaiseDataError(dataGridViewDataErrorEventArgs);
                    if (dataGridViewDataErrorEventArgs.ThrowException)
                    {
                        throw dataGridViewDataErrorEventArgs.Exception;
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Attaches and initializes the hosted editing control.
        /// </summary>
        /// <param name="rowIndex">The index of the cell's parent row.</param>
        /// <param name="initialFormattedValue">The initial value to be displayed in the control.</param>
        /// <param name="dataGridViewCellStyle">A <see cref="DataGridViewCellStyle"/> that determines the appearance of the hosted control.</param>
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

            if (DataGridView?.EditingControl is DataGridViewDropDownComboBoxEditingControl control)
            {
                // re-initialize control since DataGridViewComboBoxCell resets it, then set text
                control.Initialize();
                if (initialFormattedValue is string text) control.Text = text;
            }
        }
    }

    /// <summary>
    /// Represents the hosted combo box control in a <see cref="DataGridViewDropDownComboBoxCell"/>.
    /// </summary>
    public class DataGridViewDropDownComboBoxEditingControl : DataGridViewComboBoxEditingControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridViewDropDownComboBoxEditingControl"/> class.
        /// </summary>
        public DataGridViewDropDownComboBoxEditingControl() : base()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes behavior of the <see cref="DataGridViewDropDownComboBoxEditingControl"/>.
        /// </summary>
        internal void Initialize()
        {
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteSource = AutoCompleteSource.ListItems;
            AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        }

        /// <summary>
        /// Raises the <see cref="Control.TextChanged"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            // Notify DataGridView of value change
            EditingControlValueChanged = true;
            EditingControlDataGridView?.NotifyCurrentCellDirty(true);
        }
    }
}
