using AltUI.Controls;

namespace  AltUI.ColorPicker
{
    sealed partial class ColorEditor
  {
    /// <summary> 
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary> 
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary> 
    /// Required method for Designer support - do not modify 
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
            this.rgbHeaderLabel = new AltUI.Controls.DarkLabel();
            this.rLabel = new AltUI.Controls.DarkLabel();
            this.rNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.rColorBar = new AltUI.ColorPicker.RgbaColorSlider();
            this.gLabel = new AltUI.Controls.DarkLabel();
            this.gColorBar = new AltUI.ColorPicker.RgbaColorSlider();
            this.gNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.bLabel = new AltUI.Controls.DarkLabel();
            this.bColorBar = new AltUI.ColorPicker.RgbaColorSlider();
            this.bNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.hexLabel = new AltUI.Controls.DarkLabel();
            this.hexTextBox = new AltUI.ColorPicker.ColorComboBox();
            this.lNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.lColorBar = new AltUI.ColorPicker.LightnessColorSlider();
            this.lLabel = new AltUI.Controls.DarkLabel();
            this.sNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.sColorBar = new AltUI.ColorPicker.SaturationColorSlider();
            this.sLabel = new AltUI.Controls.DarkLabel();
            this.hColorBar = new AltUI.ColorPicker.HueColorSlider();
            this.hNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.hLabel = new AltUI.Controls.DarkLabel();
            this.hslLabel = new AltUI.Controls.DarkLabel();
            this.aNumericUpDown = new AltUI.Controls.DarkNumericUpDown();
            this.aColorBar = new AltUI.ColorPicker.RgbaColorSlider();
            this.aLabel = new AltUI.Controls.DarkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.rNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.hNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.aNumericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // rgbHeaderLabel
            // 
            this.rgbHeaderLabel.AutoSize = true;
            this.rgbHeaderLabel.Location = new System.Drawing.Point(-4, 0);
            this.rgbHeaderLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.rgbHeaderLabel.Name = "rgbHeaderLabel";
            this.rgbHeaderLabel.Size = new System.Drawing.Size(32, 15);
            this.rgbHeaderLabel.TabIndex = 0;
            this.rgbHeaderLabel.Text = "RGB:";
            // 
            // rLabel
            // 
            this.rLabel.AutoSize = true;
            this.rLabel.Location = new System.Drawing.Point(4, 15);
            this.rLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.rLabel.Name = "rLabel";
            this.rLabel.Size = new System.Drawing.Size(17, 15);
            this.rLabel.TabIndex = 1;
            this.rLabel.Text = "R:";
            // 
            // rNumericUpDown
            // 
            this.rNumericUpDown.Location = new System.Drawing.Point(122, 12);
            this.rNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.rNumericUpDown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.rNumericUpDown.Name = "rNumericUpDown";
            this.rNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.rNumericUpDown.TabIndex = 2;
            this.rNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.rNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // rColorBar
            // 
            this.rColorBar.Location = new System.Drawing.Point(31, 15);
            this.rColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.rColorBar.Name = "rColorBar";
            this.rColorBar.Size = new System.Drawing.Size(84, 23);
            this.rColorBar.TabIndex = 3;
            this.rColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // gLabel
            // 
            this.gLabel.AutoSize = true;
            this.gLabel.Location = new System.Drawing.Point(4, 45);
            this.gLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.gLabel.Name = "gLabel";
            this.gLabel.Size = new System.Drawing.Size(18, 15);
            this.gLabel.TabIndex = 4;
            this.gLabel.Text = "G:";
            // 
            // gColorBar
            // 
            this.gColorBar.Channel = AltUI.ColorPicker.RgbaChannel.Green;
            this.gColorBar.Location = new System.Drawing.Point(31, 45);
            this.gColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gColorBar.Name = "gColorBar";
            this.gColorBar.Size = new System.Drawing.Size(84, 23);
            this.gColorBar.TabIndex = 6;
            this.gColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // gNumericUpDown
            // 
            this.gNumericUpDown.Location = new System.Drawing.Point(122, 42);
            this.gNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gNumericUpDown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.gNumericUpDown.Name = "gNumericUpDown";
            this.gNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.gNumericUpDown.TabIndex = 5;
            this.gNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.gNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // bLabel
            // 
            this.bLabel.AutoSize = true;
            this.bLabel.Location = new System.Drawing.Point(4, 75);
            this.bLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.bLabel.Name = "bLabel";
            this.bLabel.Size = new System.Drawing.Size(17, 15);
            this.bLabel.TabIndex = 7;
            this.bLabel.Text = "B:";
            // 
            // bColorBar
            // 
            this.bColorBar.Channel = AltUI.ColorPicker.RgbaChannel.Blue;
            this.bColorBar.Location = new System.Drawing.Point(31, 75);
            this.bColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bColorBar.Name = "bColorBar";
            this.bColorBar.Size = new System.Drawing.Size(84, 23);
            this.bColorBar.TabIndex = 9;
            this.bColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // bNumericUpDown
            // 
            this.bNumericUpDown.Location = new System.Drawing.Point(122, 72);
            this.bNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bNumericUpDown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.bNumericUpDown.Name = "bNumericUpDown";
            this.bNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.bNumericUpDown.TabIndex = 8;
            this.bNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.bNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // hexLabel
            // 
            this.hexLabel.AutoSize = true;
            this.hexLabel.Location = new System.Drawing.Point(4, 108);
            this.hexLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.hexLabel.Name = "hexLabel";
            this.hexLabel.Size = new System.Drawing.Size(31, 15);
            this.hexLabel.TabIndex = 10;
            this.hexLabel.Text = "Hex:";
            // 
            // hexTextBox
            // 
            this.hexTextBox.Location = new System.Drawing.Point(122, 105);
            this.hexTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.hexTextBox.Name = "hexTextBox";
            this.hexTextBox.Size = new System.Drawing.Size(67, 24);
            this.hexTextBox.TabIndex = 11;
            this.hexTextBox.SelectedIndexChanged += new System.EventHandler(this.hexTextBox_SelectedIndexChanged);
            this.hexTextBox.TextChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // lNumericUpDown
            // 
            this.lNumericUpDown.Location = new System.Drawing.Point(122, 216);
            this.lNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.lNumericUpDown.Name = "lNumericUpDown";
            this.lNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.lNumericUpDown.TabIndex = 20;
            this.lNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.lNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // lColorBar
            // 
            this.lColorBar.Location = new System.Drawing.Point(31, 219);
            this.lColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.lColorBar.Name = "lColorBar";
            this.lColorBar.Size = new System.Drawing.Size(84, 23);
            this.lColorBar.TabIndex = 21;
            this.lColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // lLabel
            // 
            this.lLabel.AutoSize = true;
            this.lLabel.Location = new System.Drawing.Point(4, 222);
            this.lLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lLabel.Name = "lLabel";
            this.lLabel.Size = new System.Drawing.Size(16, 15);
            this.lLabel.TabIndex = 19;
            this.lLabel.Text = "L:";
            // 
            // sNumericUpDown
            // 
            this.sNumericUpDown.Location = new System.Drawing.Point(122, 186);
            this.sNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.sNumericUpDown.Name = "sNumericUpDown";
            this.sNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.sNumericUpDown.TabIndex = 17;
            this.sNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.sNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // sColorBar
            // 
            this.sColorBar.Location = new System.Drawing.Point(31, 189);
            this.sColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.sColorBar.Name = "sColorBar";
            this.sColorBar.Size = new System.Drawing.Size(84, 23);
            this.sColorBar.TabIndex = 18;
            this.sColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // sLabel
            // 
            this.sLabel.AutoSize = true;
            this.sLabel.Location = new System.Drawing.Point(5, 192);
            this.sLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.sLabel.Name = "sLabel";
            this.sLabel.Size = new System.Drawing.Size(16, 15);
            this.sLabel.TabIndex = 16;
            this.sLabel.Text = "S:";
            // 
            // hColorBar
            // 
            this.hColorBar.Location = new System.Drawing.Point(31, 159);
            this.hColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.hColorBar.Name = "hColorBar";
            this.hColorBar.Size = new System.Drawing.Size(84, 23);
            this.hColorBar.TabIndex = 15;
            this.hColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // hNumericUpDown
            // 
            this.hNumericUpDown.Location = new System.Drawing.Point(122, 156);
            this.hNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.hNumericUpDown.Maximum = new decimal(new int[] {
            359,
            0,
            0,
            0});
            this.hNumericUpDown.Name = "hNumericUpDown";
            this.hNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.hNumericUpDown.TabIndex = 14;
            this.hNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.hNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // hLabel
            // 
            this.hLabel.AutoSize = true;
            this.hLabel.Location = new System.Drawing.Point(4, 162);
            this.hLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.hLabel.Name = "hLabel";
            this.hLabel.Size = new System.Drawing.Size(19, 15);
            this.hLabel.TabIndex = 13;
            this.hLabel.Text = "H:";
            // 
            // hslLabel
            // 
            this.hslLabel.AutoSize = true;
            this.hslLabel.Location = new System.Drawing.Point(4, 135);
            this.hslLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.hslLabel.Name = "hslLabel";
            this.hslLabel.Size = new System.Drawing.Size(31, 15);
            this.hslLabel.TabIndex = 12;
            this.hslLabel.Text = "HSL:";
            // 
            // aNumericUpDown
            // 
            this.aNumericUpDown.Location = new System.Drawing.Point(122, 246);
            this.aNumericUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.aNumericUpDown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.aNumericUpDown.Name = "aNumericUpDown";
            this.aNumericUpDown.Size = new System.Drawing.Size(68, 23);
            this.aNumericUpDown.TabIndex = 23;
            this.aNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.aNumericUpDown.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // aColorBar
            // 
            this.aColorBar.Channel = AltUI.ColorPicker.RgbaChannel.Alpha;
            this.aColorBar.Location = new System.Drawing.Point(31, 249);
            this.aColorBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.aColorBar.Name = "aColorBar";
            this.aColorBar.Size = new System.Drawing.Size(84, 23);
            this.aColorBar.TabIndex = 24;
            this.aColorBar.ValueChanged += new System.EventHandler(this.ValueChangedHandler);
            // 
            // aLabel
            // 
            this.aLabel.AutoSize = true;
            this.aLabel.Location = new System.Drawing.Point(4, 252);
            this.aLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.aLabel.Name = "aLabel";
            this.aLabel.Size = new System.Drawing.Size(41, 15);
            this.aLabel.TabIndex = 22;
            this.aLabel.Text = "Alpha:";
            // 
            // ColorEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(31)))), ((int)(((byte)(32)))));
            this.Controls.Add(this.aLabel);
            this.Controls.Add(this.aNumericUpDown);
            this.Controls.Add(this.aColorBar);
            this.Controls.Add(this.hslLabel);
            this.Controls.Add(this.lNumericUpDown);
            this.Controls.Add(this.lColorBar);
            this.Controls.Add(this.lLabel);
            this.Controls.Add(this.sNumericUpDown);
            this.Controls.Add(this.sColorBar);
            this.Controls.Add(this.sLabel);
            this.Controls.Add(this.hColorBar);
            this.Controls.Add(this.hNumericUpDown);
            this.Controls.Add(this.hLabel);
            this.Controls.Add(this.hexTextBox);
            this.Controls.Add(this.hexLabel);
            this.Controls.Add(this.bNumericUpDown);
            this.Controls.Add(this.bColorBar);
            this.Controls.Add(this.bLabel);
            this.Controls.Add(this.gNumericUpDown);
            this.Controls.Add(this.gColorBar);
            this.Controls.Add(this.gLabel);
            this.Controls.Add(this.rColorBar);
            this.Controls.Add(this.rNumericUpDown);
            this.Controls.Add(this.rLabel);
            this.Controls.Add(this.rgbHeaderLabel);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "ColorEditor";
            this.Size = new System.Drawing.Size(202, 284);
            ((System.ComponentModel.ISupportInitialize)(this.rNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.hNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.aNumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

    }

    #endregion

    private DarkLabel rgbHeaderLabel;
    private DarkLabel rLabel;
    private DarkNumericUpDown rNumericUpDown;
    private RgbaColorSlider rColorBar;
    private DarkLabel gLabel;
    private RgbaColorSlider gColorBar;
    private DarkNumericUpDown gNumericUpDown;
    private DarkLabel bLabel;
    private RgbaColorSlider bColorBar;
    private DarkNumericUpDown bNumericUpDown;
    private DarkLabel hexLabel;
    private  AltUI.ColorPicker.ColorComboBox hexTextBox;
    private DarkNumericUpDown lNumericUpDown;
    private LightnessColorSlider lColorBar;
    private DarkLabel lLabel;
    private DarkNumericUpDown sNumericUpDown;
    private SaturationColorSlider sColorBar;
    private DarkLabel sLabel;
    private HueColorSlider hColorBar;
    private DarkNumericUpDown hNumericUpDown;
    private DarkLabel hLabel;
    private DarkLabel hslLabel;
    private DarkNumericUpDown aNumericUpDown;
    private RgbaColorSlider aColorBar;
    private DarkLabel aLabel;
  }
}
