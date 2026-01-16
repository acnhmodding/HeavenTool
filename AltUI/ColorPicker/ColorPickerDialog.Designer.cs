using System;
using AltUI.Controls;

namespace  AltUI.ColorPicker
{
  partial class ColorPickerDialog
  { /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            okButton = new DarkButton();
            cancelButton = new DarkButton();
            previewPanel = new System.Windows.Forms.Panel();
            loadPaletteButton = new DarkButton();
            savePaletteButton = new DarkButton();
            toolTip = new DarkToolTip();
            screenColorPicker = new ScreenColorPicker();
            colorWheel = new ColorWheel();
            colorEditor = new ColorEditor();
            colorGrid = new ColorGrid();
            colorEditorManager = new ColorEditorManager();
            SuspendLayout();
            // 
            // okButton
            // 
            okButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            okButton.BorderColour = System.Drawing.Color.Empty;
            okButton.Location = new System.Drawing.Point(501, 12);
            okButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            okButton.Name = "okButton";
            okButton.Padding = new System.Windows.Forms.Padding(6);
            okButton.Size = new System.Drawing.Size(88, 27);
            okButton.TabIndex = 1;
            okButton.Text = "OK";
            okButton.Click += OkButton_Click;
            // 
            // cancelButton
            // 
            cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            cancelButton.BorderColour = System.Drawing.Color.Empty;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cancelButton.Location = new System.Drawing.Point(501, 45);
            cancelButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            cancelButton.Name = "cancelButton";
            cancelButton.Padding = new System.Windows.Forms.Padding(6);
            cancelButton.Size = new System.Drawing.Size(88, 27);
            cancelButton.TabIndex = 2;
            cancelButton.Text = "Cancel";
            cancelButton.Click += CancelButton_Click;
            // 
            // previewPanel
            // 
            previewPanel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            previewPanel.Location = new System.Drawing.Point(501, 218);
            previewPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            previewPanel.Name = "previewPanel";
            previewPanel.Size = new System.Drawing.Size(88, 54);
            previewPanel.TabIndex = 3;
            previewPanel.Paint += PreviewPanel_Paint;
            // 
            // loadPaletteButton
            // 
            loadPaletteButton.BorderColour = System.Drawing.Color.Empty;
            loadPaletteButton.Location = new System.Drawing.Point(13, 167);
            loadPaletteButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            loadPaletteButton.Name = "loadPaletteButton";
            loadPaletteButton.Padding = new System.Windows.Forms.Padding(6);
            loadPaletteButton.Size = new System.Drawing.Size(27, 27);
            loadPaletteButton.TabIndex = 5;
            toolTip.SetToolTip(loadPaletteButton, "Load Palette");
            loadPaletteButton.Click += LoadPaletteButton_Click;
            // 
            // savePaletteButton
            // 
            savePaletteButton.BorderColour = System.Drawing.Color.Empty;
            savePaletteButton.Location = new System.Drawing.Point(39, 167);
            savePaletteButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            savePaletteButton.Name = "savePaletteButton";
            savePaletteButton.Padding = new System.Windows.Forms.Padding(6);
            savePaletteButton.Size = new System.Drawing.Size(27, 27);
            savePaletteButton.TabIndex = 6;
            toolTip.SetToolTip(savePaletteButton, "Save Palette");
            savePaletteButton.Click += SavePaletteButton_Click;
            // 
            // toolTip
            // 
            toolTip.OwnerDraw = true;
            // 
            // screenColorPicker
            // 
            screenColorPicker.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            screenColorPicker.Color = System.Drawing.Color.Black;
            screenColorPicker.Location = new System.Drawing.Point(501, 94);
            screenColorPicker.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            screenColorPicker.Name = "screenColorPicker";
            screenColorPicker.Size = new System.Drawing.Size(85, 98);
            toolTip.SetToolTip(screenColorPicker, "Click and drag to select screen color");
            screenColorPicker.Zoom = 6;
            // 
            // colorWheel
            // 
            colorWheel.Alpha = 1D;
            colorWheel.Color = System.Drawing.Color.FromArgb(0, 0, 0);
            colorWheel.Location = new System.Drawing.Point(13, 12);
            colorWheel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            colorWheel.Name = "colorWheel";
            colorWheel.Size = new System.Drawing.Size(192, 152);
            colorWheel.TabIndex = 4;
            // 
            // colorEditor
            // 
            colorEditor.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            colorEditor.BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            colorEditor.Color = System.Drawing.Color.FromArgb(0, 0, 0);
            colorEditor.Location = new System.Drawing.Point(214, 12);
            colorEditor.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            colorEditor.Name = "colorEditor";
            colorEditor.Size = new System.Drawing.Size(278, 260);
            colorEditor.TabIndex = 0;
            // 
            // colorGrid
            // 
            colorGrid.AutoAddColors = false;
            colorGrid.CellBorderStyle = ColorCellBorderStyle.None;
            colorGrid.EditMode = ColorEditingMode.Both;
            colorGrid.Location = new System.Drawing.Point(13, 200);
            colorGrid.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            colorGrid.Name = "colorGrid";
            colorGrid.Padding = new System.Windows.Forms.Padding(0);
            colorGrid.Palette = ColorPalette.Paint;
            colorGrid.SelectedCellStyle = ColorGridSelectedCellStyle.Standard;
            colorGrid.ShowCustomColors = false;
            colorGrid.Size = new System.Drawing.Size(192, 72);
            colorGrid.Spacing = new System.Drawing.Size(0, 0);
            colorGrid.TabIndex = 7;
            colorGrid.EditingColor += ColorGrid_EditingColor;
            // 
            // colorEditorManager
            // 
            colorEditorManager.Color = System.Drawing.Color.Empty;
            colorEditorManager.ColorEditor = colorEditor;
            colorEditorManager.ColorGrid = colorGrid;
            colorEditorManager.ColorWheel = colorWheel;
            colorEditorManager.ScreenColorPicker = screenColorPicker;
            colorEditorManager.ColorChanged += ColorEditorManager_ColorChanged;
            // 
            // ColorPickerDialog
            // 
            AcceptButton = okButton;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = cancelButton;
            ClientSize = new System.Drawing.Size(602, 284);
            Controls.Add(savePaletteButton);
            Controls.Add(loadPaletteButton);
            Controls.Add(previewPanel);
            Controls.Add(cancelButton);
            Controls.Add(okButton);
            Controls.Add(screenColorPicker);
            Controls.Add(colorWheel);
            Controls.Add(colorEditor);
            Controls.Add(colorGrid);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ColorPickerDialog";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Color Picker";
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private ColorGrid colorGrid;
    private ColorEditor colorEditor;
    private ColorWheel colorWheel;
    private ColorEditorManager colorEditorManager;
    private ScreenColorPicker screenColorPicker;
    private DarkButton okButton;
    private DarkButton cancelButton;
    private System.Windows.Forms.Panel previewPanel;
    private DarkButton loadPaletteButton;
    private DarkButton savePaletteButton;
    private DarkToolTip toolTip;
  }
}
