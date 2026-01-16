using AltUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkCheckBox : CheckBox
    {
        #region Field Region

        private DarkControlState _controlState = DarkControlState.Normal;

        private bool _spacePressed;
        private int _offset = 1;

        #endregion Field Region

        #region Property Region

        [Category("Appearance")]
        [Description("Offsets the check in the box to avoid a weird bug")]
        [DefaultValue(false)]
        public int Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Appearance Appearance => base.Appearance;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool AutoEllipsis => base.AutoEllipsis;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image BackgroundImage => base.BackgroundImage;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ImageLayout BackgroundImageLayout => base.BackgroundImageLayout;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool FlatAppearance => false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new FlatStyle FlatStyle => base.FlatStyle;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image Image => base.Image;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ContentAlignment ImageAlign => base.ImageAlign;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new int ImageIndex => base.ImageIndex;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new string ImageKey => base.ImageKey;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ImageList ImageList => base.ImageList;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ContentAlignment TextAlign => base.TextAlign;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new TextImageRelation TextImageRelation => base.TextImageRelation;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool ThreeState => base.ThreeState;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool UseCompatibleTextRendering => false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool UseVisualStyleBackColor => false;

        #endregion Property Region

        #region Constructor Region

        public DarkCheckBox()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
        }

        #endregion Constructor Region

        #region Method Region

        private void SetControlState(DarkControlState controlState)
        {
            if (_controlState != controlState)
            {
                _controlState = controlState;
                Invalidate();
            }
        }

        #endregion Method Region

        #region Event Handler Region

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_spacePressed)
                return;

            if (e.Button == MouseButtons.Left)
            {
                if (ClientRectangle.Contains(e.Location))
                    SetControlState(DarkControlState.Pressed);
                else
                    SetControlState(DarkControlState.Hover);
            }
            else
            {
                SetControlState(DarkControlState.Hover);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!ClientRectangle.Contains(e.Location))
                return;

            SetControlState(DarkControlState.Pressed);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_spacePressed)
                return;

            SetControlState(DarkControlState.Normal);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_spacePressed)
                return;

            SetControlState(DarkControlState.Normal);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);

            if (_spacePressed)
                return;

            var location = Cursor.Position;

            if (!ClientRectangle.Contains(location))
                SetControlState(DarkControlState.Normal);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);

            _spacePressed = false;

            var location = Cursor.Position;

            if (!ClientRectangle.Contains(location))
                SetControlState(DarkControlState.Normal);
            else
                SetControlState(DarkControlState.Hover);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Space)
            {
                _spacePressed = true;
                SetControlState(DarkControlState.Pressed);
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (e.KeyCode == Keys.Space)
            {
                _spacePressed = false;

                var location = Cursor.Position;

                if (!ClientRectangle.Contains(location))
                    SetControlState(DarkControlState.Normal);
                else
                    SetControlState(DarkControlState.Hover);
            }
        }

        #endregion Event Handler Region

        #region Paint Region

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

            var size = ThemeProvider.Theme.Sizes.CheckBoxSize;

            var textColor = ThemeProvider.Theme.Colors.LightText;
            var borderColor = ThemeProvider.Theme.Colors.GreySelection;
            var fillColor = Checked ? ThemeProvider.Theme.Colors.LightestBackground : ThemeProvider.Theme.Colors.GreyBackground;

            if (Enabled)
            {
                if (Focused)
                {
                    borderColor = ThemeProvider.Theme.Colors.BlueHighlight;
                    fillColor = ThemeProvider.Theme.Colors.BlueHighlight;
                }
                else if (_controlState == DarkControlState.Hover)
                {
                    borderColor = ThemeProvider.Theme.Colors.GreyHighlight;
                    fillColor = ThemeProvider.Theme.Colors.LightText;
                }
                else if (_controlState == DarkControlState.Pressed)
                {
                    borderColor = ThemeProvider.Theme.Colors.GreyHighlight;
                    fillColor = ThemeProvider.Theme.Colors.GreySelection;
                }
            }
            else
            {
                textColor = ThemeProvider.Theme.Colors.DisabledText;
                borderColor = ThemeProvider.Theme.Colors.GreySelection;
                fillColor = ThemeProvider.Theme.Colors.GreySelection;
            }

            if (Parent.GetType() == typeof(TabPage) || Parent.GetType() == typeof(DarkGroupBox) && ((DarkGroupBox)Parent).OpaqueBackground)
            {
                using var b = new SolidBrush(ThemeProvider.Theme.Colors.LightBackground);
                g.FillRectangle(b, rect);
            }
            else
            {
                using var b = new SolidBrush(ThemeProvider.Theme.Colors.GreyBackground);
                g.FillRectangle(b, rect);
            }

            using (var b = new SolidBrush(ThemeProvider.Theme.Colors.LightBackground))
            {
                var boxRect = new Rectangle(0, (rect.Height / 2) - (size / 2), size, size);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRoundedRectangle(b, boxRect, 2);
                g.SmoothingMode = SmoothingMode.None;
            }

            using (var p = new Pen(borderColor))
            {
                var boxRect = new Rectangle(0, (rect.Height / 2) - (size / 2), size, size);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawRoundedRectangle(p, boxRect, 2);
                g.SmoothingMode = SmoothingMode.None;
            }

            if (Checked)
            {
                using var p = new Pen(fillColor, 1);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, 3, 10 - _offset, 5, 13 - _offset);
                g.DrawLine(p, 5, 13 - _offset, 9, 7 - _offset);
                g.SmoothingMode = SmoothingMode.None;
            }

            using (var b = new SolidBrush(textColor))
            {
                var stringFormat = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near
                };

                var modRect = new Rectangle(size + 4, 0, rect.Width - size, rect.Height);
                g.DrawString(Text, Font, b, modRect, stringFormat);
            }
        }

        #endregion Paint Region
    }
}